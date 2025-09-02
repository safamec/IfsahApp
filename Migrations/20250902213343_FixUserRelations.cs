using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsahApp.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisclosureAssignments_Users_UserId",
                table: "DisclosureAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Disclosures_Users_SubmittedById",
                table: "Disclosures");

            migrationBuilder.DropIndex(
                name: "IX_DisclosureAssignments_UserId",
                table: "DisclosureAssignments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DisclosureAssignments");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Disclosures",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Disclosures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisclosureId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Disclosures_DisclosureId",
                        column: x => x.DisclosureId,
                        principalTable: "Disclosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Disclosures_AssignedToUserId",
                table: "Disclosures",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DisclosureId",
                table: "Comments",
                column: "DisclosureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disclosures_Users_AssignedToUserId",
                table: "Disclosures",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Disclosures_Users_SubmittedById",
                table: "Disclosures",
                column: "SubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disclosures_Users_AssignedToUserId",
                table: "Disclosures");

            migrationBuilder.DropForeignKey(
                name: "FK_Disclosures_Users_SubmittedById",
                table: "Disclosures");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Disclosures_AssignedToUserId",
                table: "Disclosures");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Disclosures");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Disclosures",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "DisclosureAssignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureAssignments_UserId",
                table: "DisclosureAssignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DisclosureAssignments_Users_UserId",
                table: "DisclosureAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Disclosures_Users_SubmittedById",
                table: "Disclosures",
                column: "SubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
