using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api;

public sealed class ManagementAuthOptions
{
    public const string SectionName = "ManagementAuth";
    public string Issuer { get; init; } = "restaurant-os";
    public string Audience { get; init; } = "restaurant-os-management";
    public string PlatformAudience { get; init; } = "restaurant-os-platform";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 10;
    public int RefreshTokenDays { get; init; } = 7;
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
}

public static class ManagementClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string BranchId = "branch_id";
    public const string Realm = "realm";
    public const string PlatformRole = "platform_role";
}

public sealed class ManagementAuthService(
    RestaurantOsDbContext dbContext,
    IPasswordHasher<ManagementUser> passwordHasher,
    IOptions<ManagementAuthOptions> options,
    TimeProvider timeProvider) : IManagementAuthService
{
    private readonly ManagementAuthOptions _options = options.Value;

    public async Task<ManagementTokenResult> RegisterAsync(
        string email,
        string password,
        string restaurantName,
        string branchName,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        ValidateRegistration(email, password, restaurantName, branchName);

        var normalizedEmail = NormalizeEmail(email);
        if (await dbContext.ManagementUsers.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ManagementAuthException(
                "EMAIL_IN_USE",
                "An account with this email already exists.");
        }

        var tenantId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var trimmedRestaurant = restaurantName.Trim();
        var trimmedBranch = string.IsNullOrWhiteSpace(branchName) ? "Ana şube" : branchName.Trim();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                dbContext.Tenants.Add(new Tenant(tenantId, trimmedRestaurant));
                dbContext.Restaurants.Add(new Restaurant(restaurantId, tenantId, trimmedRestaurant));
                dbContext.Branches.Add(new Branch(branchId, tenantId, restaurantId, trimmedBranch));
                dbContext.TenantSubscriptions.Add(
                    TenantSubscription.CreateProTrial(tenantId, now));

                await ManagementRolePermissionSync.SyncBuiltInRolesAsync(dbContext, cancellationToken);
                var role = dbContext.ManagementRoles.Local
                    .Single(x => x.Name == "RestaurantOwner");
                var user = new ManagementUser(
                    Guid.NewGuid(),
                    email.Trim(),
                    normalizedEmail,
                    "pending",
                    now);
                user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
                dbContext.ManagementUsers.Add(user);
                dbContext.ManagementMemberships.Add(new ManagementMembership(
                    Guid.NewGuid(),
                    user.Id,
                    tenantId,
                    branchId,
                    role.Id));

                var pair = CreateTokenPair(user.Id, tenantId, branchId, Guid.NewGuid(), now);
                dbContext.ManagementRefreshSessions.Add(pair.Session);
                dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                    Guid.NewGuid(),
                    "Registered",
                    true,
                    now,
                    user.Id,
                    tenantId,
                    branchId));
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return pair.Result;
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        });
    }

    public async Task<ManagementTokenResult> LoginAsync(
        string email,
        string password,
        Guid? tenantId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        var passwordValid = VerifyPassword(user, password);

        if (user is null || !user.IsActive || user.IsLockedOut(now) || !passwordValid)
        {
            if (user is not null && user.IsActive && !user.IsLockedOut(now))
            {
                user.RecordFailedLogin(
                    now,
                    _options.MaxFailedAttempts,
                    TimeSpan.FromMinutes(_options.LockoutMinutes));
            }

            dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                Guid.NewGuid(),
                "FailedLogin",
                false,
                now,
                user?.Id,
                tenantId is null || tenantId == Guid.Empty ? null : tenantId,
                branchId is null || branchId == Guid.Empty ? null : branchId));
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        var resolvedTenantId = tenantId is null || tenantId == Guid.Empty ? (Guid?)null : tenantId;
        var resolvedBranchId = branchId is null || branchId == Guid.Empty ? (Guid?)null : branchId;
        if ((resolvedTenantId is null) != (resolvedBranchId is null))
        {
            throw InvalidCredentials();
        }

        if (resolvedTenantId is null)
        {
            var membership = await dbContext.ManagementMemberships
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.IsActive)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (membership is null)
            {
                throw InvalidCredentials();
            }

            resolvedTenantId = membership.TenantId;
            resolvedBranchId = membership.BranchId;
        }

        var membershipExists = await HasActiveMembershipAsync(
            user.Id,
            resolvedTenantId.Value,
            resolvedBranchId!.Value,
            cancellationToken);
        if (!membershipExists)
        {
            user.RecordFailedLogin(
                now,
                _options.MaxFailedAttempts,
                TimeSpan.FromMinutes(_options.LockoutMinutes));
            dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                Guid.NewGuid(),
                "FailedLogin",
                false,
                now,
                user.Id));
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
        }

        await ManagementRolePermissionSync.SyncBuiltInRolesAsync(dbContext, cancellationToken);
        user.RecordSuccessfulLogin(now);
        var result = CreateTokenPair(user.Id, resolvedTenantId.Value, resolvedBranchId.Value, Guid.NewGuid(), now);
        dbContext.ManagementRefreshSessions.Add(result.Session);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "Login",
            true,
            now,
            user.Id,
            resolvedTenantId.Value,
            resolvedBranchId.Value));
        await dbContext.SaveChangesAsync(cancellationToken);
        return result.Result;
    }

    public async Task<ManagementTokenResult> LoginPlatformAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        var passwordValid = VerifyPassword(user, password);

        if (user is null || !user.IsActive || user.IsLockedOut(now) || !passwordValid)
        {
            if (user is not null && user.IsActive && !user.IsLockedOut(now))
            {
                user.RecordFailedLogin(
                    now,
                    _options.MaxFailedAttempts,
                    TimeSpan.FromMinutes(_options.LockoutMinutes));
            }

            dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                Guid.NewGuid(),
                "FailedPlatformLogin",
                false,
                now,
                user?.Id));
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        var staff = await dbContext.PlatformStaff
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, cancellationToken);
        if (staff is null)
        {
            dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                Guid.NewGuid(),
                "FailedPlatformLogin",
                false,
                now,
                user.Id,
                detail: "not-platform-staff"));
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new ManagementAuthException(
                "PLATFORM_ACCESS_DENIED",
                "This account is not authorized for platform management.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
        }

        user.RecordSuccessfulLogin(now);
        var result = CreateTokenPair(
            user.Id,
            Guid.Empty,
            Guid.Empty,
            Guid.NewGuid(),
            now,
            AuthRealms.Platform,
            staff.RoleCode,
            user.Email);
        dbContext.ManagementRefreshSessions.Add(result.Session);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "PlatformLogin",
            true,
            now,
            user.Id,
            detail: staff.RoleCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return result.Result;
    }

    private Task<ManagementRole> EnsureOwnerRoleAsync(CancellationToken cancellationToken) =>
        ManagementRolePermissionSync.SyncRoleAsync(
            dbContext,
            "RestaurantOwner",
            ManagementAuthServicePermissions.Owner,
            cancellationToken);

    private static void ValidateRegistration(
        string email,
        string password,
        string restaurantName,
        string branchName)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256 || !email.Contains('@'))
        {
            throw new ManagementAuthException("VALIDATION_ERROR", "A valid email is required.");
        }

        if (string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 128)
        {
            throw new ManagementAuthException(
                "VALIDATION_ERROR",
                "Password must be between 12 and 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(restaurantName) || restaurantName.Trim().Length > 120)
        {
            throw new ManagementAuthException("VALIDATION_ERROR", "Restaurant name is required.");
        }

        if (!string.IsNullOrWhiteSpace(branchName) && branchName.Trim().Length > 120)
        {
            throw new ManagementAuthException("VALIDATION_ERROR", "Branch name is too long.");
        }
    }

    public async Task<ManagementTokenResult> RefreshAsync(
        string refreshToken,
        string expectedRealm,
        CancellationToken cancellationToken)
    {
        ValidateOpaqueToken(refreshToken);
        var now = timeProvider.GetUtcNow();
        var tokenHash = OpaqueToken.Hash(refreshToken);
        var session = await dbContext.ManagementRefreshSessions
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw InvalidRefreshToken();
        if (AuthRealms.Normalize(session.Realm) != AuthRealms.Normalize(expectedRealm))
        {
            throw InvalidRefreshToken();
        }

        if (session.RevokedAtUtc is not null)
        {
            if (session.ReplacedByTokenHash is not null)
            {
                await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
                dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                    Guid.NewGuid(),
                    "RefreshReplay",
                    false,
                    now,
                    session.UserId,
                    session.TenantId,
                    session.BranchId));
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            throw InvalidRefreshToken();
        }

        if (session.ExpiresAtUtc <= now)
        {
            session.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        string? roleCode = null;
        string? email = null;
        var realm = AuthRealms.Normalize(session.Realm);
        if (realm == AuthRealms.Platform)
        {
            var staff = await dbContext.PlatformStaff
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == session.UserId && x.IsActive, cancellationToken);
            var user = await dbContext.ManagementUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive, cancellationToken);
            if (staff is null || user is null)
            {
                session.Revoke(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                throw InvalidRefreshToken();
            }

            roleCode = staff.RoleCode;
            email = user.Email;
        }
        else if (!await HasActiveMembershipAsync(
                session.UserId,
                session.TenantId,
                session.BranchId,
                cancellationToken))
        {
            session.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        var pair = CreateTokenPair(
            session.UserId,
            session.TenantId,
            session.BranchId,
            session.FamilyId,
            now,
            realm,
            roleCode,
            email);
        session.Rotate(now, pair.Session.TokenHash);
        dbContext.ManagementRefreshSessions.Add(pair.Session);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        return pair.Result;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length is < 32 or > 512)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = OpaqueToken.Hash(refreshToken);
        var session = await dbContext.ManagementRefreshSessions
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return;
        }

        await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "Logout",
            true,
            now,
            session.UserId,
            session.TenantId,
            session.BranchId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ManagementTokenResult> SwitchBranchAsync(
        Guid userId,
        Guid tenantId,
        Guid targetBranchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!await HasActiveMembershipAsync(userId, tenantId, targetBranchId, cancellationToken))
        {
            throw new ManagementAuthException(
                "BRANCH_ACCESS_DENIED",
                "Bu şubeye erişim yetkiniz yok.");
        }

        var pair = CreateTokenPair(userId, tenantId, targetBranchId, Guid.NewGuid(), now);
        dbContext.ManagementRefreshSessions.Add(pair.Session);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "SwitchBranch",
            true,
            now,
            userId,
            tenantId,
            targetBranchId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return pair.Result;
    }

    public async Task<IReadOnlyList<ManagementMembershipScopeResult>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from membership in dbContext.ManagementMemberships.AsNoTracking()
            join branch in dbContext.Branches.AsNoTracking() on membership.BranchId equals branch.Id
            join restaurant in dbContext.Restaurants.AsNoTracking() on branch.RestaurantId equals restaurant.Id
            join role in dbContext.ManagementRoles.AsNoTracking() on membership.RoleId equals role.Id
            where membership.UserId == userId && membership.IsActive
            orderby restaurant.Name, branch.Name
            select new
            {
                membership.Id,
                membership.TenantId,
                RestaurantId = restaurant.Id,
                RestaurantName = restaurant.Name,
                BranchId = branch.Id,
                BranchName = branch.Name,
                RoleName = role.Name,
                RoleId = role.Id,
            }).ToListAsync(cancellationToken);

        var roleIds = rows.Select(x => x.RoleId).Distinct().ToArray();
        var grants = await dbContext.ManagementRolePermissions
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId)
                && (x.Permission == ManagementPermissions.BranchManage
                    || x.Permission == ManagementPermissions.BranchMembers))
            .Select(x => new { x.RoleId, x.Permission })
            .ToListAsync(cancellationToken);
        var manageSet = grants
            .Where(x => x.Permission == ManagementPermissions.BranchManage)
            .Select(x => x.RoleId)
            .ToHashSet();
        var membersSet = grants
            .Where(x => x.Permission == ManagementPermissions.BranchMembers)
            .Select(x => x.RoleId)
            .ToHashSet();

        return rows
            .Select(row => new ManagementMembershipScopeResult(
                row.Id,
                row.TenantId,
                row.RestaurantId,
                row.RestaurantName,
                row.BranchId,
                row.BranchName,
                row.RoleName,
                manageSet.Contains(row.RoleId),
                membersSet.Contains(row.RoleId)))
            .ToArray();
    }

    private bool VerifyPassword(ManagementUser? user, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            return false;
        }

        if (user is not null)
        {
            return passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
                != PasswordVerificationResult.Failed;
        }

        var dummy = new ManagementUser(
            Guid.Empty,
            "dummy@invalid.local",
            "DUMMY@INVALID.LOCAL",
            "placeholder",
            DateTimeOffset.UnixEpoch);
        var dummyHash = passwordHasher.HashPassword(dummy, "not-the-password");
        _ = passwordHasher.VerifyHashedPassword(dummy, dummyHash, password);
        return false;
    }

    private Task<bool> HasActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.UserId == userId
                    && membership.TenantId == tenantId
                    && membership.IsActive
                    && membership.BranchId == branchId
                    && dbContext.Branches.Any(branch =>
                        branch.Id == branchId && branch.TenantId == tenantId),
                cancellationToken);

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.ManagementRefreshSessions
            .Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var item in sessions)
        {
            item.Revoke(now);
        }
    }

    private (ManagementTokenResult Result, ManagementRefreshSession Session) CreateTokenPair(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid familyId,
        DateTimeOffset now,
        string realm = AuthRealms.Management,
        string? roleCode = null,
        string? email = null)
    {
        var normalizedRealm = AuthRealms.Normalize(realm);
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var platformAudience = string.IsNullOrWhiteSpace(_options.PlatformAudience)
            ? "restaurant-os-platform"
            : _options.PlatformAudience;
        var audience = normalizedRealm == AuthRealms.Platform
            ? platformAudience
            : _options.Audience;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ManagementClaimTypes.Realm, normalizedRealm),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (normalizedRealm == AuthRealms.Platform)
        {
            claims.Add(new Claim(
                ManagementClaimTypes.PlatformRole,
                PlatformStaffRoles.Normalize(roleCode)));
        }
        else
        {
            claims.Add(new Claim(ManagementClaimTypes.TenantId, tenantId.ToString()));
            claims.Add(new Claim(ManagementClaimTypes.BranchId, branchId.ToString()));
        }

        var token = new JwtSecurityToken(
            _options.Issuer,
            audience,
            claims,
            now.UtcDateTime,
            accessExpires.UtcDateTime,
            credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = OpaqueToken.Create();
        var session = new ManagementRefreshSession(
            Guid.NewGuid(),
            familyId,
            userId,
            tenantId,
            branchId,
            OpaqueToken.Hash(refreshToken),
            now,
            refreshExpires,
            normalizedRealm);
        return (
            new ManagementTokenResult(
                accessToken,
                accessExpires,
                refreshToken,
                refreshExpires,
                userId,
                tenantId,
                branchId,
                normalizedRealm,
                normalizedRealm == AuthRealms.Platform ? PlatformStaffRoles.Normalize(roleCode) : null,
                email),
            session);
    }

    public async Task UpdateProfileAsync(
        Guid userId,
        string displayName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.ManagementUsers.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ManagementAuthException("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        try
        {
            user.UpdateProfile(displayName, phone ?? string.Empty, requireDisplayName: true);
        }
        catch (ArgumentException exception)
        {
            throw new ManagementAuthException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "ProfileUpdated",
            true,
            timeProvider.GetUtcNow(),
            userId,
            subjectId: userId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ManagementProfileResult> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.ManagementUsers.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ManagementAuthException("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        return new ManagementProfileResult(user.Id, user.Email, user.DisplayName, user.Phone);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 12 || newPassword.Length > 128)
        {
            throw new ManagementAuthException(
                "VALIDATION_ERROR",
                "Yeni şifre 12-128 karakter olmalıdır.");
        }

        var user = await dbContext.ManagementUsers.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ManagementAuthException("USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        if (!VerifyPassword(user, currentPassword))
        {
            throw new ManagementAuthException("INVALID_CREDENTIALS", "Mevcut şifre hatalı.");
        }

        user.UpdatePasswordHash(passwordHasher.HashPassword(user, newPassword));
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.ManagementRefreshSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "PasswordChanged",
            true,
            now,
            userId,
            subjectId: userId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
        {
            return string.Empty;
        }

        return email.Trim().ToUpperInvariant();
    }

    private static void ValidateOpaqueToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 512)
        {
            throw InvalidRefreshToken();
        }
    }

    private static ManagementAuthException InvalidCredentials() =>
        new("INVALID_CREDENTIALS", "Email, password, or management scope is invalid.");

    private static ManagementAuthException InvalidRefreshToken() =>
        new("INVALID_REFRESH_TOKEN", "Refresh token is invalid.");
}
