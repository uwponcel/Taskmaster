<p align="center">
  <img width="256" height="256" alt="Taskmaster" src="https://maestro-assets.pages.dev/taskmaster-logo.png" />
</p>

<h1 align="center">Taskmaster</h1>

<p align="center">
  A <a href="https://blishhud.com">Blish HUD</a> module: tabbed to-do lists for GW2 recurring content.<br>
  Freeform tabs, real reset schedules, and a reset engine built to never drift.
</p>

![Tabs and tasks](https://maestro-assets.pages.dev/taskmaster-tasks.gif)

## Features

- **Freeform tabs** - organize by activity ("Dailies", "Gold farm", "Alt parking"), not by what the module thinks. Give each tab its own accent color, scroll horizontally when you have a lot of them.
- **Per-task reset schedules** - daily/weekly server reset, map bonus rotation, PSNA, WvW EU/NA, local time, custom cooldown, or never. See [Reset Schedules](#reset-schedules) below.
- **A reset engine that never drifts** - no stored "next reset", no end dates; every check derives boundaries from UTC math, so it can't rot after hibernation, DST, or clock changes.
- **Counters** - "0/5" tasks that need several completions before they're done.
- **Subtasks** - one level of children with their own checkboxes; the parent completes when all of them do, and shares one countdown with the group.
- **Click-to-copy** - attach a waypoint code or chat command to a task, copy it with one click.
- **Notes** - hover tooltip per task.
- **Countdowns + due-soon warning** - see time to reset per task; unchecked tasks turn amber under 1 h.
- **Tab progress badges** - "Dailies 4/7" at a glance.
- **Share tabs** - export/import a whole tab via clipboard JSON, notes and progress stripped out.
- Hide-done toggle, lock mode (checking still works, editing doesn't), unfocused opacity, toggle keybind.

## Tabs

Click **+** to add a tab, drag or scroll the strip when you have more than fit, and right-click a tab for rename, color, duplicate-tab-safe delete, and "delete other tabs".

![Tab management](https://maestro-assets.pages.dev/taskmaster-tabs.gif)

## Tasks & Subtasks

Click **+ Add task**, then the pencil to edit: name, reset schedule, a count target, a clipboard payload, notes, and subtasks. Left-click a checkbox to check or uncheck (both directions); right-click anywhere on a row for its context menu (edit, duplicate, move, move to another tab, delete).

A task with subtasks shows a chevron to expand it; the parent completes exactly when every subtask does, and they all count down together instead of each subtask running its own clock.

![Subtasks](https://maestro-assets.pages.dev/taskmaster-subtasks.gif)

## Reset Schedules

| Schedule | Resets | Anchor |
|---|---|---|
| Never | Never | - |
| Daily server reset | 00:00 UTC | - |
| Weekly server reset | Monday 07:30 UTC | - |
| Map bonus rewards | Thursday 20:00 UTC | - |
| PSNA (Pact Supply Network Agent) | Daily 08:00 UTC | - |
| WvW EU reset | Friday 18:00 UTC | - |
| WvW NA reset | Saturday 02:00 UTC | - |
| Local time | Once a day at a time you set | Your local clock, DST-safe |
| Duration / cooldown | A duration after your last check | Last progress on the task |

Calendar schedules (everything except Local time and Duration) are pure UTC math - they never depend on when you last checked the task, so they can't drift. Duration is the exception: it's a cooldown timer, anchored to your most recent progress on that task.

## Sharing Tabs

Right-click a tab to **Export** its tasks (including subtasks and schedules, but not notes or progress) as JSON to your clipboard. Paste it into another tab's import prompt to bring it in - handy for sharing a checklist with guildmates or moving one between characters/accounts.

![Tab export and import](https://maestro-assets.pages.dev/taskmaster-share.gif)

## Reliability

If resets ever look wrong, open the module settings: it shows the exact UTC times
the engine computes for every boundary, plus what the module thinks "now" is.

![Module settings - reset engine status](https://maestro-assets.pages.dev/taskmaster-settings.png)

Task data lives in `tasks.json` (with automatic `.bak`); a corrupt file is
quarantined, never silently discarded.

## Get Involved

- **Feature requests?** Share them in [Ideas](https://github.com/uwponcel/Taskmaster/discussions/2)
- **Found a bug?** Open an [issue](https://github.com/uwponcel/Taskmaster/issues)

## Support

If you enjoy Taskmaster, consider supporting development:

- [Ko-fi](https://ko-fi.com/aex)
- In-game gold or items: **Aexor.6238**
