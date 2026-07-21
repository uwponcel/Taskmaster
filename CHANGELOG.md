# Changelog

## v2.0.0

Taskmaster has a refreshed look and smoother controls throughout the module.

### Added

- Multi-select tasks with click, Shift-click for ranges, and Ctrl-click for
  individual selections
- A **Delete selected** action for quickly removing several tasks
- Fixed tab navigation buttons and mouse-wheel scrolling for longer tab lists

### Improved

- New Taskmaster logo across the module and project pages
- Redesigned tabs with clearer active states, progress badges, color
  underlines, and rename-in-place editing
- Bottom action bar with hide-done and lock controls on the left and
  **Add task** on the right
- New tasks automatically scroll into view when added
- Larger, cleaner pencil, save, and cancel actions directly on task rows
- Tab move destinations are grouped under a submenu to keep menus compact
- Better spacing and resizing throughout the main window

## v1.0.0 - Initial release

Based on `valorayne.bh.todos`. Built around one rule: no stored "next reset"
timestamps - every countdown is derived from UTC math at the moment it's
shown, so it can't rot after a hibernate, DST change, or a few days away.

### Added

- Freeform tabs with accent colors and horizontal scrolling
- Reset schedules per task: daily/weekly server, map bonus, PSNA, WvW EU/NA,
  local time, cooldown, or never
- Counters and subtasks, with one shared countdown per group
- Click-to-copy, notes, due-soon warning, tab progress badges
- Tab sharing via clipboard JSON (notes and progress stripped)
- Lock mode, hide-done toggle, unfocused opacity, toggle keybind
- Reliability readout in settings showing the engine's UTC math live
- `tasks.json` persistence with automatic backup and corruption quarantine
