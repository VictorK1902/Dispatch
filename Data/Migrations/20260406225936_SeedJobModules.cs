using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedJobModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "JobModules",
                columns: new[] { "Id", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 4, 6, 0, 0, 0, 0, DateTimeKind.Utc), "Return a weather report in terms of temperature", "Weather Report" },
                    { 2, new DateTime(2026, 4, 6, 0, 0, 0, 0, DateTimeKind.Utc), "Return a full monthly historical price of a stock symbol", "Stock Price Report" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "JobModules",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "JobModules",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
