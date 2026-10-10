using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManyRooms.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialScheduling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organizations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Location = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Events", x => x.Id);
                table.UniqueConstraint("AK_Events_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.CheckConstraint("CK_Events_Dates", "\"EndsAt\" > \"StartsAt\"");
                table.ForeignKey(
                    name: "FK_Events_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Teams",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.Id);
                table.UniqueConstraint("AK_Teams_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.ForeignKey(
                    name: "FK_Teams_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Memberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Memberships", x => x.Id);
                table.UniqueConstraint("AK_Memberships_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.CheckConstraint("CK_Memberships_Role", "\"Role\" IN ('Volunteer', 'Leader')");
                table.ForeignKey(
                    name: "FK_Memberships_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Memberships_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TeamMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamMembers", x => x.Id);
                table.UniqueConstraint("AK_TeamMembers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.ForeignKey(
                    name: "FK_TeamMembers_Memberships_OrganizationId_MembershipId",
                    columns: x => new { x.OrganizationId, x.MembershipId },
                    principalTable: "Memberships",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TeamMembers_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TeamMembers_Teams_OrganizationId_TeamId",
                    columns: x => new { x.OrganizationId, x.TeamId },
                    principalTable: "Teams",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Assignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                TeamMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Assignments", x => x.Id);
                table.UniqueConstraint("AK_Assignments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.CheckConstraint("CK_Assignments_Response", "(\"Status\" = 'Pending' AND \"RespondedAt\" IS NULL) OR (\"Status\" <> 'Pending' AND \"RespondedAt\" IS NOT NULL)");
                table.CheckConstraint("CK_Assignments_Status", "\"Status\" IN ('Pending', 'Accepted', 'Declined')");
                table.CheckConstraint("CK_Assignments_Version", "\"Version\" >= 1");
                table.ForeignKey(
                    name: "FK_Assignments_Events_OrganizationId_EventId",
                    columns: x => new { x.OrganizationId, x.EventId },
                    principalTable: "Events",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Assignments_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Assignments_TeamMembers_OrganizationId_TeamMemberId",
                    columns: x => new { x.OrganizationId, x.TeamMemberId },
                    principalTable: "TeamMembers",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Responses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                AssignmentVersion = table.Column<int>(type: "integer", nullable: false),
                RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Responses", x => x.Id);
                table.UniqueConstraint("AK_Responses_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.CheckConstraint("CK_Responses_Decision", "\"Decision\" IN ('Accepted', 'Declined')");
                table.CheckConstraint("CK_Responses_Version", "\"AssignmentVersion\" >= 2");
                table.ForeignKey(
                    name: "FK_Responses_Assignments_OrganizationId_AssignmentId",
                    columns: x => new { x.OrganizationId, x.AssignmentId },
                    principalTable: "Assignments",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Responses_Memberships_OrganizationId_MembershipId",
                    columns: x => new { x.OrganizationId, x.MembershipId },
                    principalTable: "Memberships",
                    principalColumns: new[] { "OrganizationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Responses_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Assignments_OrganizationId_EventId_TeamMemberId_RoleName",
            table: "Assignments",
            columns: new[] { "OrganizationId", "EventId", "TeamMemberId", "RoleName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Assignments_OrganizationId_TeamMemberId",
            table: "Assignments",
            columns: new[] { "OrganizationId", "TeamMemberId" });

        migrationBuilder.CreateIndex(
            name: "IX_Events_OrganizationId_StartsAt",
            table: "Events",
            columns: new[] { "OrganizationId", "StartsAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Memberships_OrganizationId_UserId",
            table: "Memberships",
            columns: new[] { "OrganizationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Memberships_UserId",
            table: "Memberships",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Responses_OrganizationId_AssignmentId_AssignmentVersion",
            table: "Responses",
            columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersion" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Responses_OrganizationId_MembershipId",
            table: "Responses",
            columns: new[] { "OrganizationId", "MembershipId" });

        migrationBuilder.CreateIndex(
            name: "IX_TeamMembers_OrganizationId_MembershipId",
            table: "TeamMembers",
            columns: new[] { "OrganizationId", "MembershipId" });

        migrationBuilder.CreateIndex(
            name: "IX_TeamMembers_OrganizationId_TeamId_MembershipId",
            table: "TeamMembers",
            columns: new[] { "OrganizationId", "TeamId", "MembershipId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Teams_OrganizationId_Name",
            table: "Teams",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Responses");

        migrationBuilder.DropTable(
            name: "Assignments");

        migrationBuilder.DropTable(
            name: "Events");

        migrationBuilder.DropTable(
            name: "TeamMembers");

        migrationBuilder.DropTable(
            name: "Memberships");

        migrationBuilder.DropTable(
            name: "Teams");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropTable(
            name: "Organizations");
    }
}
