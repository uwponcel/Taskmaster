using Microsoft.Xna.Framework.Graphics;

namespace Taskmaster.UI.Controls
{
    /// <summary>Cached accessors for embedded glyphs (ref/*.png). Loaded once, never disposed.</summary>
    public static class TaskmasterIcons
    {
        private static Texture2D _check, _trash, _clipboard, _cancel, _eye, _lock,
            _plus, _chevronUp, _chevronDown, _pencil, _note;

        public static Texture2D Check => _check ?? (_check = Load("check-icon.png"));
        public static Texture2D Trash => _trash ?? (_trash = Load("trash-icon.png"));
        public static Texture2D Clipboard => _clipboard ?? (_clipboard = Load("clipboard-icon.png"));
        public static Texture2D Cancel => _cancel ?? (_cancel = Load("cancel-icon.png"));
        public static Texture2D Eye => _eye ?? (_eye = Load("eye-icon.png"));
        public static Texture2D Lock => _lock ?? (_lock = Load("lock-icon.png"));
        public static Texture2D Plus => _plus ?? (_plus = Load("plus-icon.png"));
        public static Texture2D ChevronUp => _chevronUp ?? (_chevronUp = Load("chevron-up-icon.png"));
        public static Texture2D ChevronDown => _chevronDown ?? (_chevronDown = Load("chevron-down-icon.png"));
        public static Texture2D Pencil => _pencil ?? (_pencil = Load("pencil-icon.png"));
        public static Texture2D Note => _note ?? (_note = Load("note-icon.png"));

        private static Texture2D Load(string fileName)
        {
            return Module.Instance.ContentsManager.GetTexture(fileName);
        }
    }
}
