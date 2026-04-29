namespace Greenshot.Base.Platform;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string message, string? iconPath = null);
}
