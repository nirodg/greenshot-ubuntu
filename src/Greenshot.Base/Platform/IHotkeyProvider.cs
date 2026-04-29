namespace Greenshot.Base.Platform;

public interface IHotkeyProvider : IDisposable
{
    bool IsAvailable { get; }

    bool RegisterHotkey(string id, string hotkeyString, Action callback);
    void UnregisterHotkey(string id);
    void UnregisterAll();
}
