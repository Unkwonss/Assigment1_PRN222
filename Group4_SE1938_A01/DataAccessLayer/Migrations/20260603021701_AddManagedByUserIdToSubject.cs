using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedByUserIdToSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManagedByUserId",
                table: "Subjects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_ManagedByUserId",
                table: "Subjects",
                column: "ManagedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Users_ManagedBy",
                table: "Subjects",
                column: "ManagedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Users_ManagedBy",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_ManagedByUserId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "ManagedByUserId",
                table: "Subjects");
        }
    }
}
