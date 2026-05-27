using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MagicSearch.Services
{
    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9000;
        private const int WmHotkey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint VkSpace = 0x20;

        private HwndSource? _source;
        private IntPtr _handle;

        public event EventHandler? HotkeyPressed;

        public bool Register(Window window)
        {
            _handle = new WindowInteropHelper(window).EnsureHandle();
            _source = HwndSource.FromHwnd(_handle);
            _source?.AddHook(WndProc);
            return RegisterHotKey(_handle, HotkeyId, ModControl, VkSpace);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                UnregisterHotKey(_handle, HotkeyId);
            }

            _source?.RemoveHook(WndProc);
            _source = null;
            _handle = IntPtr.Zero;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
