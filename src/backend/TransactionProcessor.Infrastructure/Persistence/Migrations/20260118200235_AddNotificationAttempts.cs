using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "fk_notification_attempts_files",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "idx_notification_attempts_file_id",
                table: "notification_attempts",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_notification_attempts_last_attempt_at",
                table: "notification_attempts",
                column: "last_attempt_at");

            migrationBuilder.CreateIndex(
                name: "idx_notification_attempts_status",
                table: "notification_attempts",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_attempts");
        }
    }
}
