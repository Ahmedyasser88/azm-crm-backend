using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzmCrm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedOn",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEscalated",
                table: "Tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsEscalated",
                table: "Tickets",
                column: "IsEscalated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsEscalated",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedOn",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsEscalated",
                table: "Tickets");
        }
    }
}
