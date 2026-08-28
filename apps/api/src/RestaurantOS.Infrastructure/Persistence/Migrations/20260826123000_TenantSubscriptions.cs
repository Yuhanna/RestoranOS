using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class TenantSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "billing");

        migrationBuilder.CreateTable(
            name: "TenantSubscriptions",
            schema: "billing",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlanCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantSubscriptions", x => x.TenantId);
                table.ForeignKey(
                    name: "FK_TenantSubscriptions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "customer",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TenantSubscriptions",
            schema: "billing");
    }
}
