using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(RestaurantOsDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!TryGetScope(context.User, out var userId, out var tenantId, out var branchId))
        {
            return;
        }

        var allowed = await dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.TenantId == tenantId
                && membership.IsActive
                && membership.BranchId == branchId
                && dbContext.Branches.Any(branch =>
                    branch.Id == branchId
                    && branch.TenantId == tenantId
                    && dbContext.Restaurants.Any(restaurant =>
                        restaurant.Id == branch.RestaurantId
                        && restaurant.TenantId == tenantId))
                && dbContext.ManagementRolePermissions.Any(permission =>
                    permission.RoleId == membership.RoleId
                    && permission.Permission == requirement.Permission));
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }

    public static bool TryGetScope(
        ClaimsPrincipal principal,
        out Guid userId,
        out Guid tenantId,
        out Guid branchId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;
        branchId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && Guid.TryParse(principal.FindFirstValue(ManagementClaimTypes.TenantId), out tenantId)
            && Guid.TryParse(principal.FindFirstValue(ManagementClaimTypes.BranchId), out branchId);
    }

    public static Task<bool> HasPermissionAsync(
        RestaurantOsDbContext dbContext,
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string permission,
        CancellationToken cancellationToken) =>
        dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == userId
                    && membership.TenantId == tenantId
                    && membership.BranchId == branchId
                    && membership.IsActive
                    && dbContext.ManagementRolePermissions.Any(grant =>
                        grant.RoleId == membership.RoleId
                        && grant.Permission == permission),
                cancellationToken);
}

public static class ManagementPolicies
{
    public const string OrderView = "Management.Order.View";
    public const string OrderModify = "Management.Order.Modify";
    public const string TableView = "Management.Table.View";
    public const string TableEdit = "Management.Table.Edit";
    public const string MenuView = "Management.Menu.View";
    public const string MenuEdit = "Management.Menu.Edit";
    public const string MenuPublish = "Management.Menu.Publish";
    public const string AnalyticsView = "Management.Analytics.View";
    public const string AnalyticsFinancialView = "Management.Analytics.FinancialView";
    public const string SubscriptionManage = "Management.Subscription.Manage";
    public const string PlatformManage = "Management.Platform.Manage";

    public static void AddManagementPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(
            OrderView,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.OrderView)));
        options.AddPolicy(
            OrderModify,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.OrderModify)));
        options.AddPolicy(
            TableView,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.TableView)));
        options.AddPolicy(
            TableEdit,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.TableEdit)));
        options.AddPolicy(
            MenuView,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.MenuView)));
        options.AddPolicy(
            MenuEdit,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.MenuEdit)));
        options.AddPolicy(
            MenuPublish,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.MenuPublish)));
        options.AddPolicy(
            AnalyticsView,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.AnalyticsView)));
        options.AddPolicy(
            AnalyticsFinancialView,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.AnalyticsFinancialView)));
        options.AddPolicy(
            SubscriptionManage,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.SubscriptionManage)));
        options.AddPolicy(
            PlatformManage,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(ManagementPermissions.PlatformManage)));
    }
}
