using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tp_aspire_samy_jugurtha.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "T_Rooms",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "T_Rooms");
        }
    }
}
