using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAdditionalCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TechnicianCategories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "TechnicianCategories",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "TechnicianCategories");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "TechnicianCategories");
        }
    }
}
