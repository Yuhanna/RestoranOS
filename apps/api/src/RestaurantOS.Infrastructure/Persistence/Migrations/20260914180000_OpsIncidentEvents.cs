using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260914180000_OpsIncidentEvents")]
public partial class OpsIncidentEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "ops");

        migrationBuilder.CreateTable(
            name: "IncidentEvents",
            schema: "ops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                HttpStatus = table.Column<int>(type: "int", nullable: true),
                Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CustomerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GuestSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ClientFingerprintHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                ClientIpHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                RequestMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                RequestPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                DetailJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DedupeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_TenantId_BranchId_OccurredAtUtc",
            schema: "ops",
            table: "IncidentEvents",
            columns: new[] { "TenantId", "BranchId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_Code_OccurredAtUtc",
            schema: "ops",
            table: "IncidentEvents",
            columns: new[] { "Code", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_ExpiresAtUtc",
            schema: "ops",
            table: "IncidentEvents",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_CorrelationId",
            schema: "ops",
            table: "IncidentEvents",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_OrderId",
            schema: "ops",
            table: "IncidentEvents",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentEvents_TableId",
            schema: "ops",
            table: "IncidentEvents",
            column: "TableId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IncidentEvents",
            schema: "ops");
    }
}
