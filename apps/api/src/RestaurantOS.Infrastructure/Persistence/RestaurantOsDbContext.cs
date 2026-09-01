using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class RestaurantOsDbContext(DbContextOptions<RestaurantOsDbContext> options)
    : DbContext(options)
{
    public const string ManagementAuthSchema = "auth";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<TableQrCode> TableQrCodes => Set<TableQrCode>();
    public DbSet<PublishedMenu> Menus => Set<PublishedMenu>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuCategoryTranslation> MenuCategoryTranslations => Set<MenuCategoryTranslation>();
    public DbSet<MenuItemTranslation> MenuItemTranslations => Set<MenuItemTranslation>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<CustomerOrderItem> CustomerOrderItems => Set<CustomerOrderItem>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ManagementUser> ManagementUsers => Set<ManagementUser>();
    public DbSet<ManagementRole> ManagementRoles => Set<ManagementRole>();
    public DbSet<ManagementRolePermissionGrant> ManagementRolePermissions => Set<ManagementRolePermissionGrant>();
    public DbSet<ManagementMembership> ManagementMemberships => Set<ManagementMembership>();
    public DbSet<ManagementRefreshSession> ManagementRefreshSessions => Set<ManagementRefreshSession>();
    public DbSet<ManagementAuditLog> ManagementAuditLogs => Set<ManagementAuditLog>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<MenuPromotion> MenuPromotions => Set<MenuPromotion>();
    public DbSet<TenantNotification> TenantNotifications => Set<TenantNotification>();
    public DbSet<SubscriptionOffer> SubscriptionOffers => Set<SubscriptionOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customer");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
        });
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.ToTable("Restaurants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasIndex(x => new { x.TenantId, x.Id }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.CustomerMenuSettingsJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.TenantId, x.Id }).IsUnique();
            entity.HasOne(x => x.Restaurant).WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DiningTable>(entity =>
        {
            entity.ToTable("DiningTables");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(80);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.Label }).IsUnique();
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TableQrCode>(entity =>
        {
            entity.ToTable("TableQrCodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.ProtectedToken).HasMaxLength(1024);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.TableId });
            entity.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PublishedMenu>(entity =>
        {
            entity.ToTable("Menus");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Ignore(x => x.Lifecycle);
            entity.Ignore(x => x.IsArchived);
            entity.Ignore(x => x.IsPublished);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.PublishedAtUtc });
        });
        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.ToTable("MenuCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.MenuId, x.SortOrder });
        });
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("MenuItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.ImageUrl).HasMaxLength(2048);
            entity.Property(x => x.ImageAlt).HasMaxLength(200);
            entity.Property(x => x.CatalogJson).HasColumnType("nvarchar(max)");
            entity.Ignore(x => x.Price);
            entity.Property(x => x.PriceCurrency).HasMaxLength(3);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.MenuId, x.CategoryId });
        });
        modelBuilder.Entity<MenuCategoryTranslation>(entity =>
        {
            entity.ToTable("MenuCategoryTranslations");
            entity.HasKey(x => new { x.CategoryId, x.Locale });
            entity.Property(x => x.Locale).HasMaxLength(8);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.CategoryId });
        });
        modelBuilder.Entity<MenuItemTranslation>(entity =>
        {
            entity.ToTable("MenuItemTranslations");
            entity.HasKey(x => new { x.ItemId, x.Locale });
            entity.Property(x => x.Locale).HasMaxLength(8);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.ItemId });
        });
        modelBuilder.Entity<CustomerSession>(entity =>
        {
            entity.ToTable("CustomerSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.Locale).HasMaxLength(8);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.ExpiresAtUtc });
        });
        modelBuilder.Entity<GuestSession>(entity =>
        {
            entity.ToTable("GuestSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceIdentifier).HasMaxLength(128);
            entity.Property(x => x.IpHash).HasMaxLength(64);
            entity.Property(x => x.Locale).HasMaxLength(8);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.TableSessionId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.LastActivityAtUtc });
        });
        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("CustomerOrders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.DisplayNumber).HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.Ignore(x => x.Subtotal);
            entity.Ignore(x => x.Discount);
            entity.Ignore(x => x.Total);
            entity.Property(x => x.TotalCurrency).HasMaxLength(3);
            entity.HasIndex(x => new { x.CustomerSessionId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.CreatedAtUtc });
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId);
        });
        modelBuilder.Entity<CustomerOrderItem>(entity =>
        {
            entity.ToTable("CustomerOrderItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Note).HasMaxLength(160);
            entity.Ignore(x => x.ListUnitPrice);
            entity.Ignore(x => x.DiscountUnitAmount);
            entity.Ignore(x => x.UnitPrice);
            entity.Property(x => x.UnitPriceCurrency).HasMaxLength(3);
        });
        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("ServiceRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Note).HasMaxLength(160);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CustomerSessionId, x.Type, x.Status });
        });
        modelBuilder.Entity<ManagementUser>(entity =>
        {
            entity.ToTable("Users", ManagementAuthSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        modelBuilder.Entity<ManagementRole>(entity =>
        {
            entity.ToTable("Roles", ManagementAuthSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<ManagementRolePermissionGrant>(entity =>
        {
            entity.ToTable("RolePermissions", ManagementAuthSchema);
            entity.HasKey(x => new { x.RoleId, x.Permission });
            entity.Property(x => x.Permission).HasMaxLength(120);
            entity.HasOne<ManagementRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ManagementMembership>(entity =>
        {
            entity.ToTable("Memberships", ManagementAuthSchema);
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.TenantId, x.BranchId, x.RoleId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.TenantId, x.IsActive });
            entity.HasOne<ManagementUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BranchId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManagementRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ManagementRefreshSession>(entity =>
        {
            entity.ToTable("RefreshSessions", ManagementAuthSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.FamilyId, x.RevokedAtUtc });
            entity.HasOne<ManagementUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ManagementAuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "audit");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.Detail).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        });
        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("TenantSubscriptions", "billing");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.PlanCode).HasMaxLength(32);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(x => x.Entitlements);
        });
        modelBuilder.Entity<MenuPromotion>(entity =>
        {
            entity.ToTable("MenuPromotions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Scope).HasMaxLength(16);
            entity.Property(x => x.DiscountKind).HasMaxLength(16);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.IsActive, x.StartsAtUtc });
        });
        modelBuilder.Entity<TenantNotification>(entity =>
        {
            entity.ToTable("TenantNotifications", "billing");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Audience).HasMaxLength(16);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(2000);
            entity.Property(x => x.ActionUrl).HasMaxLength(2048);
            entity.HasIndex(x => new { x.TenantId, x.Audience, x.IsActive, x.StartsAtUtc });
        });
        modelBuilder.Entity<SubscriptionOffer>(entity =>
        {
            entity.ToTable("SubscriptionOffers", "billing");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Audience).HasMaxLength(16);
            entity.Property(x => x.TargetPlanCode).HasMaxLength(32);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(2000);
            entity.HasIndex(x => new { x.Audience, x.IsActive, x.StartsAtUtc });
        });
    }
}
