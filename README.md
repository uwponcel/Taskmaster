<p align="center">
  <img width="256" height="256" alt="Taskmaster" src="https://maestro-assets.pages.dev/taskmaster-logo.png?v=20260721" />
</p>

<h1 align="center">Taskmaster</h1>

<p align="center">
  A <a href="https://blishhud.com">Blish HUD</a> module: tabbed to-do lists for GW2 recurring content.<br>
  Freeform tabs, real reset schedules, and a reset engine built to never drift.
</p>

![Creating and editing tasks](https://maestro-assets.pages.dev/taskmaster-tasks.gif?v=20260722-colorfix)

## Features

- **Freeform tabs** - organize by activity ("Dailies", "Gold farm", "Alt parking"), not by what the module thinks. Give each tab its own accent color, scroll horizontally when you have a lot of them.
- **Per-task reset schedules** - daily/weekly server reset, map bonus rotation, Pact Supply Network Agent, WvW EU/NA, local time, custom cooldown, or never. See [Reset Schedules](#reset-schedules) below.
- **A reset engine that never drifts** - no stored "next reset", no end dates; every check derives boundaries from UTC math, so it can't rot after hibernation, DST, or clock changes.
- **Counters** - "0/5" tasks that need several completions before they're done.
- **Subtasks** - one level of children with their own checkboxes and clipboard content; the parent completes when all of them do, and shares one countdown with the group.
- **Click-to-copy** - attach a waypoint code or chat command to a task or subtask and copy it with one click.
- **Notes** - hover tooltip per task.
- **Countdowns + due-soon warning** - see time to reset per task; unchecked tasks turn amber under 1 h.
- **Tab progress badges** - "Dailies 4/7" at a glance.
- **Multi-select** - click a task to select it, Shift-click a range, or Ctrl-click individual tasks, then move or delete the selection together.
- **Flexible sizing** - adjust interface and text scale independently for compact or more accessible layouts.
- **Fast ordering** - drag tasks and subtasks into place, or use move up/down/top/bottom actions.
- **Check all** - complete or clear every task and subtask in the active tab from one footer toggle.
- **Built-in presets** - add today's six Pact Supply Network Agent locations to any tab, with individual waypoint buttons and one-click copy-all.
- **Share tabs** - export/import a whole tab via clipboard JSON, notes and progress stripped out.
- Hide-done toggle, lock mode (checking still works, editing doesn't), unfocused opacity, toggle keybind.

## Tabs

Click **+** to add a tab, use the arrows or mouse wheel when you have more than fit, and right-click a tab to rename it in place, change its color, export or import, and manage deletion.

![Tab management](https://maestro-assets.pages.dev/taskmaster-tabs.gif?v=20260722-colorfix)

## Tasks & Subtasks

Click **+ Add task** in the bottom-right, then use the pencil to edit its name, reset schedule, count target, clipboard payload, notes, and subtasks. Each subtask can also carry its own waypoint or chat command. New tasks scroll into view automatically. Left-click a checkbox to check or uncheck it; right-click a task or subtask for ordering and deletion actions.

A task with subtasks shows a chevron to expand it. Expanded subtasks have dedicated copy buttons when clipboard content is configured. The parent completes exactly when every subtask does, and they all count down together instead of each subtask running its own clock.

Drag-and-drop ordering is enabled by default and can be disabled in the module settings. Context menus also provide move up, move down, move to top, and move to bottom actions.

![Tasks and subtasks](https://maestro-assets.pages.dev/taskmaster-subtasks.gif?v=20260722-colorfix)

## Built-in Presets

### Pact Supply Network Agents

Use the preset dropdown beside **+ Add task** to add one **Pact Supply Network Agents** group to the current tab. Expand it to check off all six agents and copy each current map link individually, or use the parent copy button to copy all six links at once.

![Adding the Pact Supply Network Agents preset](https://maestro-assets.pages.dev/taskmaster-presets.gif?v=20260722-colorfix)

Taskmaster calculates the [official seven-day rotation](https://wiki.guildwars2.com/wiki/Pact_Supply_Network_Agent) locally. Locations and progress roll over together at 08:00 UTC, with no account, API key, Wiki request, or stored "next rotation" timestamp.

More built-in presets are coming.

## Multi-select Tasks

Click a task to select it, Shift-click another task to select the visible range between them, or Ctrl-click to toggle individual tasks. Right-click the selection to move every selected task to another tab or delete them together.

![Selecting and managing multiple tasks](https://maestro-assets.pages.dev/taskmaster-selection.gif?v=20260722-colorfix)

## Reset Schedules

| Schedule | Resets | Anchor |
|---|---|---|
| Never | Never | - |
| Daily server reset | 00:00 UTC | - |
| Weekly server reset | Monday 07:30 UTC | - |
| Map bonus rewards | Thursday 20:00 UTC | - |
| Pact Supply Network Agent | Daily 08:00 UTC | - |
| WvW EU reset | Friday 18:00 UTC | - |
| WvW NA reset | Saturday 02:00 UTC | - |
| Local time | Once a day at a time you set | Your local clock, DST-safe |
| Duration / cooldown | A duration after your last check | Last progress on the task |

Calendar schedules (everything except Local time and Duration) are pure UTC math - they never depend on when you last checked the task, so they can't drift. Duration is the exception: it's a cooldown timer, anchored to your most recent progress on that task.

## Sharing Tabs

Right-click a tab to **Export** its tasks (including subtasks and schedules, but not notes or progress) as JSON to your clipboard. Paste it into another tab's import prompt to bring it in - handy for sharing a checklist with guildmates or moving one between characters/accounts.

![Tab export and import](https://maestro-assets.pages.dev/taskmaster-share.gif?v=20260722-colorfix)

## Module Settings

Adjust interface and text scale independently for a compact layout or larger, more accessible controls. You can also configure unfocused opacity, the show/hide keybind, map visibility, and whether task and subtask drag-and-drop ordering is enabled.

![Taskmaster module settings](https://maestro-assets.pages.dev/taskmaster-settings.png?v=20260722-v21)

## Reliability

If resets ever look wrong, open the module settings: it shows the exact UTC times
the engine computes for every boundary, plus what the module thinks "now" is.

Task data lives in `tasks.json` (with automatic `.bak`); a corrupt file is
quarantined, never silently discarded.

## Get Involved

- **Feature requests?** Share them in [Ideas](https://github.com/uwponcel/Taskmaster/discussions/2)
- **Found a bug?** Open an [issue](https://github.com/uwponcel/Taskmaster/issues)

## Support

If you enjoy Taskmaster, consider supporting development:

- [Ko-fi](https://ko-fi.com/aex)
- In-game gold or items: **Aexor.6238**
