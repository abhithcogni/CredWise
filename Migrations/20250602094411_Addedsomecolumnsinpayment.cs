using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CredWise_Trail.Migrations
{
    /// <inheritdoc />
    public partial class Addedsomecolumnsinpayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InterestPaid",
                table: "LOAN_PAYMENTS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalPaid",
                table: "LOAN_PAYMENTS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterestPaid",
                table: "LOAN_PAYMENTS");

            migrationBuilder.DropColumn(
                name: "PrincipalPaid",
                table: "LOAN_PAYMENTS");
        }
    }
}
