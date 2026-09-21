using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIrrAnnotationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Annotations_RecordingId",
                table: "Annotations");

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalAnnotationId",
                table: "Recordings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredAnnotatorCount",
                table: "Recordings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_CanonicalAnnotationId",
                table: "Recordings",
                column: "CanonicalAnnotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_RecordingId_AnnotatorUserId",
                table: "Annotations",
                columns: new[] { "RecordingId", "AnnotatorUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Recordings_Annotations_CanonicalAnnotationId",
                table: "Recordings",
                column: "CanonicalAnnotationId",
                principalTable: "Annotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: every recording annotated before this migration had at
            // most one annotation (the old schema enforced that 1:1), so it's
            // unambiguously that annotation's new canonical one. Without this,
            // the main export would silently start showing a blank
            // `annotation` column for previously-annotated recordings.
            migrationBuilder.Sql(
                """
                UPDATE "Recordings" r
                SET "CanonicalAnnotationId" = a."Id"
                FROM "Annotations" a
                WHERE a."RecordingId" = r."Id" AND r."CanonicalAnnotationId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recordings_Annotations_CanonicalAnnotationId",
                table: "Recordings");

            migrationBuilder.DropIndex(
                name: "IX_Recordings_CanonicalAnnotationId",
                table: "Recordings");

            migrationBuilder.DropIndex(
                name: "IX_Annotations_RecordingId_AnnotatorUserId",
                table: "Annotations");

            migrationBuilder.DropColumn(
                name: "CanonicalAnnotationId",
                table: "Recordings");

            migrationBuilder.DropColumn(
                name: "RequiredAnnotatorCount",
                table: "Recordings");

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_RecordingId",
                table: "Annotations",
                column: "RecordingId",
                unique: true);
        }
    }
}
