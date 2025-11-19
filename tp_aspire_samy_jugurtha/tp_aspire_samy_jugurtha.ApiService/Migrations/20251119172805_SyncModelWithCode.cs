using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tp_aspire_samy_jugurtha.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelWithCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_Bookings_ResourceType_ResourceId_StartUtc_EndUtc",
                table: "T_Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_T_Bookings_ResourceType_ResourceId_StartUtc_EndUtc_Status",
                table: "T_Bookings",
                columns: new[] { "ResourceType", "ResourceId", "StartUtc", "EndUtc", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_Bookings_ResourceType_ResourceId_StartUtc_EndUtc_Status",
                table: "T_Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_T_Bookings_ResourceType_ResourceId_StartUtc_EndUtc",
                table: "T_Bookings",
                columns: new[] { "ResourceType", "ResourceId", "StartUtc", "EndUtc" },
                unique: true);
        }
    }
}
