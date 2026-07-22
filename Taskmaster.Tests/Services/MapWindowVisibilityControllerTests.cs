using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class MapWindowVisibilityControllerTests
    {
        [Fact]
        public void Update_WindowWasVisible_RestoresItAfterMapCloses()
        {
            var controller = new MapWindowVisibilityController();

            Assert.Equal(
                WindowVisibilityAction.Hide,
                controller.Update(isVisible: true, hiddenByMap: true));
            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Update(isVisible: false, hiddenByMap: true));
            Assert.Equal(
                WindowVisibilityAction.Show,
                controller.Update(isVisible: false, hiddenByMap: false));
        }

        [Fact]
        public void Update_WindowWasAlreadyClosed_DoesNotOpenItAfterMapCloses()
        {
            var controller = new MapWindowVisibilityController();

            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Update(isVisible: false, hiddenByMap: true));
            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Update(isVisible: false, hiddenByMap: false));
        }

        [Fact]
        public void Toggle_WhileTemporarilyHidden_CancelsRestore()
        {
            var controller = new MapWindowVisibilityController();
            controller.Update(isVisible: true, hiddenByMap: true);

            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Toggle(isVisible: false, hiddenByMap: true));
            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Update(isVisible: false, hiddenByMap: false));
        }

        [Fact]
        public void Toggle_TwiceWhileMapIsOpen_RearmsRestore()
        {
            var controller = new MapWindowVisibilityController();
            controller.Update(isVisible: true, hiddenByMap: true);
            controller.Toggle(isVisible: false, hiddenByMap: true);

            Assert.Equal(
                WindowVisibilityAction.None,
                controller.Toggle(isVisible: false, hiddenByMap: true));
            Assert.Equal(
                WindowVisibilityAction.Show,
                controller.Update(isVisible: false, hiddenByMap: false));
        }

        [Fact]
        public void Update_EnablingShowOnMap_RestoresWindowImmediately()
        {
            var controller = new MapWindowVisibilityController();
            controller.Update(isVisible: true, hiddenByMap: true);

            Assert.Equal(
                WindowVisibilityAction.Show,
                controller.Update(isVisible: false, hiddenByMap: false));
        }
    }
}
