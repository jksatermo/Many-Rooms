using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManyRooms.Infrastructure.Migrations;

/// <inheritdoc />
public partial class VolunteerUnavailability : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Unavailability",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Unavailability", x => x.Id);
                table.UniqueConstraint("AK_Unavailability_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.CheckConstraint("CK_Unavailability_Dates", "\"EndsAt\" > \"StartsAt\"");
                table.CheckConstraint("CK_Unavailability_Version", "\"Version\" >= 1");
                table.ForeignKey(
                    name: "FK_Unavailability_Memberships_OrganizationId_MembershipId",
                    columns: x => new { x.OrganizationId, x.MembershipId },
                    principalTable: "Memberships",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Unavailability_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Unavailability_OrganizationId_MembershipId_StartsAt",
            table: "Unavailability",
            columns: new[] { "OrganizationId", "MembershipId", "StartsAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Unavailability");
    }
}
