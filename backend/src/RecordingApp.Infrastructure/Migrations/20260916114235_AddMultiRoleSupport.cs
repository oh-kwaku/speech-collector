using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiRoleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "Invites",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => new { x.UserId, x.Role });
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve each user's existing single role as their first role assignment,
            // and each invite's role as the (single-entry) CSV, before dropping the old columns.
            migrationBuilder.Sql(
                "INSERT INTO \"UserRoleAssignments\" (\"UserId\", \"Role\") SELECT \"Id\", \"Role\" FROM \"Users\";");
            migrationBuilder.Sql("UPDATE \"Invites\" SET \"Roles\" = \"Role\";");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Invites");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Invites",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Lossy by nature (multiple roles collapse to one): picks the alphabetically
            // first role assignment per user, and the first CSV entry per invite.
            migrationBuilder.Sql(
                "UPDATE \"Users\" u SET \"Role\" = COALESCE(" +
                "(SELECT \"Role\" FROM \"UserRoleAssignments\" WHERE \"UserId\" = u.\"Id\" ORDER BY \"Role\" LIMIT 1), '');");
            migrationBuilder.Sql(
                "UPDATE \"Invites\" SET \"Role\" = COALESCE(SPLIT_PART(\"Roles\", ',', 1), '');");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Invites");
        }
    }
}
