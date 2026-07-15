using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashAndWeeklyTokenLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklyTokenLimit",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 250000);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeeklyTokenLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Documents");
        }
    }
}
