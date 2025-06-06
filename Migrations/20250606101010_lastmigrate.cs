using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CredWise_Trail.Migrations
{
    /// <inheritdoc />
    public partial class lastmigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.AlterColumn<int>(
                name: "LoanProductId",
                table: "LOAN_APPLICATIONS",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "LoanProductId1",
                table: "LOAN_APPLICATIONS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_APPLICATIONS_LoanProductId1",
                table: "LOAN_APPLICATIONS",
                column: "LoanProductId1");

            migrationBuilder.AddForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS",
                column: "LoanProductId",
                principalTable: "LoanProducts",
                principalColumn: "LoanProductId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId1",
                table: "LOAN_APPLICATIONS",
                column: "LoanProductId1",
                principalTable: "LoanProducts",
                principalColumn: "LoanProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId1",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropIndex(
                name: "IX_LOAN_APPLICATIONS_LoanProductId1",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "LoanProductId1",
                table: "LOAN_APPLICATIONS");

            migrationBuilder.AlterColumn<int>(
                name: "LoanProductId",
                table: "LOAN_APPLICATIONS",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LOAN_APPLICATIONS_LoanProducts_LoanProductId",
                table: "LOAN_APPLICATIONS",
                column: "LoanProductId",
                principalTable: "LoanProducts",
                principalColumn: "LoanProductId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
