using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzmCrm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRules_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_AssignedToUserId",
                table: "AssignmentRules",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_IsActive_EvaluationOrder",
                table: "AssignmentRules",
                columns: new[] { "IsActive", "EvaluationOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentRules");
        }
    }
}
