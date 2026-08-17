using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SoundPadZ.Services;

public sealed class HotkeyService : IDisposable
{
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_WIN = 0x8;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private sealed record HotkeyEntry(int Id, uint Mods, uint Vk, Action Action);

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly Dictionary<int, HotkeyEntry> _entries = new();
    private readonly HashSet<uint> _pressedKeys = new();

    public HotkeyService(Window? window = null)
    {
        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk = kb.vkCode;
            uint normalizedVk = NormalizeVk(vk);

            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                if (_pressedKeys.Add(vk))
                {
                    uint currentMods = GetCurrentModifiers();

                    foreach (var entry in _entries.Values.ToArray())
                    {
                        if (Matches(entry, vk, normalizedVk, currentMods))
                        {
                            try
                            {
                                entry.Action();
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }
            }
            else if (msg is WM_KEYUP or WM_SYSKEYUP)
            {
                _pressedKeys.Remove(vk);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public static uint NormalizeVk(uint vk)
    {
        return vk switch
        {
            0xA4 or 0xA5 => 0x12, // VK_LMENU / VK_RMENU -> VK_MENU (Alt)
            0xA2 or 0xA3 => 0x11, // VK_LCONTROL / VK_RCONTROL -> VK_CONTROL (Ctrl)
            0xA0 or 0xA1 => 0x10, // VK_LSHIFT / VK_RSHIFT -> VK_SHIFT (Shift)
            0x5B or 0x5C => 0x5B, // VK_LWIN / VK_RWIN -> VK_LWIN (Win)
            _ => vk
        };
    }

    private static uint GetCurrentModifiers()
    {
        uint mods = 0;
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) mods |= MOD_CONTROL;
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) mods |= MOD_ALT;
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) mods |= MOD_SHIFT;
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) mods |= MOD_WIN;
        return mods;
    }

    private static bool Matches(HotkeyEntry entry, uint rawVk, uint normalizedVk, uint currentMods)
    {
        bool keyMatches = entry.Vk == rawVk || entry.Vk == normalizedVk || NormalizeVk(entry.Vk) == normalizedVk;
        if (!keyMatches)
        {
            return false;
        }

        uint effectiveCurrentMods = currentMods;
        if (normalizedVk == 0x12) effectiveCurrentMods &= ~MOD_ALT;
        if (normalizedVk == 0x11) effectiveCurrentMods &= ~MOD_CONTROL;
        if (normalizedVk == 0x10) effectiveCurrentMods &= ~MOD_SHIFT;
        if (normalizedVk == 0x5B) effectiveCurrentMods &= ~MOD_WIN;

        return effectiveCurrentMods == entry.Mods;
    }

    public bool Register(int id, uint modifiers, uint vk, Action action)
    {
        Unregister(id);
        if (vk == 0) return false;
        _entries[id] = new HotkeyEntry(id, modifiers, vk, action);
        return true;
    }

    public void Unregister(int id)
    {
        _entries.Remove(id);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _entries.Clear();
        _pressedKeys.Clear();
    }

    public static string ComboText(uint modifiers, uint vk)
    {
        if (vk == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        var normalized = NormalizeVk(vk);

        if ((modifiers & MOD_CONTROL) != 0 && normalized != 0x11) sb.Append("Ctrl+");
        if ((modifiers & MOD_ALT) != 0 && normalized != 0x12) sb.Append("Alt+");
        if ((modifiers & MOD_SHIFT) != 0 && normalized != 0x10) sb.Append("Shift+");
        if ((modifiers & MOD_WIN) != 0 && normalized != 0x5B) sb.Append("Win+");

        sb.Append(FormatKeyName(vk));
        return sb.ToString();
    }

    public static string FormatKeyName(uint vk)
    {
        return vk switch
        {
            0x12 or 0xA4 => "Alt",
            0xA5 => "RAlt",
            0x11 or 0xA2 => "Ctrl",
            0xA3 => "RCtrl",
            0x10 or 0xA0 => "Shift",
            0xA1 => "RShift",
            0x5B => "Win",
            0x5C => "RWin",
            0x20 => "Space",
            0x09 => "Tab",
            0x14 => "Caps Lock",
            0x1B => "Esc",
            0x08 => "Backspace",
            0x0D => "Enter",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x90 => "Num Lock",
            0x91 => "Scroll Lock",
            0x13 => "Pause",
            0x2C => "PrintScreen",
            0xC0 => "~",
            0xBD => "-",
            0xBB => "=",
            0xDB => "[",
            0xDD => "]",
            0xDC => "\\",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",
            >= 0x60 and <= 0x69 => $"Num {vk - 0x60}",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            _ => KeyInterop.KeyFromVirtualKey((int)vk).ToString()
        };
    }
}
