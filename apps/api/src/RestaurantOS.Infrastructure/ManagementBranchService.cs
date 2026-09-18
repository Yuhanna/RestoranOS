using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementBranchService(
    RestaurantOsDbContext dbContext,
    IFeatureEntitlementService entitlements,
    IPasswordHasher<ManagementUser> passwordHasher,
    TimeProvider timeProvider) : IManagementBranchService
{
    public const string RoleKeyOwner = "owner";
    public const string RoleKeyManager = "manager";
    public const string RoleKeyStaff = "staff";

    public async Task<IReadOnlyList<ManagementBranchResult>> ListBranchesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var branchIds = branches.Select(x => x.Id).ToArray();
        var memberCounts = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && branchIds.Contains(x.BranchId))
            .GroupBy(x => x.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var tableCounts = await dbContext.DiningTables
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && branchIds.Contains(x.BranchId))
            .GroupBy(x => x.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        return branches
            .Select(branch => new ManagementBranchResult(
                branch.Id,
                branch.RestaurantId,
                branch.Name,
                memberCounts.GetValueOrDefault(branch.Id),
                tableCounts.GetValueOrDefault(branch.Id),
                IsCurrent: false,
                branch.IsFrozen))
            .ToArray();
    }

    public async Task<ManagementBranchResult> CreateBranchAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid currentBranchId,
        string name,
        bool confirmAddonPurchase,
        CancellationToken cancellationToken)
    {
        await entitlements.EnsureCanCreateBranchAsync(tenantId, confirmAddonPurchase, cancellationToken);

        var trimmed = RequireName(name);
        var current = await dbContext.Branches
            .AsNoTracking()
            .Where(x => x.Id == currentBranchId && x.TenantId == tenantId)
            .Select(x => new { x.RestaurantId, x.IsFrozen })
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            throw new CustomerExperienceException("BRANCH_NOT_FOUND", "Geçerli şube bulunamadı.");
        }

        if (current.IsFrozen)
        {
            throw new EntitlementException(
                "BRANCH_FROZEN",
                "Dondurulmuş şubeden yeni şube açılamaz. Önce planınızı yükseltin.");
        }

        var restaurantId = current.RestaurantId;

        if (await dbContext.Branches.AnyAsync(
                x => x.TenantId == tenantId && x.RestaurantId == restaurantId && x.Name == trimmed,
                cancellationToken))
        {
            throw new CustomerExperienceException("BRANCH_NAME_IN_USE", "Bu isimde bir şube zaten var.");
        }

        var ownerRole = await EnsureRoleAsync(
            "RestaurantOwner",
            ManagementAuthServicePermissions.Owner,
            cancellationToken);

        var branch = new Branch(Guid.NewGuid(), tenantId, restaurantId, trimmed);
        dbContext.Branches.Add(branch);

        var alreadyMember = await dbContext.ManagementMemberships.AnyAsync(
            x => x.UserId == actorUserId
                && x.TenantId == tenantId
                && x.BranchId == branch.Id
                && x.IsActive,
            cancellationToken);
        if (!alreadyMember)
        {
            var existingInactive = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
                x => x.UserId == actorUserId
                    && x.TenantId == tenantId
                    && x.BranchId == branch.Id,
                cancellationToken);
            if (existingInactive is not null)
            {
                existingInactive.Activate();
            }
            else
            {
                dbContext.ManagementMemberships.Add(new ManagementMembership(
                    Guid.NewGuid(),
                    actorUserId,
                    tenantId,
                    branch.Id,
                    ownerRole.Id));
            }
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchCreated",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            tenantId,
            branch.Id,
            branch.Id,
            trimmed));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ManagementBranchResult(branch.Id, restaurantId, branch.Name, 1, 0, IsCurrent: false, IsFrozen: false);
    }

    public async Task<ManagementBranchResult> RenameBranchAsync(
        Guid tenantId,
        Guid branchId,
        string name,
        CancellationToken cancellationToken)
    {
        var trimmed = RequireName(name);
        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(x => x.Id == branchId && x.TenantId == tenantId, cancellationToken)
            ?? throw new CustomerExperienceException("BRANCH_NOT_FOUND", "Şube bulunamadı.");

        if (await dbContext.Branches.AnyAsync(
                x => x.TenantId == tenantId
                    && x.RestaurantId == branch.RestaurantId
                    && x.Id != branchId
                    && x.Name == trimmed,
                cancellationToken))
        {
            throw new CustomerExperienceException("BRANCH_NAME_IN_USE", "Bu isimde bir şube zaten var.");
        }

        branch.Rename(trimmed);
        await dbContext.SaveChangesAsync(cancellationToken);

        var memberCount = await dbContext.ManagementMemberships.CountAsync(
            x => x.TenantId == tenantId && x.BranchId == branchId && x.IsActive,
            cancellationToken);
        var tableCount = await dbContext.DiningTables.CountAsync(
            x => x.TenantId == tenantId && x.BranchId == branchId && x.IsActive,
            cancellationToken);

        return new ManagementBranchResult(
            branch.Id,
            branch.RestaurantId,
            branch.Name,
            memberCount,
            tableCount,
            IsCurrent: false,
            branch.IsFrozen);
    }

    public async Task<IReadOnlyList<ManagementBranchMemberResult>> ListMembersAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);

        var rows = await (
            from membership in dbContext.ManagementMemberships.AsNoTracking()
            join user in dbContext.ManagementUsers.AsNoTracking() on membership.UserId equals user.Id
            join role in dbContext.ManagementRoles.AsNoTracking() on membership.RoleId equals role.Id
            where membership.TenantId == tenantId && membership.BranchId == branchId
            orderby membership.IsActive descending, user.Email
            select new ManagementBranchMemberResult(
                membership.Id,
                user.Id,
                user.Email,
                role.Name,
                ToRoleKey(role.Name),
                membership.IsActive,
                user.LastLoginAtUtc,
                user.DisplayName,
                user.Phone)).ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<ManagementBranchMemberResult> InviteMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        string email,
        string? password,
        string roleKey,
        string displayName,
        string? phone,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        await entitlements.EnsureBranchNotFrozenAsync(tenantId, branchId, cancellationToken);

        var actor = await GetActorAuthorityAsync(actorUserId, tenantId, branchId, cancellationToken);
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256 || !email.Contains('@'))
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Geçerli bir e-posta gerekli.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Ad soyad gerekli.");
        }

        var resolvedRoleKey = NormalizeRoleKey(roleKey);
        EnsureCanAssignRole(actor, resolvedRoleKey);
        if (resolvedRoleKey != RoleKeyStaff)
        {
            await entitlements.EnsureCanManageAdditionalRolesAsync(tenantId, cancellationToken);
        }

        var (roleName, permissions) = ResolveRole(resolvedRoleKey);
        var role = await EnsureRoleAsync(roleName, permissions, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        var seatReservedForNewUser = false;

        try
        {
            if (user is null)
            {
                if (string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 128)
                {
                    throw new CustomerExperienceException(
                        "VALIDATION_ERROR",
                        "Yeni hesap için şifre 12-128 karakter olmalıdır.");
                }

                await entitlements.EnsureCanAddUserAsync(tenantId, cancellationToken);
                seatReservedForNewUser = true;
                user = new ManagementUser(
                    Guid.NewGuid(),
                    email.Trim(),
                    normalizedEmail,
                    "pending",
                    now,
                    displayName,
                    phone);
                user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
                dbContext.ManagementUsers.Add(user);
            }
            else if (!user.IsActive)
            {
                throw new CustomerExperienceException("USER_INACTIVE", "Bu hesap pasif durumda.");
            }
            else
            {
                user.UpdateProfile(displayName, phone ?? string.Empty, requireDisplayName: true);
            }
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        var membership = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken);

        if (membership is null)
        {
            var isNewUserForTenant = !await dbContext.ManagementMemberships.AnyAsync(
                x => x.UserId == user.Id && x.TenantId == tenantId && x.IsActive,
                cancellationToken);
            if (isNewUserForTenant && !seatReservedForNewUser)
            {
                await entitlements.EnsureCanAddUserAsync(tenantId, cancellationToken);
            }

            membership = new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                tenantId,
                branchId,
                role.Id);
            dbContext.ManagementMemberships.Add(membership);
        }
        else if (!membership.IsActive)
        {
            var isNewUserForTenant = !await dbContext.ManagementMemberships.AnyAsync(
                x => x.UserId == user.Id
                    && x.TenantId == tenantId
                    && x.IsActive
                    && x.Id != membership.Id,
                cancellationToken);
            if (isNewUserForTenant)
            {
                await entitlements.EnsureCanAddUserAsync(tenantId, cancellationToken);
            }

            membership.Activate();
            membership.ChangeRole(role.Id);
        }
        else
        {
            throw new CustomerExperienceException(
                "MEMBERSHIP_EXISTS",
                "Bu kullanıcı zaten bu şubede aktif.");
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchMemberInvited",
            true,
            now,
            actorUserId,
            tenantId,
            branchId,
            user.Id,
            $"{user.DisplayName}:{user.Email}:{resolvedRoleKey}"));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ManagementBranchMemberResult(
            membership.Id,
            user.Id,
            user.Email,
            role.Name,
            resolvedRoleKey,
            membership.IsActive,
            user.LastLoginAtUtc,
            user.DisplayName,
            user.Phone);
    }

    public async Task<ManagementBranchMemberResult> UpdateMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        Guid membershipId,
        string displayName,
        string? phone,
        string? roleKey,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        await entitlements.EnsureBranchNotFrozenAsync(tenantId, branchId, cancellationToken);

        var actor = await GetActorAuthorityAsync(actorUserId, tenantId, branchId, cancellationToken);
        var membership = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
            x => x.Id == membershipId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        var user = await dbContext.ManagementUsers.SingleOrDefaultAsync(x => x.Id == membership.UserId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");
        var currentRole = await dbContext.ManagementRoles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == membership.RoleId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        EnsureCanManageTarget(actor, currentRole.Name, allowSelf: true, actorUserId, membership.UserId);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Ad soyad gerekli.");
        }

        try
        {
            user.UpdateProfile(displayName, phone ?? string.Empty, requireDisplayName: true);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        var roleName = currentRole.Name;
        var resolvedRoleKey = ToRoleKey(currentRole.Name);
        if (!string.IsNullOrWhiteSpace(roleKey))
        {
            resolvedRoleKey = NormalizeRoleKey(roleKey);
            EnsureCanAssignRole(actor, resolvedRoleKey);
            if (membership.UserId == actorUserId && resolvedRoleKey != ToRoleKey(currentRole.Name))
            {
                throw new CustomerExperienceException(
                    "CANNOT_CHANGE_OWN_ROLE",
                    "Kendi rolünüzü bu ekrandan değiştiremezsiniz.");
            }

            if (resolvedRoleKey != RoleKeyStaff)
            {
                await entitlements.EnsureCanManageAdditionalRolesAsync(tenantId, cancellationToken);
            }

            var (nextRoleName, permissions) = ResolveRole(resolvedRoleKey);
            var role = await EnsureRoleAsync(nextRoleName, permissions, cancellationToken);
            membership.ChangeRole(role.Id);
            roleName = nextRoleName;
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchMemberUpdated",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            tenantId,
            branchId,
            user.Id,
            $"{user.DisplayName}:{user.Email}:{resolvedRoleKey}"));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ManagementBranchMemberResult(
            membership.Id,
            user.Id,
            user.Email,
            roleName,
            resolvedRoleKey,
            membership.IsActive,
            user.LastLoginAtUtc,
            user.DisplayName,
            user.Phone);
    }

    public async Task ResetMemberPasswordAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        Guid membershipId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        await entitlements.EnsureBranchNotFrozenAsync(tenantId, branchId, cancellationToken);

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 12 || newPassword.Length > 128)
        {
            throw new CustomerExperienceException(
                "VALIDATION_ERROR",
                "Şifre 12-128 karakter olmalıdır.");
        }

        var actor = await GetActorAuthorityAsync(actorUserId, tenantId, branchId, cancellationToken);
        var membership = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
            x => x.Id == membershipId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        var role = await dbContext.ManagementRoles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == membership.RoleId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");
        EnsureCanManageTarget(actor, role.Name, allowSelf: true, actorUserId, membership.UserId);

        var user = await dbContext.ManagementUsers.SingleOrDefaultAsync(x => x.Id == membership.UserId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        user.UpdatePasswordHash(passwordHasher.HashPassword(user, newPassword));
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.ManagementRefreshSessions
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchMemberPasswordReset",
            true,
            now,
            actorUserId,
            tenantId,
            branchId,
            user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        var actor = await GetActorAuthorityAsync(actorUserId, tenantId, branchId, cancellationToken);
        var membership = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
            x => x.Id == membershipId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        if (membership.UserId == actorUserId)
        {
            throw new CustomerExperienceException(
                "CANNOT_DEACTIVATE_SELF",
                "Kendi üyeliğinizi bu ekrandan kapatamazsınız.");
        }

        var role = await dbContext.ManagementRoles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == membership.RoleId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");
        EnsureCanManageTarget(actor, role.Name, allowSelf: false, actorUserId, membership.UserId);

        membership.Deactivate();
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchMemberDeactivated",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            tenantId,
            branchId,
            membership.UserId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        await entitlements.EnsureBranchNotFrozenAsync(tenantId, branchId, cancellationToken);

        var actor = await GetActorAuthorityAsync(actorUserId, tenantId, branchId, cancellationToken);
        var membership = await dbContext.ManagementMemberships.SingleOrDefaultAsync(
            x => x.Id == membershipId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");

        if (membership.IsActive)
        {
            throw new CustomerExperienceException(
                "MEMBERSHIP_ALREADY_ACTIVE",
                "Bu üyelik zaten aktif.");
        }

        var user = await dbContext.ManagementUsers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == membership.UserId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");
        if (!user.IsActive)
        {
            throw new CustomerExperienceException(
                "USER_INACTIVE",
                "Bu hesap pasif durumda; önce hesabı açın.");
        }

        var role = await dbContext.ManagementRoles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == membership.RoleId, cancellationToken)
            ?? throw new CustomerExperienceException("MEMBERSHIP_NOT_FOUND", "Üyelik bulunamadı.");
        EnsureCanManageTarget(actor, role.Name, allowSelf: true, actorUserId, membership.UserId);

        var hasOtherActiveSeat = await dbContext.ManagementMemberships.AnyAsync(
            x => x.UserId == membership.UserId
                && x.TenantId == tenantId
                && x.IsActive
                && x.Id != membership.Id,
            cancellationToken);
        if (!hasOtherActiveSeat)
        {
            await entitlements.EnsureCanAddUserAsync(tenantId, cancellationToken);
        }

        membership.Activate();
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "BranchMemberActivated",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            tenantId,
            branchId,
            membership.UserId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ActorMemberAuthority> GetActorAuthorityAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.ManagementMemberships.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == actorUserId
                    && x.TenantId == tenantId
                    && x.BranchId == branchId
                    && x.IsActive,
                cancellationToken)
            ?? throw new CustomerExperienceException(
                "FORBIDDEN",
                "Bu şubede üye yönetimi için aktif üyeliğiniz yok.");

        var canManageBranches = await dbContext.ManagementRolePermissions.AsNoTracking()
            .AnyAsync(
                x => x.RoleId == membership.RoleId && x.Permission == ManagementPermissions.BranchManage,
                cancellationToken);
        var canManageMembers = canManageBranches
            || await dbContext.ManagementRolePermissions.AsNoTracking()
                .AnyAsync(
                    x => x.RoleId == membership.RoleId && x.Permission == ManagementPermissions.BranchMembers,
                    cancellationToken);
        if (!canManageMembers)
        {
            throw new CustomerExperienceException(
                "FORBIDDEN",
                "Bu şubede hesap yönetimi yetkiniz yok.");
        }

        return new ActorMemberAuthority(canManageBranches);
    }

    private static void EnsureCanAssignRole(ActorMemberAuthority actor, string roleKey)
    {
        if (actor.CanManageBranches)
        {
            return;
        }

        if (roleKey != RoleKeyStaff)
        {
            throw new CustomerExperienceException(
                "MEMBER_ROLE_FORBIDDEN",
                "Şube yöneticisi yalnızca personel hesabı ekleyebilir veya atayabilir.");
        }
    }

    private static void EnsureCanManageTarget(
        ActorMemberAuthority actor,
        string targetRoleName,
        bool allowSelf,
        Guid actorUserId,
        Guid targetUserId)
    {
        if (!allowSelf && targetUserId == actorUserId)
        {
            throw new CustomerExperienceException(
                "CANNOT_DEACTIVATE_SELF",
                "Kendi üyeliğinizi bu ekrandan kapatamazsınız.");
        }

        if (actor.CanManageBranches)
        {
            return;
        }

        if (!string.Equals(targetRoleName, "BranchStaff", StringComparison.Ordinal))
        {
            throw new CustomerExperienceException(
                "MEMBER_TARGET_FORBIDDEN",
                "Şube yöneticisi yalnızca personel hesaplarını düzenleyebilir.");
        }
    }

    private sealed record ActorMemberAuthority(bool CanManageBranches);

    public async Task<ManagementNetworkSummaryResult> GetNetworkSummaryAsync(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeRange(fromUtc, toUtc);
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var branchIds = branches.Select(x => x.Id).ToArray();
        var completed = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && branchIds.Contains(order.BranchId)
                && order.Status == OrderStatus.Completed
                && order.CreatedAtUtc >= from
                && order.CreatedAtUtc < to)
            .Select(order => new { order.BranchId, order.TotalAmountMinor, order.TotalCurrency })
            .ToListAsync(cancellationToken);

        var cancelled = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && branchIds.Contains(order.BranchId)
                && order.Status == OrderStatus.Cancelled
                && order.CreatedAtUtc >= from
                && order.CreatedAtUtc < to)
            .Select(order => new { order.BranchId, order.TotalAmountMinor })
            .ToListAsync(cancellationToken);

        var openOrders = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && branchIds.Contains(order.BranchId)
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled)
            .GroupBy(order => order.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var openRequests = await dbContext.ServiceRequests
            .AsNoTracking()
            .Where(request =>
                request.TenantId == tenantId
                && branchIds.Contains(request.BranchId)
                && request.Status == ServiceRequestStatus.Open)
            .GroupBy(request => request.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var tableCounts = await dbContext.DiningTables
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && branchIds.Contains(x.BranchId))
            .GroupBy(x => x.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var memberCounts = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && branchIds.Contains(x.BranchId))
            .GroupBy(x => x.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var currency = completed.FirstOrDefault()?.TotalCurrency ?? "TRY";
        var branchStats = branches.Select(branch =>
        {
            var sales = completed.Where(x => x.BranchId == branch.Id).Sum(x => x.TotalAmountMinor);
            var completedCount = completed.Count(x => x.BranchId == branch.Id);
            var cancelledCount = cancelled.Count(x => x.BranchId == branch.Id);
            return new ManagementNetworkBranchStatResult(
                branch.Id,
                branch.Name,
                sales,
                completedCount,
                cancelledCount,
                openOrders.GetValueOrDefault(branch.Id),
                openRequests.GetValueOrDefault(branch.Id),
                tableCounts.GetValueOrDefault(branch.Id),
                memberCounts.GetValueOrDefault(branch.Id),
                completedCount == 0 ? 0 : sales / completedCount);
        }).ToArray();

        return new ManagementNetworkSummaryResult(
            branchStats.Sum(x => x.GrossSalesMinor),
            branchStats.Sum(x => x.CompletedOrderCount),
            branchStats.Sum(x => x.CancelledOrderCount),
            branchStats.Sum(x => x.OpenOrderCount),
            branchStats.Sum(x => x.OpenServiceRequestCount),
            branches.Count,
            currency,
            branchStats);
    }

    private async Task EnsureBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Branches.AnyAsync(
            x => x.Id == branchId && x.TenantId == tenantId,
            cancellationToken);
        if (!exists)
        {
            throw new CustomerExperienceException("BRANCH_NOT_FOUND", "Şube bulunamadı.");
        }
    }

    private Task<ManagementRole> EnsureRoleAsync(
        string roleName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken) =>
        ManagementRolePermissionSync.SyncRoleAsync(dbContext, roleName, permissions, cancellationToken);

    private static (string RoleName, IReadOnlyList<string> Permissions) ResolveRole(string roleKey) =>
        roleKey switch
        {
            RoleKeyOwner => ("RestaurantOwner", ManagementAuthServicePermissions.Owner),
            RoleKeyManager => ("BranchManager", ManagementAuthServicePermissions.BranchManager),
            _ => ("BranchStaff", ManagementAuthServicePermissions.BranchStaff),
        };

    private static string NormalizeRoleKey(string? roleKey)
    {
        if (string.Equals(roleKey, RoleKeyOwner, StringComparison.OrdinalIgnoreCase))
        {
            return RoleKeyOwner;
        }

        if (string.Equals(roleKey, RoleKeyManager, StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleKey, "şube yöneticisi", StringComparison.OrdinalIgnoreCase))
        {
            return RoleKeyManager;
        }

        return RoleKeyStaff;
    }

    private static string ToRoleKey(string roleName) =>
        roleName switch
        {
            "RestaurantOwner" => RoleKeyOwner,
            "BranchManager" => RoleKeyManager,
            _ => RoleKeyStaff,
        };

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Şube adı gerekli (en fazla 120 karakter).");
        }

        return name.Trim();
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static (DateTimeOffset From, DateTimeOffset To) NormalizeRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        var from = fromUtc.ToUniversalTime();
        var to = toUtc.ToUniversalTime();
        if (to <= from)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Tarih aralığı geçersiz.");
        }

        if (to - from > TimeSpan.FromDays(93))
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "En fazla 93 günlük aralık seçilebilir.");
        }

        return (from, to);
    }
}

