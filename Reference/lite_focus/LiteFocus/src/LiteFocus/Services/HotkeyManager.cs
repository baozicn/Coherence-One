using LiteFocus.Interop;
using System;
using System.Windows;
using System.Windows.Interop;

namespace LiteFocus.Services
{
    public class HotkeyManager : IDisposable
    {
        private readonly Window _window;
        private HwndSource? _source;
        private const int HOTKEY_ID_CLEAR = 1; // Ctrl+Alt+Space

        public HotkeyManager(Window window)
        {
            _window = window;
            _window.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(_window);
                _source = HwndSource.FromHwnd(helper.Handle);
                _source.AddHook(HwndHook);
            };
        }

        public void RegisterClearScreenHotkey()
        {
            var helper = new WindowInteropHelper(_window);
            // VK_SPACE = 0x20
            NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID_CLEAR, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x20);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_CLEAR)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var mw = (LiteFocus.MainWindow)System.Windows.Application.Current.MainWindow;
                        mw?.GetType().GetMethod("ClearScreenNow_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                           ?.Invoke(mw, new object?[] { null!, null! });
                    });
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            try
            {
                var helper = new WindowInteropHelper(_window);
                NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID_CLEAR);
            }
            catch { }
        }
    }
}
