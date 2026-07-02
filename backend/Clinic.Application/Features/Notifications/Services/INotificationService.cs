using Clinic.Application.Common.Models;
using Clinic.Application.Features.Notifications.DTOs;

namespace Clinic.Application.Features.Notifications.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMineAsync(PageRequest page, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
