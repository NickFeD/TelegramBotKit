using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramBotKit.Sample.PhotoSelector.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAndReusablePreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PreviewIsReusable",
                table: "MediaItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewIsReusable",
                table: "MediaItems");
        }
    }
}
