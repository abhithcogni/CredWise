using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CredWise_Trail.Migrations
{
    /// <inheritdoc />
    public partial class paymentdbchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanApplications_Customers_CustomerId",
                table: "LoanApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoanApplications_LoanProducts_LoanProductId",
                table: "LoanApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_Repayments_LoanApplications_ApplicationId",
                table: "Repayments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoanApplications",
                table: "LoanApplications");

            migrationBuilder.RenameTable(
                name: "LoanApplications",
                newName: "LOAN_APPLICATIONS");

            migrationBuilder.RenameIndex(
                name: "IX_LoanApplications_LoanProductId",
                table: "LOAN_APPLICATIONS",
                newName: "IX_LOAN_APPLICATIONS_LoanProductId");

            migrationBuilder.RenameIndex(
                name: "IX_LoanApplications_CustomerId",
                table: "LOAN_APPLICATIONS",
                newName: "IX_LOAN_APPLICATIONS_CustomerId");

            migrationBuilder.AlterColumn<decimal>(
                name: "LoanAmount",
                table: "LOAN_APPLICATIONS",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountDue",
                table: "LOAN_APPLICATIONS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EMI",
                table: "LOAN_APPLICATIONS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "LOAN_APPLICATIONS",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentDate",
                table: "LOAN_APPLICATIONS",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoanNumber",
                table: "LOAN_APPLICATIONS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LoanStatus",
                table: "LOAN_APPLICATIONS",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextDueDate",
                table: "LOAN_APPLICATIONS",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingBalance",
                table: "LOAN_APPLICATIONS",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TenureMonths",
                table: "LOAN_APPLICATIONS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LOAN_APPLICATIONS",
                table: "LOAN_APPLICATIONS",
                column: "ApplicationId");

            migrationBuilder.CreateTable(
                name: "LOAN_PAYMENTS",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOAN_PAYMENTS", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_LOAN_PAYMENTS_LOAN_APPLICATIONS_LoanId",
                        column: x => x.LoanId,
                        principalTable: "LOAN_APPLICATIONS",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_PAYMENTS_LoanId",
                table: "LOAN_PAYMENTS",
                column: "LoanId");

            migrationBuilder.AddForeignKey(
                name: "FK_LOAN_APPLICATIONS_Customers_CustomerId",
                table: "LOAN_APPLICATIONS",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS",
                column: "LoanProductId",
                principalTable: "LoanProducts",
                principalColumn: "LoanProductId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repayments_LOAN_APPLICATIONS_ApplicationId",
                table: "Repayments",
                column: "ApplicationId",
                principalTable: "LOAN_APPLICATIONS",
                principalColumn: "ApplicationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LOAN_APPLICATIONS_Customers_CustomerId",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_Repayments_LOAN_APPLICATIONS_ApplicationId",
                table: "Repayments");

            migrationBuilder.DropTable(
                name: "LOAN_PAYMENTS");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LOAN_APPLICATIONS",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "AmountDue",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "EMI",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "LastPaymentDate",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "LoanNumber",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "LoanStatus",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "NextDueDate",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "OutstandingBalance",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "TenureMonths",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.RenameTable(
                name: "LOAN_APPLICATIONS",
                newName: "LoanApplications");

            migrationBuilder.RenameIndex(
                name: "IX_LOAN_APPLICATIONS_LoanProductId",
                table: "LoanApplications",
                newName: "IX_LoanApplications_LoanProductId");

            migrationBuilder.RenameIndex(
                name: "IX_LOAN_APPLICATIONS_CustomerId",
                table: "LoanApplications",
                newName: "IX_LoanApplications_CustomerId");

            migrationBuilder.AlterColumn<decimal>(
                name: "LoanAmount",
                table: "LoanApplications",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoanApplications",
                table: "LoanApplications",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoanApplications_Customers_CustomerId",
                table: "LoanApplications",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanApplications_LoanProducts_LoanProductId",
                table: "LoanApplications",
                column: "LoanProductId",
                principalTable: "LoanProducts",
                principalColumn: "LoanProductId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repayments_LoanApplications_ApplicationId",
                table: "Repayments",
                column: "ApplicationId",
                principalTable: "LoanApplications",
                principalColumn: "ApplicationId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
