using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CredWise_Trail.Migrations
{
    /// <inheritdoc />
    public partial class makepayment_changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountDue",
                table: "LOAN_PAYMENTS");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentOverdueAmount",
                table: "LOAN_APPLICATIONS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OverdueMonths",
                table: "LOAN_APPLICATIONS",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentOverdueAmount",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "OverdueMonths",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountDue",
                table: "LOAN_PAYMENTS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
