namespace POS.Domain.Interfaces;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public enum NotificationCategory
{
    General,
    Sale,
    Inventory,
    Backup,
    Printer,
    System
}

public class NotificationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;
    public NotificationCategory Category { get; set; } = NotificationCategory.General;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
    public bool IsDismissed { get; set; }
    public int AutoDismissSeconds { get; set; } = 5;
    public Action? OnClick { get; set; }
}

public interface INotificationService
{
    event EventHandler<NotificationMessage>? NotificationRaised;
    IReadOnlyList<NotificationMessage> Notifications { get; }
    int UnreadCount { get; }

    void ShowInfo(string title, string message, NotificationCategory category = NotificationCategory.General);
    void ShowSuccess(string title, string message, NotificationCategory category = NotificationCategory.General);
    void ShowWarning(string title, string message, NotificationCategory category = NotificationCategory.General);
    void ShowError(string title, string message, NotificationCategory category = NotificationCategory.General);
    void Show(NotificationMessage notification);

    void MarkAsRead(Guid notificationId);
    void MarkAllAsRead();
    void Dismiss(Guid notificationId);
    void DismissAll();
    void Clear();
}
