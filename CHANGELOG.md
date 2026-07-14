# Changelog

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
