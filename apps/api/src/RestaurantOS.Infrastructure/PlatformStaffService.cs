using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class PlatformStaffService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IPasswordHasher<ManagementUser> passwordHasher) : IPlatformStaffService
{
    public async Task<IReadOnlyList<PlatformStaffMemberResult>> ListAsync(CancellationToken cancellationToken)
    {
        var staff = await dbContext.PlatformStaff.AsNoTracking().ToListAsync(cancellationToken);
        var userIds = staff.Select(member => member.UserId).Distinct().ToArray();
        var emails = await dbContext.ManagementUsers
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email, cancellationToken);
        return staff
            .Select(member => new PlatformStaffMemberResult(
                member.UserId,
                emails.GetValueOrDefault(member.UserId, member.UserId.ToString()),
                member.RoleCode,
                member.IsActive,
                member.GrantedAtUtc))
            .OrderBy(member => member.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<PlatformStaffMemberResult> InviteAsync(
        Guid actorUserId,
        string actorRole,
        InvitePlatformStaffCommand command,
        CancellationToken cancellationToken)
    {
        EnsureOwner(actorRole);
        var email = command.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Geçerli bir e-posta girin.");
        }

        var roleCode = PlatformStaffRoles.Normalize(command.RoleCode);
        var normalizedEmail = email.ToUpperInvariant();
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            var password = command.Password ?? string.Empty;
            if (password.Length is < 12 or > 128)
            {
                throw new CustomerExperienceException(
                    "VALIDATION_ERROR",
                    "Yeni operatör için parola 12–128 karakter olmalı.");
            }

            user = new ManagementUser(Guid.NewGuid(), email, normalizedEmail, "pending", timeProvider.GetUtcNow());
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
            dbContext.ManagementUsers.Add(user);
        }

        var existing = await dbContext.PlatformStaff
            .SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (existing is not null)
        {
            throw new CustomerExperienceException("STAFF_EXISTS", "Bu e-posta zaten platform kadrosunda.");
        }

        var staff = new PlatformStaff(user.Id, roleCode, timeProvider.GetUtcNow());
        dbContext.PlatformStaff.Add(staff);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PlatformStaffMemberResult(staff.UserId, user.Email, staff.RoleCode, staff.IsActive, staff.GrantedAtUtc);
    }

    public async Task<PlatformStaffMemberResult> ChangeRoleAsync(
        Guid actorUserId,
        string actorRole,
        Guid targetUserId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        EnsureOwner(actorRole);
        var nextRole = PlatformStaffRoles.Normalize(roleCode);
        var staff = await GetStaffAsync(targetUserId, cancellationToken);
        if (PlatformStaffRoles.Normalize(staff.RoleCode) == PlatformStaffRoles.Owner
            && nextRole != PlatformStaffRoles.Owner
            && await CountActiveOwnersAsync(cancellationToken) <= 1)
        {
            throw new CustomerExperienceException(
                "LAST_OWNER",
                "Son aktif Owner operatörünün rolü değiştirilemez.");
        }

        staff.ChangeRole(nextRole);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ToResultAsync(staff, cancellationToken);
    }

    public async Task<PlatformStaffMemberResult> SetActiveAsync(
        Guid actorUserId,
        string actorRole,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureOwner(actorRole);
        if (targetUserId == actorUserId && !isActive)
        {
            throw new CustomerExperienceException("SELF_LOCKOUT", "Kendi hesabınızı pasifleştiremezsiniz.");
        }

        var staff = await GetStaffAsync(targetUserId, cancellationToken);
        if (staff.IsActive
            && !isActive
            && PlatformStaffRoles.Normalize(staff.RoleCode) == PlatformStaffRoles.Owner
            && await CountActiveOwnersAsync(cancellationToken) <= 1)
        {
            throw new CustomerExperienceException(
                "LAST_OWNER",
                "Son aktif Owner operatörü pasifleştirilemez.");
        }

        if (isActive)
        {
            staff.Activate();
        }
        else
        {
            staff.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ToResultAsync(staff, cancellationToken);
    }

    private async Task<PlatformStaff> GetStaffAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.PlatformStaff.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
        ?? throw new CustomerExperienceException("STAFF_NOT_FOUND", "Platform operatörü bulunamadı.");

    private async Task<int> CountActiveOwnersAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.PlatformStaff
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.RoleCode)
            .ToListAsync(cancellationToken);
        return roles.Count(role => PlatformStaffRoles.Normalize(role) == PlatformStaffRoles.Owner);
    }

    private async Task<PlatformStaffMemberResult> ToResultAsync(PlatformStaff staff, CancellationToken cancellationToken)
    {
        var email = await dbContext.ManagementUsers.AsNoTracking()
            .Where(x => x.Id == staff.UserId)
            .Select(x => x.Email)
            .SingleAsync(cancellationToken);
        return new PlatformStaffMemberResult(staff.UserId, email, staff.RoleCode, staff.IsActive, staff.GrantedAtUtc);
    }

    private static void EnsureOwner(string actorRole)
    {
        if (!PlatformStaffRoles.CanManageStaff(actorRole))
        {
            throw new CustomerExperienceException(
                "PLATFORM_ROLE_DENIED",
                "Operatör kadrosunu yalnızca Owner yönetebilir.");
        }
    }
}
