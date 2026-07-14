using System;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Taskmaster.Models;
using Taskmaster.Services;
using Taskmaster.UI;

namespace Taskmaster.Settings
{
    /// <summary>
    /// Custom settings view: default setting controls + a live "what the reset engine
    /// thinks" readout, so reset problems are diagnosable from a screenshot.
    /// </summary>
    public class TaskmasterSettingsView : View
    {
        private readonly ModuleSettings _settings;

        public TaskmasterSettingsView(ModuleSettings settings)
        {
            _settings = settings;
        }

        protected override void Build(Container buildPanel)
        {
            // Fixed row height rather than AutoSize + reading container.Bottom - a
            // ViewContainer's AutoSize height doesn't resolve synchronously right after
            // Show(view) for every setting type (the keybinding row in particular), so
            // stacking off its live Bottom caused the next row to overlap it.
            const int rowHeight = 32;
            const int rowGap = 6;

            // HideDone and LockTasks are deliberately not listed here - they're
            // controlled entirely by the eye/lock icon buttons in the Taskmaster
            // window itself, so showing them again in the global settings panel would
            // just be a redundant, easy-to-desync duplicate toggle.
            int y = 10;
            foreach (var setting in new SettingEntry[]
                     { _settings.ToggleWindow, _settings.UnfocusedOpacity, _settings.ShowOnMap })
            {
                var view = Blish_HUD.Settings.UI.Views.SettingView.FromType(setting, buildPanel.Width);
                if (view != null)
                {
                    var container = new ViewContainer
                    {
                        Parent = buildPanel,
                        Location = new Point(10, y),
                        Width = buildPanel.Width - 20,
                        Height = rowHeight
                    };
                    container.Show(view);
                    y += rowHeight + rowGap;
                }
            }

            y += 14;
            new Label
            {
                Parent = buildPanel,
                Location = new Point(10, y),
                Width = buildPanel.Width - 20,
                Height = 24,
                Text = "Reset engine status (server time is UTC)",
                ShowShadow = true
            };
            y += 26;

            var nowUtc = DateTime.UtcNow;
            var statusLines = new ValueTuple<string, ResetScheduleType>[]
            {
                ("Daily reset", ResetScheduleType.DailyServer),
                ("Weekly reset", ResetScheduleType.WeeklyServer),
                ("Map bonus rotation", ResetScheduleType.MapBonus),
                ("PSNA rotation", ResetScheduleType.Psna),
                ("WvW EU reset", ResetScheduleType.WvwEu),
                ("WvW NA reset", ResetScheduleType.WvwNa)
            };
            foreach (var line in statusLines)
            {
                var next = ResetEngine.NextBoundary(new TodoTask { Schedule = line.Item2 }, nowUtc);
                var text = next.HasValue
                    ? $"{line.Item1}: in {TaskRow.FormatCountdown(next.Value - nowUtc)} ({next.Value:ddd HH:mm} UTC)"
                    : $"{line.Item1}: n/a";
                new Label
                {
                    Parent = buildPanel,
                    Location = new Point(20, y),
                    Width = buildPanel.Width - 30,
                    Height = 20,
                    Text = text
                };
                y += 22;
            }
            new Label
            {
                Parent = buildPanel,
                Location = new Point(20, y),
                Width = buildPanel.Width - 30,
                Height = 20,
                Text = $"Module clock now: {nowUtc:yyyy-MM-dd HH:mm} UTC / {TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local):HH:mm} local"
            };
        }
    }
}
