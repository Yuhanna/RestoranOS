using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260916120000_AlignMenuPackageSchema")]
public partial class AlignMenuPackageSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MenuPackageComponents_MenuPackages_PackageId",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropPrimaryKey(
            name: "PK_MenuPackageComponents",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropIndex(
            name: "IX_MenuPackageComponents_PackageId_MenuItemId",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropIndex(
            name: "IX_MenuPackageComponents_PackageId_SortOrder",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropColumn(
            name: "Id",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.RenameColumn(
            name: "PackageId",
            schema: "customer",
            table: "MenuPackageComponents",
            newName: "MenuPackageId");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            schema: "customer",
            table: "MenuPackages",
            type: "nvarchar(160)",
            maxLength: 160,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(120)",
            oldMaxLength: 120);

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "customer",
            table: "MenuPackages",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SlotLabel",
            schema: "customer",
            table: "MenuPackageComponents",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(60)",
            oldMaxLength: 60,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SourcePackageName",
            schema: "customer",
            table: "CustomerOrderItems",
            type: "nvarchar(160)",
            maxLength: 160,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(120)",
            oldMaxLength: 120,
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_MenuPackageComponents",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "MenuPackageId", "MenuItemId" });

        migrationBuilder.AddForeignKey(
            name: "FK_MenuPackageComponents_MenuPackages_MenuPackageId",
            schema: "customer",
            table: "MenuPackageComponents",
            column: "MenuPackageId",
            principalSchema: "customer",
            principalTable: "MenuPackages",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_MenuPackageId_SortOrder",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "MenuPackageId", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MenuPackageComponents_MenuPackages_MenuPackageId",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropPrimaryKey(
            name: "PK_MenuPackageComponents",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.DropIndex(
            name: "IX_MenuPackageComponents_MenuPackageId_SortOrder",
            schema: "customer",
            table: "MenuPackageComponents");

        migrationBuilder.RenameColumn(
            name: "MenuPackageId",
            schema: "customer",
            table: "MenuPackageComponents",
            newName: "PackageId");

        migrationBuilder.AddColumn<Guid>(
            name: "Id",
            schema: "customer",
            table: "MenuPackageComponents",
            type: "uniqueidentifier",
            nullable: false,
            defaultValueSql: "NEWSEQUENTIALID()");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            schema: "customer",
            table: "MenuPackages",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(160)",
            oldMaxLength: 160);

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            schema: "customer",
            table: "MenuPackages",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SlotLabel",
            schema: "customer",
            table: "MenuPackageComponents",
            type: "nvarchar(60)",
            maxLength: 60,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(80)",
            oldMaxLength: 80,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SourcePackageName",
            schema: "customer",
            table: "CustomerOrderItems",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(160)",
            oldMaxLength: 160,
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_MenuPackageComponents",
            schema: "customer",
            table: "MenuPackageComponents",
            column: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_MenuPackageComponents_MenuPackages_PackageId",
            schema: "customer",
            table: "MenuPackageComponents",
            column: "PackageId",
            principalSchema: "customer",
            principalTable: "MenuPackages",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_PackageId_MenuItemId",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "PackageId", "MenuItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_PackageId_SortOrder",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "PackageId", "SortOrder" });
    }
}
