namespace MasterBlaster.Rdp;

using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowExW(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    internal const uint PW_RENDERFULLCONTENT = 0x00000002;

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
