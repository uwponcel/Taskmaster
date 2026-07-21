using System;
using System.Collections.Generic;
using Taskmaster.UI;
using Xunit;

namespace Taskmaster.Tests.UI
{
    public class TaskSelectionTests
    {
        [Fact]
        public void Select_PlainClick_ReplacesSelection()
        {
            var ids = CreateIds(3);
            var selection = new TaskSelection();

            selection.Select(ids[0], ids, false, false);
            selection.Select(ids[2], ids, false, false);

            Assert.Single(selection.SelectedIds);
            Assert.Contains(ids[2], selection.SelectedIds);
        }

        [Fact]
        public void Select_ShiftClick_SelectsRangeFromAnchor()
        {
            var ids = CreateIds(5);
            var selection = new TaskSelection();

            selection.Select(ids[1], ids, false, false);
            selection.Select(ids[4], ids, true, false);

            Assert.Equal(4, selection.SelectedIds.Count);
            for (int i = 1; i <= 4; i++) Assert.Contains(ids[i], selection.SelectedIds);
        }

        [Fact]
        public void Select_CtrlClick_TogglesIndividualTask()
        {
            var ids = CreateIds(3);
            var selection = new TaskSelection();

            selection.Select(ids[0], ids, false, false);
            selection.Select(ids[2], ids, false, true);
            selection.Select(ids[0], ids, false, true);

            Assert.Single(selection.SelectedIds);
            Assert.Contains(ids[2], selection.SelectedIds);
        }

        [Fact]
        public void SelectForContext_PreservesGroupOnlyForSelectedTask()
        {
            var ids = CreateIds(4);
            var selection = new TaskSelection();
            selection.Select(ids[0], ids, false, false);
            selection.Select(ids[2], ids, false, true);

            selection.SelectForContext(ids[2]);
            Assert.Equal(2, selection.SelectedIds.Count);

            selection.SelectForContext(ids[3]);
            Assert.Single(selection.SelectedIds);
            Assert.Contains(ids[3], selection.SelectedIds);
        }

        [Fact]
        public void Retain_RemovesTasksThatNoLongerExist()
        {
            var ids = CreateIds(3);
            var selection = new TaskSelection();
            selection.Select(ids[0], ids, false, false);
            selection.Select(ids[2], ids, false, true);

            selection.Retain(new[] { ids[2] });

            Assert.Single(selection.SelectedIds);
            Assert.Contains(ids[2], selection.SelectedIds);
        }

        private static IReadOnlyList<Guid> CreateIds(int count)
        {
            var ids = new List<Guid>();
            for (int i = 0; i < count; i++) ids.Add(Guid.NewGuid());
            return ids;
        }
    }
}
