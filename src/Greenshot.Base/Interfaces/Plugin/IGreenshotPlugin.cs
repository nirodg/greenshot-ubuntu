namespace Greenshot.Base.Interfaces.Plugin;

public interface IGreenshotPlugin : IDisposable
{
    string Name { get; }
    bool IsConfigurable { get; }

    bool Initialize();
    void Shutdown();
    void Configure();
}
