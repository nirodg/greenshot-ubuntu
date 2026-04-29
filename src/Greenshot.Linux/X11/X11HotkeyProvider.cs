using System.Runtime.InteropServices;
using Greenshot.Base.Platform;
using Microsoft.Extensions.Logging;

namespace Greenshot.Linux.X11;

public class X11HotkeyProvider : IHotkeyProvider
{
    private readonly ILogger<X11HotkeyProvider> _logger;
    private readonly nint _display;
    private readonly nint _root;
    private readonly Dictionary<string, (int keycode, uint modifiers, Action callback)> _hotkeys = new();
    private Thread? _eventThread;
    private volatile bool _running;

    public bool IsAvailable => _display != nint.Zero;

    private static readonly Dictionary<string, ulong> KeysymMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Print"]     = X11Api.XK_Print,
        ["PrintScreen"] = X11Api.XK_Print,
        ["F1"]        = X11Api.XK_F1,
        ["F2"]        = X11Api.XK_F1 + 1,
        ["F3"]        = X11Api.XK_F1 + 2,
        ["F4"]        = X11Api.XK_F1 + 3,
        ["F5"]        = X11Api.XK_F1 + 4,
        ["F6"]        = X11Api.XK_F1 + 5,
        ["F7"]        = X11Api.XK_F1 + 6,
        ["F8"]        = X11Api.XK_F1 + 7,
        ["F9"]        = X11Api.XK_F1 + 8,
        ["F10"]       = X11Api.XK_F1 + 9,
        ["F11"]       = X11Api.XK_F1 + 10,
        ["F12"]       = X11Api.XK_F1 + 11,
    };

    public X11HotkeyProvider(ILogger<X11HotkeyProvider> logger)
    {
        _logger = logger;
        try
        {
            _display = X11Api.XOpenDisplay(null);
            if (_display == nint.Zero)
            {
                _logger.LogWarning("Cannot open X11 display for hotkeys");
                return;
            }
            _root = X11Api.XDefaultRootWindow(_display);
            StartEventLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize X11 hotkeys");
        }
    }

    public bool RegisterHotkey(string id, string hotkeyString, Action callback)
    {
        if (!IsAvailable) return false;

        var (keycode, modifiers) = ParseHotkey(hotkeyString);
        if (keycode == 0)
        {
            _logger.LogWarning("Cannot parse hotkey: {Hotkey}", hotkeyString);
            return false;
        }

        UnregisterHotkey(id);

        // Register with all lock key combinations (NumLock, CapsLock, ScrollLock)
        foreach (var extraMod in GetLockCombinations())
        {
            X11Api.XGrabKey(_display, keycode, modifiers | extraMod, _root, false,
                (int)X11Api.GrabModeAsync, (int)X11Api.GrabModeAsync);
        }

        _hotkeys[id] = (keycode, modifiers, callback);
        X11Api.XFlush(_display);
        _logger.LogDebug("Registered hotkey {Id}: {Hotkey} (keycode={Keycode}, mods={Mods})",
            id, hotkeyString, keycode, modifiers);
        return true;
    }

    public void UnregisterHotkey(string id)
    {
        if (!_hotkeys.TryGetValue(id, out var entry)) return;
        foreach (var extraMod in GetLockCombinations())
            X11Api.XUngrabKey(_display, entry.keycode, entry.modifiers | extraMod, _root);
        _hotkeys.Remove(id);
        X11Api.XFlush(_display);
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
            UnregisterHotkey(id);
    }

    private (int keycode, uint modifiers) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        string keyPart = hotkey;

        var parts = hotkey.Split('+').Select(p => p.Trim()).ToArray();
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLower() switch
            {
                "ctrl" or "control" => X11Api.ControlMask,
                "alt" => X11Api.Mod1Mask,
                "shift" => X11Api.ShiftMask,
                "super" or "windows" => X11Api.Mod4Mask,
                _ => 0u
            };
        }
        keyPart = parts[^1];

        if (!KeysymMap.TryGetValue(keyPart, out ulong keysym))
        {
            if (keyPart.Length == 1)
                keysym = (ulong)char.ToLower(keyPart[0]);
            else
                return (0, 0);
        }

        int keycode = X11Api.XKeysymToKeycode(_display, keysym);
        return (keycode, modifiers);
    }

    private static IEnumerable<uint> GetLockCombinations()
    {
        // Register for all combos of NumLock (Mod2), CapsLock (LockMask), ScrollLock (Mod5)
        for (uint i = 0; i < 8; i++)
        {
            uint mask = 0;
            if ((i & 1) != 0) mask |= X11Api.LockMask;   // CapsLock
            if ((i & 2) != 0) mask |= 0x10;               // Mod2 (NumLock)
            if ((i & 4) != 0) mask |= 0x80;               // Mod5 (ScrollLock)
            yield return mask;
        }
    }

    private void StartEventLoop()
    {
        _running = true;
        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "X11HotkeyEventLoop" };
        _eventThread.Start();
    }

    private void EventLoop()
    {
        X11Api.XSelectInput(_display, _root, 0); // KeyPressMask added via XGrabKey

        while (_running)
        {
            if (X11Api.XPending(_display) == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            X11Api.XNextEvent(_display, out var evt);

            if (evt.type != X11Api.KeyPress) continue;

            // Re-interpret as XKeyEvent
            var keyEvt = Marshal.PtrToStructure<XKeyEvent>(GetEventPointer(ref evt));
            uint cleanMods = keyEvt.state & ~(X11Api.LockMask | 0x10u | 0x80u); // strip lock keys

            foreach (var (id, (keycode, modifiers, callback)) in _hotkeys)
            {
                if (keyEvt.keycode == keycode && cleanMods == modifiers)
                {
                    try { callback(); }
                    catch (Exception ex) { _logger.LogError(ex, "Error in hotkey callback {Id}", id); }
                }
            }
        }
    }

    private static unsafe nint GetEventPointer(ref XEvent evt)
    {
        fixed (XEvent* p = &evt)
            return (nint)p;
    }

    public void Dispose()
    {
        _running = false;
        UnregisterAll();
        if (_display != nint.Zero)
            X11Api.XCloseDisplay(_display);
        GC.SuppressFinalize(this);
    }
}
