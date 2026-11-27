using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameTechnicianStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TechnicianProfiles_VerificationStatus",
                table: "TechnicianProfiles");

            migrationBuilder.RenameColumn(
                name: "VerificationStatus",
                table: "TechnicianProfiles",
                newName: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TechnicianProfiles_Status",
                table: "TechnicianProfiles",
                sql: "[Status] IN (N'Pending', N'Approved', N'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TechnicianProfiles_Status",
                table: "TechnicianProfiles");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "TechnicianProfiles",
                newName: "VerificationStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TechnicianProfiles_VerificationStatus",
                table: "TechnicianProfiles",
                sql: "[VerificationStatus] IN (N'Pending', N'Approved', N'Rejected')");
        }
    }
}
