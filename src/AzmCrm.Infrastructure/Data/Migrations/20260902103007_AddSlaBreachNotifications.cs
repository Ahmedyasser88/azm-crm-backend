using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzmCrm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaBreachNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaBreachNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BreachType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NotifiedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SlaBreachNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaBreachNotifications_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaBreachNotifications_NotifiedUserId",
                table: "SlaBreachNotifications",
                column: "NotifiedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaBreachNotifications_TicketId",
                table: "SlaBreachNotifications",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaBreachNotifications");
        }
    }
}
