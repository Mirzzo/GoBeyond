using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoBeyond.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgressEntries_ClientProfileId",
                table: "ProgressEntries");

            migrationBuilder.AddColumn<string>(
                name: "PlanSnapshotJson",
                table: "ProgressEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingPlanId",
                table: "ProgressEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingTypeId",
                table: "MentorProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "ClientProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TrainingExperience",
                table: "ClientProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientProfileId = table.Column<int>(type: "int", nullable: false),
                    TrainingPlanId = table.Column<int>(type: "int", nullable: false),
                    DayPlanId = table.Column<int>(type: "int", nullable: false),
                    Repetitions = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_ClientProfiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "ClientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_DayPlans_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "DayPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_TrainingPlans_TrainingPlanId",
                        column: x => x.TrainingPlanId,
                        principalTable: "TrainingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_ClientProfileId_Year_Month",
                table: "ProgressEntries",
                columns: new[] { "ClientProfileId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_TrainingPlanId",
                table: "ProgressEntries",
                column: "TrainingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorProfiles_TrainingTypeId",
                table: "MentorProfiles",
                column: "TrainingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_SentAt",
                table: "OutboxMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_UserId",
                table: "OutboxMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_ClientProfileId_DayPlanId",
                table: "TrainingSessions",
                columns: new[] { "ClientProfileId", "DayPlanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_DayPlanId",
                table: "TrainingSessions",
                column: "DayPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TrainingPlanId",
                table: "TrainingSessions",
                column: "TrainingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingTypes_Name",
                table: "TrainingTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorProfiles_TrainingTypes_TrainingTypeId",
                table: "MentorProfiles",
                column: "TrainingTypeId",
                principalTable: "TrainingTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressEntries_TrainingPlans_TrainingPlanId",
                table: "ProgressEntries",
                column: "TrainingPlanId",
                principalTable: "TrainingPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorProfiles_TrainingTypes_TrainingTypeId",
                table: "MentorProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressEntries_TrainingPlans_TrainingPlanId",
                table: "ProgressEntries");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "TrainingTypes");

            migrationBuilder.DropIndex(
                name: "IX_ProgressEntries_ClientProfileId_Year_Month",
                table: "ProgressEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProgressEntries_TrainingPlanId",
                table: "ProgressEntries");

            migrationBuilder.DropIndex(
                name: "IX_MentorProfiles_TrainingTypeId",
                table: "MentorProfiles");

            migrationBuilder.DropColumn(
                name: "PlanSnapshotJson",
                table: "ProgressEntries");

            migrationBuilder.DropColumn(
                name: "TrainingPlanId",
                table: "ProgressEntries");

            migrationBuilder.DropColumn(
                name: "TrainingTypeId",
                table: "MentorProfiles");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "TrainingExperience",
                table: "ClientProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_ClientProfileId",
                table: "ProgressEntries",
                column: "ClientProfileId");
        }
    }
}
