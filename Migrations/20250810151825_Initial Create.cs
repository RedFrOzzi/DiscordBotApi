using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBotApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsTextChannel = table.Column<bool>(type: "INTEGER", nullable: false),
                    GuildKey = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Key);
                    table.UniqueConstraint("AK_Channels_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerPrimaryKey = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Key);
                    table.UniqueConstraint("AK_Guilds_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    PrimaryKey = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    GlobalName = table.Column<string>(type: "TEXT", nullable: true),
                    ImageURL = table.Column<string>(type: "TEXT", nullable: true),
                    DiscordGuildKey = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.PrimaryKey);
                    table.UniqueConstraint("AK_Users_Id", x => x.Id);
                    table.UniqueConstraint("AK_Users_Username", x => x.Username);
                    table.ForeignKey(
                        name: "FK_Users_Guilds_DiscordGuildKey",
                        column: x => x.DiscordGuildKey,
                        principalTable: "Guilds",
                        principalColumn: "Key");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildKey",
                table: "Channels",
                column: "GuildKey");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_OwnerPrimaryKey",
                table: "Guilds",
                column: "OwnerPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DiscordGuildKey",
                table: "Users",
                column: "DiscordGuildKey");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Guilds_GuildKey",
                table: "Channels",
                column: "GuildKey",
                principalTable: "Guilds",
                principalColumn: "Key",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Users_OwnerPrimaryKey",
                table: "Guilds",
                column: "OwnerPrimaryKey",
                principalTable: "Users",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Guilds_DiscordGuildKey",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
