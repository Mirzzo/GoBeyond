using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoBeyond.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Postojeće prijave ostaju važeće: svaki refresh token je zasebna sesija i vezan je za (upravo dodijeljen) stamp
            // korisnika, pa aplikacije jednim refresh-om dobiju access token sa novim tvrdnjama.
            migrationBuilder.Sql("""
                UPDATE r
                SET r.SessionId = NEWID(), r.SecurityStamp = u.SecurityStamp
                FROM RefreshTokens AS r
                INNER JOIN Users AS u ON u.Id = r.UserId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");
        }
    }
}
