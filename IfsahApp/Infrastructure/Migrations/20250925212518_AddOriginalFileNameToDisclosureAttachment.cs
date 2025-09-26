using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsahApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalFileNameToDisclosureAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "DisclosureAttachments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureAttachments_DisclosureId_UploadedAt",
                table: "DisclosureAttachments",
                columns: new[] { "DisclosureId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DisclosureAttachments_DisclosureId_UploadedAt",
                table: "DisclosureAttachments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "DisclosureAttachments");
        }
    }
}
