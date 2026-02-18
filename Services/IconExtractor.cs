using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
    public class IconExtractor : IIconExtractor
    {
        public ImageSource? ExtractIcon(string executablePath)
        {
            IntPtr[]? smallIcons = null;
            try
            {
                // Extract one small icon from the executable
                smallIcons = new IntPtr[1];
                var largeIcons = new IntPtr[1];

                uint count = NativeInterop.ExtractIconEx(executablePath, 0, largeIcons, smallIcons, 1);

                if (count == 0 || smallIcons[0] == IntPtr.Zero)
                {
                    // Try large icon as fallback
                    if (largeIcons[0] != IntPtr.Zero)
                    {
                        var largeBitmapSource = CreateBitmapSourceFromHIcon(largeIcons[0]);
                        NativeInterop.DestroyIcon(largeIcons[0]);
                        return largeBitmapSource;
                    }
                    return null;
                }

                // Convert HICON to BitmapSource
                var bitmapSource = CreateBitmapSourceFromHIcon(smallIcons[0]);

                // Clean up large icon if extracted
                if (largeIcons[0] != IntPtr.Zero)
                {
                    NativeInterop.DestroyIcon(largeIcons[0]);
                }

                return bitmapSource;
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Failed to extract icon from '{executablePath}'", ex);
                return null;
            }
            finally
            {
                // Always clean up the small icon handle
                if (smallIcons != null && smallIcons[0] != IntPtr.Zero)
                {
                    NativeInterop.DestroyIcon(smallIcons[0]);
                }
            }
        }

        private static BitmapSource? CreateBitmapSourceFromHIcon(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero)
                return null;

            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Freeze for cross-thread access and performance
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }
    }
}
