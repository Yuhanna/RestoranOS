using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260918120000_TenantContractOverridesAndQuoteReview")]
public partial class TenantContractOverridesAndQuoteReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OverrideMaxBranches",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OverrideMaxActiveUsers",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OverrideMaxOrderHistoryHours",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OverrideMaxActiveQrCodes",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContractNote",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReviewedByUserId",
            schema: "billing",
            table: "EnterpriseQuoteRequests",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ReviewedAtUtc",
            schema: "billing",
            table: "EnterpriseQuoteRequests",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DecisionNote",
            schema: "billing",
            table: "EnterpriseQuoteRequests",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EnterpriseQuoteRequests_Status_CreatedAtUtc",
            schema: "billing",
            table: "EnterpriseQuoteRequests",
            columns: new[] { "Status", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EnterpriseQuoteRequests_Status_CreatedAtUtc",
            schema: "billing",
            table: "EnterpriseQuoteRequests");

        migrationBuilder.DropColumn(
            name: "OverrideMaxBranches",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "OverrideMaxActiveUsers",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "OverrideMaxOrderHistoryHours",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "OverrideMaxActiveQrCodes",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "ContractNote",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "ReviewedByUserId",
            schema: "billing",
            table: "EnterpriseQuoteRequests");

        migrationBuilder.DropColumn(
            name: "ReviewedAtUtc",
            schema: "billing",
            table: "EnterpriseQuoteRequests");

        migrationBuilder.DropColumn(
            name: "DecisionNote",
            schema: "billing",
            table: "EnterpriseQuoteRequests");
    }
}
