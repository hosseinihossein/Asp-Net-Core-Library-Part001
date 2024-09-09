using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportTicket.Migrations.SupportTicket_Db
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageGuid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromUserGuid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToUsersGuids = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToAll = table.Column<bool>(type: "bit", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachedFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeenByClientsGuids = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}
