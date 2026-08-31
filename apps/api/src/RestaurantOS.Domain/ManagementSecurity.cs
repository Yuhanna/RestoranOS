namespace RestaurantOS.Domain;

public static class ManagementPermissions
{
    public const string OrderView = "Order.View";
    public const string OrderModify = "Order.Modify";
    public const string TableView = "Table.View";
    public const string TableEdit = "Table.Edit";
    public const string MenuView = "Menu.View";
    public const string MenuEdit = "Menu.Edit";
    public const string MenuPublish = "Menu.Publish";
    public const string AnalyticsView = "Analytics.View";
    public const string AnalyticsFinancialView = "Analytics.FinancialView";
    public const string SubscriptionManage = "Subscription.Manage";
    public const string PlatformManage = "Platform.Manage";
}

public sealed class ManagementUser
{
    private ManagementUser() { }

    public ManagementUser(
        Guid id,
        string email,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = Required(email);
        NormalizedEmail = Required(normalizedEmail);
        PasswordHash = Required(passwordHash);
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockoutEndUtc > nowUtc;

    public void RecordFailedLogin(DateTimeOffset nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEndUtc = nowUtc.ToUniversalTime().Add(lockoutDuration);
            AccessFailedCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset nowUtc)
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = nowUtc.ToUniversalTime();
    }

    public void UpdatePasswordHash(string passwordHash) => PasswordHash = Required(passwordHash);

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class ManagementRole
{
    private ManagementRole() { }
    public ManagementRole(Guid id, string name) => (Id, Name) = (id, Required(name));
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class ManagementRolePermissionGrant
{
    private ManagementRolePermissionGrant() { }
    public ManagementRolePermissionGrant(Guid roleId, string permission) =>
        (RoleId, Permission) = (roleId, Required(permission));
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = null!;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class ManagementMembership
{
    private ManagementMembership() { }

    public ManagementMembership(
        Guid id,
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid roleId)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        BranchId = branchId;
        RoleId = roleId;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public void Deactivate() => IsActive = false;
}

public sealed class ManagementRefreshSession
{
    private ManagementRefreshSession() { }

    public ManagementRefreshSession(
        Guid id,
        Guid familyId,
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Expiry must be after creation.");
        }

        Id = id;
        FamilyId = familyId;
        UserId = userId;
        TenantId = tenantId;
        BranchId = branchId;
        TokenHash = Required(tokenHash);
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Rotate(DateTimeOffset nowUtc, string replacementHash)
    {
        RevokedAtUtc = nowUtc.ToUniversalTime();
        ReplacedByTokenHash = Required(replacementHash);
    }

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc.ToUniversalTime();

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class ManagementAuditLog
{
    private ManagementAuditLog() { }

    public ManagementAuditLog(
        Guid id,
        string action,
        bool succeeded,
        DateTimeOffset occurredAtUtc,
        Guid? userId = null,
        Guid? tenantId = null,
        Guid? branchId = null,
        Guid? subjectId = null,
        string? detail = null)
    {
        Id = id;
        Action = Required(action);
        Succeeded = succeeded;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        UserId = userId;
        TenantId = tenantId;
        BranchId = branchId;
        SubjectId = subjectId;
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
    }

    public Guid Id { get; private set; }
    public string Action { get; private set; } = null!;
    public bool Succeeded { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? SubjectId { get; private set; }
    public string? Detail { get; private set; }

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}
