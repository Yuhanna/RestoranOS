using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public interface IManagementNotificationNotifier
{
    Task NotifyAsync(
        Guid tenantId,
        ManagedNotificationResult notification,
        CancellationToken cancellationToken);
}

public sealed class NotificationManagementService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IEmailSender emailSender,
    IManagementNotificationNotifier pushNotifier,
    IOptions<EmailOptions> emailOptions) : INotificationManagementService
{
    public async Task<IReadOnlyList<ManagedNotificationResult>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TenantNotifications
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartsAtUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedNotificationResult>> ListPlatformAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.TenantNotifications
            .AsNoTracking()
            .Where(x => x.TenantId == null)
            .OrderByDescending(x => x.StartsAtUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ManagedNotificationResult> CreateAsync(
        Guid tenantId,
        CreateManagedNotificationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BroadcastToAllTenants)
        {
            throw new CustomerExperienceException(
                "PLATFORM_SCOPE_REQUIRED",
                "Platform-wide notifications must be created from the platform console.");
        }

        TenantNotification entity;
        try
        {
            entity = new TenantNotification(
                Guid.NewGuid(),
                tenantId,
                command.Audience,
                command.Title,
                command.Body,
                command.StartsAtUtc,
                command.EndsAtUtc,
                command.ActionUrl,
                command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.TenantNotifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ManagedNotificationResult> CreatePlatformAsync(
        CreatePlatformNotificationCommand command,
        CancellationToken cancellationToken)
    {
        TenantNotification entity;
        try
        {
            entity = new TenantNotification(
                Guid.NewGuid(),
                tenantId: null,
                command.Audience,
                command.Title,
                command.Body,
                command.StartsAtUtc,
                command.EndsAtUtc,
                command.ActionUrl,
                command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.TenantNotifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<NotificationDispatchResult> DispatchPlatformAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.TenantNotifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.TenantId == null,
            cancellationToken)
            ?? throw new CustomerExperienceException("NOTIFICATION_NOT_FOUND", "Notification was not found.");

        return await DispatchCoreAsync(notification, cancellationToken);
    }

    public async Task<NotificationDispatchResult> DispatchAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.TenantNotifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.TenantId == tenantId,
            cancellationToken)
            ?? throw new CustomerExperienceException("NOTIFICATION_NOT_FOUND", "Notification was not found.");

        return await DispatchCoreAsync(notification, cancellationToken);
    }

    private async Task<NotificationDispatchResult> DispatchCoreAsync(
        TenantNotification notification,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!notification.IsVisibleAt(now))
        {
            throw new CustomerExperienceException(
                "NOTIFICATION_INACTIVE",
                "Notification is not active or is outside its schedule.");
        }

        var tenantIds = notification.TenantId is null
            ? await dbContext.Tenants.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken)
            : [notification.TenantId.Value];

        var recipients = new List<(Guid TenantId, string Email)>();
        foreach (var targetTenantId in tenantIds)
        {
            var subscription = await dbContext.TenantSubscriptions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == targetTenantId, cancellationToken);
            if (subscription is null || !SubscriptionAudiences.Matches(notification.Audience, subscription, now))
            {
                continue;
            }

            var emails = await dbContext.ManagementMemberships
                .AsNoTracking()
                .Where(x => x.TenantId == targetTenantId && x.IsActive)
                .Join(
                    dbContext.ManagementUsers.AsNoTracking(),
                    membership => membership.UserId,
                    user => user.Id,
                    (_, user) => user.Email)
                .Distinct()
                .ToListAsync(cancellationToken);
            recipients.AddRange(emails.Select(email => (targetTenantId, email)));
        }

        var fromName = emailOptions.Value.FromName;
        var subject = notification.Title;
        var bodyText = BuildPlainBody(notification, fromName);
        var bodyHtml = BuildHtmlBody(notification, fromName);
        var sentEmails = new List<string>();
        foreach (var email in recipients.Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await emailSender.SendAsync(
                    new EmailMessage(email, subject, bodyText, bodyHtml),
                    cancellationToken);
                sentEmails.Add(email);
            }
            catch (Exception)
            {
                // Continue with other recipients; partial delivery is acceptable for MVP.
            }
        }

        var pushCount = 0;
        var pushedTenants = new HashSet<Guid>();
        foreach (var (targetTenantId, _) in recipients)
        {
            if (!pushedTenants.Add(targetTenantId))
            {
                continue;
            }

            await pushNotifier.NotifyAsync(targetTenantId, Map(notification), cancellationToken);
            pushCount++;
        }

        notification.MarkDispatched(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationDispatchResult(sentEmails.Count, pushCount, sentEmails);
    }

    public async Task<ManagedNotificationResult> SetPlatformActiveAsync(
        Guid notificationId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.TenantNotifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.TenantId == null,
            cancellationToken)
            ?? throw new CustomerExperienceException("NOTIFICATION_NOT_FOUND", "Notification was not found.");

        notification.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(notification);
    }

    private static ManagedNotificationResult Map(TenantNotification notification) =>
        new(
            notification.Id,
            notification.TenantId,
            notification.Audience,
            notification.Title,
            notification.Body,
            notification.ActionUrl,
            notification.StartsAtUtc,
            notification.EndsAtUtc,
            notification.IsActive,
            notification.LastDispatchedAtUtc);

    private static string BuildPlainBody(TenantNotification notification, string fromName)
    {
        var action = string.IsNullOrWhiteSpace(notification.ActionUrl)
            ? string.Empty
            : $"\n\nDevam: {notification.ActionUrl}";
        return $"{notification.Body}{action}\n\n— {fromName}";
    }

    private static string BuildHtmlBody(TenantNotification notification, string fromName)
    {
        var action = string.IsNullOrWhiteSpace(notification.ActionUrl)
            ? string.Empty
            : $"""<p><a href="{notification.ActionUrl}">Detayları görüntüle</a></p>""";
        return $"""
            <html><body>
            <p>{System.Net.WebUtility.HtmlEncode(notification.Body)}</p>
            {action}
            <p><small>{System.Net.WebUtility.HtmlEncode(fromName)}</small></p>
            </body></html>
            """;
    }
}
