using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsahApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumnToDisclosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Audit",
                table: "Disclosures",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audit",
                table: "Disclosures");
        }
    }
}
