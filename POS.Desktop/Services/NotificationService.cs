using POS.Domain.Interfaces;

namespace POS.Desktop.Services;

/// <summary>
/// Desktop implementation of INotificationService.
/// Manages a list of notifications and raises events for the UI to display toasts.
/// </summary>
public class NotificationService : INotificationService
{
    public event EventHandler<NotificationMessage>? NotificationRaised;

    private readonly List<NotificationMessage> _notifications = new();
    private readonly object _lock = new();

    public IReadOnlyList<NotificationMessage> Notifications
    {
        get { lock (_lock) return _notifications.ToList(); }
    }

    public int UnreadCount
    {
        get { lock (_lock) return _notifications.Count(n => !n.IsRead && !n.IsDismissed); }
    }

    public void ShowInfo(string title, string message, NotificationCategory category = NotificationCategory.General)
        => Show(new NotificationMessage
        {
            Title = title,
            Message = message,
            Type = NotificationType.Info,
            Category = category
        });

    public void ShowSuccess(string title, string message, NotificationCategory category = NotificationCategory.General)
        => Show(new NotificationMessage
        {
            Title = title,
            Message = message,
            Type = NotificationType.Success,
            Category = category,
            AutoDismissSeconds = 4
        });

    public void ShowWarning(string title, string message, NotificationCategory category = NotificationCategory.General)
        => Show(new NotificationMessage
        {
            Title = title,
            Message = message,
            Type = NotificationType.Warning,
            Category = category,
            AutoDismissSeconds = 6
        });

    public void ShowError(string title, string message, NotificationCategory category = NotificationCategory.General)
        => Show(new NotificationMessage
        {
            Title = title,
            Message = message,
            Type = NotificationType.Error,
            Category = category,
            AutoDismissSeconds = 0 // Errors stay until dismissed
        });

    public void Show(NotificationMessage notification)
    {
        lock (_lock)
        {
            _notifications.Add(notification);
        }

        NotificationRaised?.Invoke(this, notification);
    }

    public void MarkAsRead(Guid notificationId)
    {
        lock (_lock)
        {
            var n = _notifications.FirstOrDefault(x => x.Id == notificationId);
            if (n != null) n.IsRead = true;
        }
    }

    public void MarkAllAsRead()
    {
        lock (_lock)
        {
            foreach (var n in _notifications)
                n.IsRead = true;
        }
    }

    public void Dismiss(Guid notificationId)
    {
        lock (_lock)
        {
            var n = _notifications.FirstOrDefault(x => x.Id == notificationId);
            if (n != null) n.IsDismissed = true;
        }
    }

    public void DismissAll()
    {
        lock (_lock)
        {
            foreach (var n in _notifications)
                n.IsDismissed = true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _notifications.Clear();
        }
    }
}
