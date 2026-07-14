using Blish_HUD.Input;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework.Input;

namespace Taskmaster.Settings
{
    public class ModuleSettings
    {
        public SettingEntry<KeyBinding> ToggleWindow { get; }
        public SettingEntry<bool> HideDone { get; }
        public SettingEntry<bool> LockTasks { get; }
        public SettingEntry<float> UnfocusedOpacity { get; }
        public SettingEntry<bool> ShowOnMap { get; }

        public ModuleSettings(SettingCollection settings)
        {
            ToggleWindow = settings.DefineSetting("ToggleWindow",
                new KeyBinding(ModifierKeys.Alt, Keys.T),
                () => "Show/hide window", () => "Keybind to toggle the Taskmaster window.");

            HideDone = settings.DefineSetting("HideDone", false,
                () => "Hide completed tasks",
                () => "Hide tasks that are already done, and hide tabs that are fully completed.");

            LockTasks = settings.DefineSetting("LockTasks", false,
                () => "Lock tasks", () => "Prevent editing, adding, and deleting; checking still works.");

            UnfocusedOpacity = settings.DefineSetting("UnfocusedOpacity", 1.0f,
                () => "Unfocused opacity", () => "Window opacity when the mouse is not over it.");
            UnfocusedOpacity.SetRange(0.2f, 1.0f);

            ShowOnMap = settings.DefineSetting("ShowOnMap", false,
                () => "Show on map", () => "Keep the window visible while the full-screen map is open.");
        }
    }
}
