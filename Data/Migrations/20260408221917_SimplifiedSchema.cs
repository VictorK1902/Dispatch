using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifiedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "AcsMessageId",
                table: "Jobs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcsMessageId",
                table: "Jobs");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcsMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_JobId",
                table: "Notifications",
                column: "JobId");
        }
    }
}
