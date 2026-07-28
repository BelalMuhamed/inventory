using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class BatchUploadHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Batches --------------------------------------------------------------

            migrationBuilder.RenameColumn(
                name: "fileMac",
                table: "Batches",
                newName: "FileMac");

            migrationBuilder.AlterColumn<string>(
                name: "FileMac",
                table: "Batches",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Batches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ProcessingError",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int));

            // UploadStatus no longer has Pending/Processing (0=Succeeded, 1=PartialSuccess,
            // 2=Failed). Fail-safe default is Failed (2), never a silent false-positive success.
            migrationBuilder.AlterColumn<int>(
                name: "BatchStatus",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Batches",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId_FileMac",
                table: "Batches",
                columns: new[] { "UploadedByTenantId", "FileMac" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId_UploadedTime",
                table: "Batches",
                columns: new[] { "UploadedByTenantId", "UploadedTime" });

            // ---- Cards (ProductItem) ---------------------------------------------------
            // BatchId stays required. FK behavior changes from Cascade->Cascade in effect, but
            // we drop/recreate it explicitly to also drop the old unfiltered unique PAN index.

            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.AddColumn<string>(
                name: "MaskedPan",
                table: "Cards",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "EncryptedPan" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "ProductId", "BranchID", "EncryptedPan" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ---- Stocks -----------------------------------------------------------------

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_TenantId_ProductId",
                table: "Stocks",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_AvailableQuantity_NonNegative",
                table: "Stocks",
                sql: "[AvailableQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_HoldQuantity_NonNegative",
                table: "Stocks",
                sql: "[HoldQuantity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_AvailableQuantity_NonNegative",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_HoldQuantity_NonNegative",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_TenantId_ProductId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "MaskedPan",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "EncryptedPan" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropIndex(
                name: "IX_Batches_UploadedByTenantId_FileMac",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_Batches_UploadedByTenantId_UploadedTime",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Batches");

            migrationBuilder.AlterColumn<int>(
                name: "ProcessingError",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Batches",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "FileMac",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.RenameColumn(
                name: "FileMac",
                table: "Batches",
                newName: "fileMac");
        }
    }
}
