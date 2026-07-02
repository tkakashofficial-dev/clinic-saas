using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Application.Features.Notifications.DTOs;
using Clinic.Application.Features.Notifications.Services;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ClinicDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(ClinicDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<NotificationDto>> GetMineAsync(
        PageRequest page, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientTenantUserId == _currentUser.TenantUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.RecipientTenantUserId == _currentUser.TenantUserId && !n.IsRead,
                cancellationToken);
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId
                && n.RecipientTenantUserId == _currentUser.TenantUserId, cancellationToken)
            ?? throw new NotFoundException("Notification not found.");

        notification.MarkRead();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var unread = await _context.Notifications
            .Where(n => n.RecipientTenantUserId == _currentUser.TenantUserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
            notification.MarkRead();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
