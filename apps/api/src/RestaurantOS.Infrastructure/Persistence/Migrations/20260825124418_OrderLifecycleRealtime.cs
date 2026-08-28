using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderLifecycleRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "customer",
                table: "CustomerOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                schema: "customer",
                table: "CustomerOrders",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                """
                UPDATE [customer].[CustomerOrders]
                SET [StatusChangedAtUtc] = [CreatedAtUtc],
                    [Status] = CASE [Status]
                        WHEN N'Received' THEN N'Submitted'
                        WHEN N'Served' THEN N'Completed'
                        ELSE [Status]
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [customer].[CustomerOrders]
                SET [Status] = CASE [Status]
                    WHEN N'Submitted' THEN N'Received'
                    WHEN N'Accepted' THEN N'Received'
                    WHEN N'Completed' THEN N'Served'
                    WHEN N'Cancelled' THEN N'Received'
                    ELSE [Status]
                END;
                """);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "customer",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                schema: "customer",
                table: "CustomerOrders");
        }
    }
}