/// <summary>
/// Shared permission sets for management roles. Kept here so Infrastructure can ensure roles
/// without a circular reference to Api.ManagementAuthService.
/// </summary>
public static class ManagementAuthServicePermissions
{
    public static readonly string[] Owner =
    [
        ManagementPermissions.OrderView,
        ManagementPermissions.OrderModify,
        ManagementPermissions.TableView,
        ManagementPermissions.TableEdit,
        ManagementPermissions.MenuView,
        ManagementPermissions.MenuEdit,
        ManagementPermissions.MenuPublish,
        ManagementPermissions.AnalyticsView,
        ManagementPermissions.AnalyticsFinancialView,
        ManagementPermissions.SubscriptionManage,
        ManagementPermissions.BranchManage,
        ManagementPermissions.BranchMembers,
    ];

    public static readonly string[] BranchManager =
    [
        ManagementPermissions.OrderView,
        ManagementPermissions.OrderModify,
        ManagementPermissions.TableView,
        ManagementPermissions.TableEdit,
        ManagementPermissions.MenuView,
        ManagementPermissions.MenuEdit,
        ManagementPermissions.MenuPublish,
        ManagementPermissions.AnalyticsView,
        ManagementPermissions.AnalyticsFinancialView,
        ManagementPermissions.BranchMembers,
    ];

    /// <summary>
    /// Floor staff: live orders/service only. No table/QR admin, menu edits, analytics, billing, or branches.
    /// </summary>
    public static readonly string[] BranchStaff =
    [
        ManagementPermissions.OrderView,
        ManagementPermissions.OrderModify,
        ManagementPermissions.TableView,
        ManagementPermissions.MenuView,
    ];

    /// <summary>
    /// Permissions that built-in role sync may add or remove. Custom grants outside this set are left alone.
    /// </summary>
    public static readonly HashSet<string> SyncedPermissionUniverse = new(Owner, StringComparer.Ordinal);

    public static IEnumerable<(string RoleName, string[] Permissions)> BuiltInRoles { get; } =
    [
        ("RestaurantOwner", Owner),
        ("BranchManager", BranchManager),
        ("BranchStaff", BranchStaff),
    ];
}
