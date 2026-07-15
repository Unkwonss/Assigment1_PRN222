using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndMomo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchasedTokenBalance",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchasedTokenExpiry",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionPackages",
                columns: table => new
                {
                    PackageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExtraTokenAmount = table.Column<int>(type: "int", nullable: false),
                    DurationUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Month"),
                    DurationValue = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPackages", x => x.PackageId);
                });

            migrationBuilder.CreateTable(
                name: "UserTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentGateway = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "MoMo"),
                    TransactionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_UserTransactions_SubscriptionPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "SubscriptionPackages",
                        principalColumn: "PackageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTransactions_PackageId",
                table: "UserTransactions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTransactions_UserId",
                table: "UserTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTransactions");

            migrationBuilder.DropTable(
                name: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "PurchasedTokenBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PurchasedTokenExpiry",
                table: "Users");
        }
    }
}
