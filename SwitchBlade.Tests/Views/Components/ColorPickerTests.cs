using System.Threading;
using System.Windows.Media;
using SwitchBlade.Views.Components;
using Xunit;

namespace SwitchBlade.Tests.Views.Components
{
    public class ColorPickerTests
    {
        private void RunOnSTA(System.Action action)
        {
            System.Exception? ex = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (System.Exception e)
                {
                    ex = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (ex != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }

        [Fact]
        public void SettingRGBA_UpdatesHexAndPreview()
        {
            RunOnSTA(() =>
            {
                var picker = new ColorPicker();

                // Act
                picker.A = 255;
                picker.R = 255;
                picker.G = 0;
                picker.B = 0;

                // Assert
                Assert.Equal("#FFFF0000", picker.HexColor);
                Assert.Equal("#FFFF0000", picker.SelectedColor);
                Assert.Equal(Colors.Red, picker.PreviewColor);
            });
        }

        [Fact]
        public void SettingHexColor_UpdatesRGBAAndPreview()
        {
            RunOnSTA(() =>
            {
                var picker = new ColorPicker();

                // Act
                picker.HexColor = "#FF00FF00";

                // Assert
                Assert.Equal(255, picker.A);
                Assert.Equal(0, picker.R);
                Assert.Equal(255, picker.G);
                Assert.Equal(0, picker.B);
                Assert.Equal("#FF00FF00", picker.SelectedColor);
                Assert.Equal(Colors.Lime, picker.PreviewColor);
            });
        }

        [Fact]
        public void SettingSelectedColor_UpdatesHexAndRGBAAndPreview()
        {
            RunOnSTA(() =>
            {
                var picker = new ColorPicker();

                // Act
                picker.SelectedColor = "#FF0000FF";

                // Assert
                Assert.Equal(255, picker.A);
                Assert.Equal(0, picker.R);
                Assert.Equal(0, picker.G);
                Assert.Equal(255, picker.B);
                Assert.Equal("#FF0000FF", picker.HexColor);
                Assert.Equal(Colors.Blue, picker.PreviewColor);
            });
        }

        [Fact]
        public void SettingInvalidHexColor_ShouldNotUpdateInternalState()
        {
            RunOnSTA(() =>
            {
                var picker = new ColorPicker();
                picker.HexColor = "#FF00FF00"; // Start with green

                // Act
                picker.HexColor = "invalid";

                // Assert
                Assert.Equal(255, picker.A);
                Assert.Equal(0, picker.R);
                Assert.Equal(255, picker.G);
                Assert.Equal(0, picker.B);
                Assert.Equal("#FF00FF00", picker.SelectedColor); 
                Assert.Equal(Colors.Lime, picker.PreviewColor);
            });
        }

        [Fact]
        public void SettingInvalidSelectedColor_ShouldNotUpdateInternalState()
        {
            RunOnSTA(() =>
            {
                var picker = new ColorPicker();
                picker.SelectedColor = "#FF00FF00"; // Start with green

                // Act
                picker.SelectedColor = null!;

                // Assert
                Assert.Equal(255, picker.A);
                Assert.Equal(0, picker.R);
                Assert.Equal(255, picker.G);
                Assert.Equal(0, picker.B);
                Assert.Equal("#FF00FF00", picker.HexColor);
                Assert.Equal(Colors.Lime, picker.PreviewColor);
            });
        }
    }
}
