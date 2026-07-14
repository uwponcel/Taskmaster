using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Taskmaster.Services;
using Taskmaster.Settings;
using Taskmaster.UI;

namespace Taskmaster
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Module>();

        internal static Module Instance { get; private set; }

        internal SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;

        private ModuleSettings _settings;
        private TaskStore _store;
        private TaskmasterWindow _window;
        private CornerIcon _cornerIcon;
        private double _minuteAccumulator;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters)
        {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _settings = new ModuleSettings(settings);
        }

        protected override Task LoadAsync()
        {
            var dir = DirectoriesManager.GetFullDirectoryPath("taskmaster");
            _store = new TaskStore(dir);
            var result = _store.Load();
            Logger.Info($"Task store loaded: {result.Outcome}, {_store.Tabs.Count} tab(s)");

            switch (result.Outcome)
            {
                case TaskStoreLoadOutcome.LoadedBackup:
                    ScreenNotification.ShowNotification(
                        "Taskmaster: tasks.json was corrupt - restored from backup",
                        ScreenNotification.NotificationType.Warning);
                    break;
                case TaskStoreLoadOutcome.StartedEmptyAfterCorruption:
                    ScreenNotification.ShowNotification(
                        $"Taskmaster: task file was corrupt and no backup found. Quarantined at {result.QuarantinedPath}",
                        ScreenNotification.NotificationType.Error);
                    break;
                case TaskStoreLoadOutcome.VersionTooNew:
                    ScreenNotification.ShowNotification(
                        "Taskmaster: your tasks were saved by a newer version - update the module. Running read-only.",
                        ScreenNotification.NotificationType.Error);
                    break;
            }
            return Task.CompletedTask;
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            int startupResets = ResetEngine.ApplyResets(_store.Tabs, DateTime.UtcNow);
            if (startupResets > 0)
            {
                _store.MarkDirty(DateTime.UtcNow);
                Logger.Info($"Applied {startupResets} reset(s) at startup");
            }

            _window = new TaskmasterWindow(_store, _settings);
            _window.Hide();

            _cornerIcon = new CornerIcon
            {
                Icon = ContentsManager.GetTexture("corner-icon.png"),
                BasicTooltipText = "Taskmaster"
            };
            _cornerIcon.Click += (s, ev) => ToggleWindow();

            _settings.ToggleWindow.Value.Enabled = true;
            _settings.ToggleWindow.Value.Activated += (s, ev) => ToggleWindow();

            base.OnModuleLoaded(e);
        }

        private void ToggleWindow()
        {
            if (_window == null) return;
            if (_window.Visible) _window.Hide(); else _window.Show();
        }

        public override Blish_HUD.Graphics.UI.IView GetSettingsView()
        {
            return new Taskmaster.Settings.TaskmasterSettingsView(_settings);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_store == null) return;

            _store.FlushIfDue(DateTime.UtcNow);

            _minuteAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
            if (_minuteAccumulator >= 60)
            {
                _minuteAccumulator = 0;
                _window?.OnMinuteTick(DateTime.UtcNow);
            }

            if (_window != null && _window.Visible && !_settings.ShowOnMap.Value
                && GameService.Gw2Mumble.IsAvailable && GameService.Gw2Mumble.UI.IsMapOpen)
            {
                _window.Hide();
            }
        }

        protected override void Unload()
        {
            _settings.ToggleWindow.Value.Enabled = false;
            _store?.Save();
            _window?.Dispose();
            _cornerIcon?.Dispose();
            Instance = null;
        }
    }
}
