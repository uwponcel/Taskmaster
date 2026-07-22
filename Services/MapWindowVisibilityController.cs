namespace Taskmaster.Services
{
    public enum WindowVisibilityAction
    {
        None,
        Show,
        Hide
    }

    public sealed class MapWindowVisibilityController
    {
        private bool _restoreAfterMap;

        public WindowVisibilityAction Toggle(bool isVisible, bool hiddenByMap)
        {
            if (isVisible)
            {
                _restoreAfterMap = false;
                return WindowVisibilityAction.Hide;
            }

            if (hiddenByMap)
            {
                _restoreAfterMap = !_restoreAfterMap;
                return WindowVisibilityAction.None;
            }

            _restoreAfterMap = false;
            return WindowVisibilityAction.Show;
        }

        public WindowVisibilityAction Update(bool isVisible, bool hiddenByMap)
        {
            if (hiddenByMap)
            {
                if (!isVisible)
                    return WindowVisibilityAction.None;

                _restoreAfterMap = true;
                return WindowVisibilityAction.Hide;
            }

            if (!_restoreAfterMap)
                return WindowVisibilityAction.None;

            _restoreAfterMap = false;
            return WindowVisibilityAction.Show;
        }
    }
}
