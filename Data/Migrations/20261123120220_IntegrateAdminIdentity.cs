using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateAdminIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Identity reuses the existing Users columns; only EF's model snapshot changes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No physical schema change was required.
        }
    }
}
