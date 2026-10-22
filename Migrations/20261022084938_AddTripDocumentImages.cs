using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogisticsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDocumentImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CmrImageDeleteUrl",
                table: "Trips",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmrImageThumbUrl",
                table: "Trips",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmrImageUrl",
                table: "Trips",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteImageDeleteUrl",
                table: "Trips",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteImageThumbUrl",
                table: "Trips",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteImageUrl",
                table: "Trips",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImgBBApiKey",
                table: "PlatformSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CmrImageDeleteUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CmrImageThumbUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CmrImageUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteImageDeleteUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteImageThumbUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteImageUrl",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ImgBBApiKey",
                table: "PlatformSettings");
        }
    }
}
