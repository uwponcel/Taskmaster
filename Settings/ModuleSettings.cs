using Blish_HUD.Input;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework.Input;

namespace Taskmaster.Settings
{
    public class ModuleSettings
    {
        public SettingEntry<KeyBinding> ToggleWindow { get; }
        public SettingEntry<float> UnfocusedOpacity { get; }
        public SettingEntry<bool> ShowOnMap { get; }
        public SettingEntry<float> InterfaceScale { get; }
        public SettingEntry<float> TextScale { get; }
        public SettingEntry<bool> EnableDragReordering { get; }

        public ModuleSettings(SettingCollection settings)
        {
            ToggleWindow = settings.DefineSetting("ToggleWindow",
                new KeyBinding(ModifierKeys.Alt, Keys.T),
                () => "Show/hide window", () => "Keybind to toggle the Taskmaster window.");

            UnfocusedOpacity = settings.DefineSetting("UnfocusedOpacity", 1.0f,
                () => "Unfocused opacity", () => "Window opacity when the mouse is not over it.");
            UnfocusedOpacity.SetRange(0.2f, 1.0f);

            ShowOnMap = settings.DefineSetting("ShowOnMap", false,
                () => "Show on map", () => "Keep the window visible while the full-screen map is open.");

            InterfaceScale = settings.DefineSetting("InterfaceScale", 1.0f,
                () => "Interface scale",
                () => "Scale rows, buttons, icons, spacing, and minimum window size.");
            InterfaceScale.SetRange(UI.TaskmasterSizing.MinInterfaceScale, UI.TaskmasterSizing.MaxInterfaceScale);

            TextScale = settings.DefineSetting("TextScale", 1.0f,
                () => "Text scale",
                () => "Choose larger or smaller native Blish HUD fonts throughout Taskmaster.");
            TextScale.SetRange(UI.TaskmasterSizing.MinTextScale, UI.TaskmasterSizing.MaxTextScale);

            EnableDragReordering = settings.DefineSetting("EnableDragReordering", true,
                () => "Drag to reorder tasks",
                () => "Allow dragging task and subtask rows to reorder them.");
        }
    }
}
