using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramBotKit.Sample.PhotoSelector.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrowseSessions",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Filter = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceChatId = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceThreadId = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentMediaItemId = table.Column<long>(type: "INTEGER", nullable: true),
                    BrowseMessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoryMediaItemIds = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    HistoryIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowseSessions", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "SourceChats",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceChats", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GrantedByUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.TelegramUserId);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceMessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceThreadId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceSenderUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    OriginalFileId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OriginalFileUniqueId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PreviewFileId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PreviewFileUniqueId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 127, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    TelegramMessageDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_SourceChats_SourceChatId",
                        column: x => x.SourceChatId,
                        principalTable: "SourceChats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceTopics",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceTopics", x => new { x.ChatId, x.ThreadId });
                    table.ForeignKey(
                        name: "FK_SourceTopics_SourceChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "SourceChats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Selections",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Selections", x => new { x.UserId, x.MediaItemId });
                    table.ForeignKey(
                        name: "FK_Selections_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Selections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_SourceChatId_SourceMessageId",
                table: "MediaItems",
                columns: new[] { "SourceChatId", "SourceMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_SourceChatId_SourceThreadId",
                table: "MediaItems",
                columns: new[] { "SourceChatId", "SourceThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Selections_MediaItemId",
                table: "Selections",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Selections_UserId_State",
                table: "Selections",
                columns: new[] { "UserId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceChats_IsEnabled",
                table: "SourceChats",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_IsEnabled",
                table: "Users",
                columns: new[] { "Role", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowseSessions");

            migrationBuilder.DropTable(
                name: "Selections");

            migrationBuilder.DropTable(
                name: "SourceTopics");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "SourceChats");
        }
    }
}
