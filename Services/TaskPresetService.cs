using System;
using System.Collections.Generic;
using System.Linq;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    public static class TaskPresetService
    {
        public const string PsnaName = "Pact Supply Network Agents";

        public static TodoTask CreatePsna(int order)
        {
            var parent = new TodoTask
            {
                Name = PsnaName,
                Order = order,
                Schedule = ResetScheduleType.Psna,
                PresetType = TaskPresetType.PactSupplyNetworkAgents,
                Notes = "Locations rotate daily at 08:00 UTC."
            };

            int childOrder = 0;
            foreach (var slot in PsnaRotation.Slots)
            {
                parent.Subtasks.Add(new TodoTask
                {
                    Name = slot.ToString(),
                    Order = childOrder++,
                    Schedule = ResetScheduleType.Psna,
                    PresetType = TaskPresetType.PactSupplyNetworkAgents,
                    PresetSlot = slot
                });
            }

            return parent;
        }

        public static string ResolveName(TodoTask task, DateTime nowUtc)
        {
            if (task == null) return "";
            if (task.PresetType != TaskPresetType.PactSupplyNetworkAgents)
                return task.Name;
            if (task.PresetSlot == TaskPresetSlot.None)
                return PsnaName;

            var location = PsnaRotation.GetLocation(task.PresetSlot, nowUtc);
            return location == null
                ? task.Name
                : $"{location.Region}: {location.Location}";
        }

        public static string ResolveClipboardContent(TodoTask task, DateTime nowUtc)
        {
            if (task == null) return null;
            if (task.PresetType != TaskPresetType.PactSupplyNetworkAgents)
                return task.ClipboardContent;

            if (task.PresetSlot != TaskPresetSlot.None)
                return PsnaRotation.GetLocation(task.PresetSlot, nowUtc)?.MapLink;

            return string.Join(
                " ",
                PsnaRotation.Slots
                    .Select(slot => PsnaRotation.GetLocation(slot, nowUtc)?.MapLink)
                    .Where(link => !string.IsNullOrEmpty(link)));
        }

        public static bool ContainsPreset(TodoTab tab, TaskPresetType presetType) =>
            tab?.Tasks.Any(task =>
                task.PresetType == presetType &&
                task.PresetSlot == TaskPresetSlot.None) == true;

        public static bool CanMoveTo(
            IEnumerable<TodoTask> tasks,
            TodoTab destination)
        {
            if (destination == null) return false;
            var presetTypes = (tasks ?? Enumerable.Empty<TodoTask>())
                .Where(task => task.IsManagedPresetParent)
                .Select(task => task.PresetType)
                .Distinct();
            return presetTypes.All(type => !ContainsPreset(destination, type));
        }

        public static bool ValidateTab(TodoTab tab)
        {
            if (tab?.Tasks == null) return false;

            var presetTypes = new HashSet<TaskPresetType>();
            foreach (var task in tab.Tasks)
            {
                if (task == null) return false;
                if (task.IsManagedPresetParent)
                {
                    if (!presetTypes.Add(task.PresetType) ||
                        !ValidatePreset(task))
                        return false;
                }
                else if (!ValidateOrdinaryTask(task))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateOrdinaryTask(TodoTask task)
        {
            if (task == null ||
                task.PresetType != TaskPresetType.None ||
                task.PresetSlot != TaskPresetSlot.None)
                return false;
            return task.Subtasks == null ||
                   task.Subtasks.All(ValidateOrdinaryTask);
        }

        private static bool ValidatePreset(TodoTask parent)
        {
            if (parent.PresetType != TaskPresetType.PactSupplyNetworkAgents ||
                parent.PresetSlot != TaskPresetSlot.None ||
                parent.Schedule != ResetScheduleType.Psna ||
                parent.TargetCount != 1 ||
                parent.LocalResetTime.HasValue ||
                parent.ResetDuration.HasValue ||
                parent.Subtasks == null ||
                parent.Subtasks.Count != PsnaRotation.Slots.Count ||
                parent.Subtasks.Any(child => child == null))
                return false;

            var children = parent.Subtasks
                .OrderBy(child => child.Order)
                .ToList();
            for (int index = 0; index < children.Count; index++)
            {
                var child = children[index];
                if (child.PresetType != parent.PresetType ||
                    child.PresetSlot != PsnaRotation.Slots[index] ||
                    child.Schedule != ResetScheduleType.Psna ||
                    child.TargetCount != 1 ||
                    child.LocalResetTime.HasValue ||
                    child.ResetDuration.HasValue ||
                    child.HasSubtasks ||
                    child.Order != index)
                    return false;
            }

            return true;
        }
    }
}
