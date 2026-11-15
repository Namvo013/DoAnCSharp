using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageContentType",
                table: "BOOK",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CoverImageData",
                table: "BOOK",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageContentType",
                table: "BOOK");

            migrationBuilder.DropColumn(
                name: "CoverImageData",
                table: "BOOK");
        }
    }
}
