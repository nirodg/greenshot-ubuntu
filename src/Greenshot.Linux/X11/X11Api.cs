using System.Runtime.InteropServices;

namespace Greenshot.Linux.X11;

internal static class X11Api
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";

    [DllImport(LibX11)] public static extern nint XOpenDisplay(string? display);
    [DllImport(LibX11)] public static extern int XCloseDisplay(nint display);
    [DllImport(LibX11)] public static extern nint XDefaultRootWindow(nint display);
    [DllImport(LibX11)] public static extern int XDefaultScreen(nint display);
    [DllImport(LibX11)] public static extern int XDisplayWidth(nint display, int screen);
    [DllImport(LibX11)] public static extern int XDisplayHeight(nint display, int screen);
    [DllImport(LibX11)] public static extern int XFree(nint data);
    [DllImport(LibX11)] public static extern int XFlush(nint display);
    [DllImport(LibX11)] public static extern int XSync(nint display, bool discard);

    [DllImport(LibX11)]
    public static extern unsafe XImage* XGetImage(nint display, nint drawable, int x, int y,
        uint width, uint height, ulong planeMask, int format);

    [DllImport(LibX11)]
    public static extern unsafe int XDestroyImage(XImage* image);

    [DllImport(LibX11)]
    public static extern bool XQueryPointer(nint display, nint w,
        out nint root, out nint child,
        out int rootX, out int rootY,
        out int winX, out int winY,
        out uint mask);

    [DllImport(LibX11)]
    public static extern int XGrabKey(nint display, int keycode, uint modifiers,
        nint grab_window, bool owner_events, int pointer_mode, int keyboard_mode);

    [DllImport(LibX11)]
    public static extern int XUngrabKey(nint display, int keycode, uint modifiers, nint grab_window);

    [DllImport(LibX11)]
    public static extern int XKeysymToKeycode(nint display, ulong keysym);

    [DllImport(LibX11)]
    public static extern int XNextEvent(nint display, out XEvent evt);

    [DllImport(LibX11)]
    public static extern int XPending(nint display);

    [DllImport(LibX11)]
    public static extern int XSelectInput(nint display, nint window, long event_mask);

    [DllImport(LibX11)]
    public static extern nint XRootWindow(nint display, int screen_number);

    [DllImport(LibX11)]
    public static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attrs);

    [DllImport(LibX11)]
    public static extern nint XGetRootWindow(nint display, int screen);

    // X11 constants
    public const int ZPixmap = 2;
    public const ulong AllPlanes = 0xFFFFFFFFFFFFFFFF;

    public const int KeyPress = 2;
    public const int KeyRelease = 3;

    public const uint GrabModeSync = 0;
    public const uint GrabModeAsync = 1;

    // Key modifiers
    public const uint ShiftMask   = 1 << 0;
    public const uint LockMask    = 1 << 1;
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask    = 1 << 3; // Alt
    public const uint Mod4Mask    = 1 << 6; // Super/Windows
    public const uint AnyModifier = 1 << 15;

    // Common keysyms
    public const ulong XK_Print       = 0xFF61;
    public const ulong XK_F1          = 0xFFBE;
    public const ulong XK_a           = 0x0061;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XImage
{
    public int width;
    public int height;
    public int xoffset;
    public int format;
    public byte* data;
    public int byte_order;
    public int bitmap_unit;
    public int bitmap_bit_order;
    public int bitmap_pad;
    public int depth;
    public int bytes_per_line;
    public int bits_per_pixel;
    public ulong red_mask;
    public ulong green_mask;
    public ulong blue_mask;
    // funcs pointer - opaque
    public nint f;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XEvent
{
    public int type;
    public ulong serial;
    public bool send_event;
    public nint display;
    public nint window;
    // Padding for union (largest member is ~96 bytes)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public nint[] pad;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XKeyEvent
{
    public int type;
    public ulong serial;
    public bool send_event;
    public nint display;
    public nint window;
    public nint root;
    public nint subwindow;
    public ulong time;
    public int x, y;
    public int x_root, y_root;
    public uint state;
    public uint keycode;
    public bool same_screen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XWindowAttributes
{
    public int x, y;
    public int width, height;
    public int border_width;
    public int depth;
    // ... more fields
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
    public nint[] pad;
}
