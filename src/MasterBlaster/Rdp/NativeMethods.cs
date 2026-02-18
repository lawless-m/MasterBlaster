namespace MasterBlaster.Rdp;

using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    internal static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("user32.dll")]
    internal static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    internal const uint SRCCOPY = 0x00CC0020;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    internal static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowExW(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    internal const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint type;
        internal INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] internal MOUSEINPUT mi;
        [FieldOffset(0)] internal KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int dx;
        internal int dy;
        internal uint mouseData;
        internal uint dwFlags;
        internal uint time;
        internal IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort wVk;
        internal ushort wScan;
        internal uint dwFlags;
        internal uint time;
        internal IntPtr dwExtraInfo;
    }

    internal static void SendUnicodeChar(char ch)
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wScan = (ushort)ch;
        inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wScan = (ushort)ch;
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    internal static void SendVkDown(int vk)
    {
        var input = new INPUT[1];
        input[0].type = INPUT_KEYBOARD;
        input[0].u.ki.wVk = (ushort)vk;
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }

    internal static void SendVkUp(int vk)
    {
        var input = new INPUT[1];
        input[0].type = INPUT_KEYBOARD;
        input[0].u.ki.wVk = (ushort)vk;
        input[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }

    internal const uint WM_KEYDOWN = 0x0100;
    internal const uint WM_KEYUP = 0x0101;
    internal const uint WM_CHAR = 0x0102;
    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_LBUTTONDBLCLK = 0x0203;
    internal const uint WM_RBUTTONDOWN = 0x0204;
    internal const uint WM_RBUTTONUP = 0x0205;

    internal const int MK_LBUTTON = 0x0001;
    internal const int MK_RBUTTON = 0x0002;
    internal const uint MAPVK_VK_TO_VSC = 0;

    internal static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

    internal static IntPtr MakeKeyLParam(uint scanCode, bool extended, bool keyUp)
    {
        int lParam = (int)((scanCode << 16) | 0x0001);
        if (extended) lParam |= 1 << 24;
        if (keyUp)
        {
            lParam |= 1 << 30;
            lParam |= unchecked((int)(1u << 31));
        }
        return (IntPtr)lParam;
    }

    /// <summary>
    /// Walk the child window hierarchy to find the deepest (leaf) child,
    /// which is the RDP input surface window.
    /// </summary>
    internal static IntPtr FindDeepestChild(IntPtr parent)
    {
        var current = parent;
        while (true)
        {
            var child = FindWindowExW(current, IntPtr.Zero, null, null);
            if (child == IntPtr.Zero)
                return current;
            current = child;
        }
    }

    private static readonly Dictionary<string, (int vk, bool extended)> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = (0x11, false), ["control"] = (0x11, false),
        ["alt"] = (0x12, false), ["menu"] = (0x12, false),
        ["shift"] = (0x10, false),
        ["win"] = (0x5B, true), ["lwin"] = (0x5B, true),
        ["tab"] = (0x09, false),
        ["enter"] = (0x0D, false), ["return"] = (0x0D, false),
        ["esc"] = (0x1B, false), ["escape"] = (0x1B, false),
        ["backspace"] = (0x08, false), ["back"] = (0x08, false),
        ["delete"] = (0x2E, true), ["del"] = (0x2E, true),
        ["insert"] = (0x2D, true), ["ins"] = (0x2D, true),
        ["home"] = (0x24, true), ["end"] = (0x23, true),
        ["pageup"] = (0x21, true), ["pgup"] = (0x21, true),
        ["pagedown"] = (0x22, true), ["pgdn"] = (0x22, true),
        ["up"] = (0x26, true), ["down"] = (0x28, true),
        ["left"] = (0x25, true), ["right"] = (0x27, true),
        ["space"] = (0x20, false),
        ["f1"] = (0x70, false), ["f2"] = (0x71, false), ["f3"] = (0x72, false),
        ["f4"] = (0x73, false), ["f5"] = (0x74, false), ["f6"] = (0x75, false),
        ["f7"] = (0x76, false), ["f8"] = (0x77, false), ["f9"] = (0x78, false),
        ["f10"] = (0x79, false), ["f11"] = (0x7A, false), ["f12"] = (0x7B, false),
    };

    internal static (int vk, bool extended) ParseKeyName(string name)
    {
        if (KeyMap.TryGetValue(name, out var entry))
            return entry;

        // Single character — use its uppercase ASCII as the VK code
        if (name.Length == 1)
        {
            var ch = char.ToUpperInvariant(name[0]);
            return (ch, false);
        }

        throw new ArgumentException($"Unknown key name: '{name}'");
    }

    internal static void SendKeyDown(IntPtr hwnd, int vk, bool extended)
    {
        uint scan = MapVirtualKeyW((uint)vk, MAPVK_VK_TO_VSC);
        SendMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, MakeKeyLParam(scan, extended, false));
    }

    internal static void SendKeyUp(IntPtr hwnd, int vk, bool extended)
    {
        uint scan = MapVirtualKeyW((uint)vk, MAPVK_VK_TO_VSC);
        SendMessage(hwnd, WM_KEYUP, (IntPtr)vk, MakeKeyLParam(scan, extended, true));
    }
}
