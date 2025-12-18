using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Superchef.Migrations
{
    /// <inheritdoc />
    public partial class Tenth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PushAuth",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushEndpoint",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushP256dh",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "Sessions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StoreId",
                table: "Sessions",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Stores_StoreId",
                table: "Sessions",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Stores_StoreId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_StoreId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PushAuth",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PushEndpoint",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PushP256dh",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Sessions");
        }
    }
}
