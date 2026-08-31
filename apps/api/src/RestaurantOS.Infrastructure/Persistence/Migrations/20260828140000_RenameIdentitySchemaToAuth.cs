using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260828140000_RenameIdentitySchemaToAuth")]
public partial class RenameIdentitySchemaToAuth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: RestaurantOsDbContext.ManagementAuthSchema);

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'identity')
            BEGIN
                ALTER SCHEMA auth TRANSFER [identity].[Users];
                ALTER SCHEMA auth TRANSFER [identity].[Roles];
                ALTER SCHEMA auth TRANSFER [identity].[RolePermissions];
                ALTER SCHEMA auth TRANSFER [identity].[Memberships];
                ALTER SCHEMA auth TRANSFER [identity].[RefreshSessions];
                DROP SCHEMA [identity];
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "identity");

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'auth')
            BEGIN
                ALTER SCHEMA [identity] TRANSFER auth.[Users];
                ALTER SCHEMA [identity] TRANSFER auth.[Roles];
                ALTER SCHEMA [identity] TRANSFER auth.[RolePermissions];
                ALTER SCHEMA [identity] TRANSFER auth.[Memberships];
                ALTER SCHEMA [identity] TRANSFER auth.[RefreshSessions];
                DROP SCHEMA auth;
            END
            """);
    }
}
