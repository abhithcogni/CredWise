using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CredWise_Trail.Migrations
{
    /// <inheritdoc />
    public partial class ChangestoKYCDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KYC_APPROVAL_Admin_ApprovedByAdminId",
                table: "KYC_APPROVAL");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "KYC_APPROVAL");

            migrationBuilder.RenameColumn(
                name: "ApprovedByAdminId",
                table: "KYC_APPROVAL",
                newName: "AdminId");

            migrationBuilder.RenameColumn(
                name: "KycApprovalId",
                table: "KYC_APPROVAL",
                newName: "KycID");

            migrationBuilder.RenameIndex(
                name: "IX_KYC_APPROVAL_ApprovedByAdminId",
                table: "KYC_APPROVAL",
                newName: "IX_KYC_APPROVAL_AdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_KYC_APPROVAL_Admin_AdminId",
                table: "KYC_APPROVAL",
                column: "AdminId",
                principalTable: "Admin",
                principalColumn: "AdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KYC_APPROVAL_Admin_AdminId",
                table: "KYC_APPROVAL");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "KYC_APPROVAL",
                newName: "ApprovedByAdminId");

            migrationBuilder.RenameColumn(
                name: "KycID",
                table: "KYC_APPROVAL",
                newName: "KycApprovalId");

            migrationBuilder.RenameIndex(
                name: "IX_KYC_APPROVAL_AdminId",
                table: "KYC_APPROVAL",
                newName: "IX_KYC_APPROVAL_ApprovedByAdminId");

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "KYC_APPROVAL",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_KYC_APPROVAL_Admin_ApprovedByAdminId",
                table: "KYC_APPROVAL",
                column: "ApprovedByAdminId",
                principalTable: "Admin",
                principalColumn: "AdminId");
        }
    }
}
