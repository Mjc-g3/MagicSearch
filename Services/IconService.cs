using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagicSearch.Services
{
    public static class IconService
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiSmallIcon = 0x000000001;

        public static ImageSource? GetSmallIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                return null;
            }

            var info = new ShFileInfo();
            var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(16, 16));

                source.Freeze();
                return source;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref ShFileInfo psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
    }
}
