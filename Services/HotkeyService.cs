using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MagicSearch.Services
{
    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9000;
        private const int WmHotkey = 0x0312;

        private HwndSource? _source;
        private IntPtr _handle;
        private bool _isRegistered;

        public event EventHandler? HotkeyPressed;

        public bool Register(Window window, uint modifiers, uint key)
        {
            _handle = new WindowInteropHelper(window).EnsureHandle();
            _source ??= HwndSource.FromHwnd(_handle);
            _source?.RemoveHook(WndProc);
            _source?.AddHook(WndProc);

            Unregister();

            _isRegistered = RegisterHotKey(_handle, HotkeyId, modifiers, key);
            return _isRegistered;
        }

        public void Unregister()
        {
            if (_handle != IntPtr.Zero && _isRegistered)
            {
                UnregisterHotKey(_handle, HotkeyId);
                _isRegistered = false;
            }
        }

        public void Dispose()
        {
            Unregister();
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

        public bool TryChangeHotkey(Window window, uint modifiers, uint key)
        {
            var oldHandle = _handle;
            var wasRegistered = _isRegistered;

            if (_handle == IntPtr.Zero)
            {
                _handle = new WindowInteropHelper(window).EnsureHandle();
                _source ??= HwndSource.FromHwnd(_handle);
                _source?.RemoveHook(WndProc);
                _source?.AddHook(WndProc);
            }

            if (_isRegistered)
            {
                UnregisterHotKey(_handle, HotkeyId);
                _isRegistered = false;
            }

            var success = RegisterHotKey(_handle, HotkeyId, modifiers, key);

            if (success)
            {
                _isRegistered = true;
                return true;
            }

            if (wasRegistered && oldHandle != IntPtr.Zero)
            {
                RegisterHotKey(oldHandle, HotkeyId, 0x0002, 0x20);
                _handle = oldHandle;
                _isRegistered = true;
            }

            return false;
        }
    }
}