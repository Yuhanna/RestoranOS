using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

/// <summary>
/// Keeps built-in management role permission grants aligned with the product matrix.
/// Adds missing grants and removes obsolete ones from the synced universe only.
/// </summary>
public static class ManagementRolePermissionSync
{
    public static async Task SyncBuiltInRolesAsync(
        RestaurantOsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var (roleName, permissions) in ManagementAuthServicePermissions.BuiltInRoles)
        {
            await SyncRoleAsync(dbContext, roleName, permissions, cancellationToken);
        }
    }

    public static async Task<ManagementRole> SyncRoleAsync(
        RestaurantOsDbContext dbContext,
        string roleName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.ManagementRoles
            .Where(x => x.Name == roleName)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (role is null)
        {
            role = new ManagementRole(Guid.NewGuid(), roleName);
            dbContext.ManagementRoles.Add(role);
        }

        var desired = permissions.ToHashSet(StringComparer.Ordinal);
        var existing = await dbContext.ManagementRolePermissions
            .Where(x => x.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        foreach (var permission in desired)
        {
            if (!existing.Any(x => string.Equals(x.Permission, permission, StringComparison.Ordinal)))
            {
                dbContext.ManagementRolePermissions.Add(new ManagementRolePermissionGrant(role.Id, permission));
            }
        }

        foreach (var grant in existing)
        {
            if (desired.Contains(grant.Permission))
            {
                continue;
            }

            if (ManagementAuthServicePermissions.SyncedPermissionUniverse.Contains(grant.Permission))
            {
                dbContext.ManagementRolePermissions.Remove(grant);
            }
        }

        return role;
    }
}
