using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManyRooms.Infrastructure;

public sealed class ManyRoomsDbContext(DbContextOptions<ManyRoomsDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<VolunteerAssignment> Assignments => Set<VolunteerAssignment>();
    public DbSet<AssignmentResponse> Responses => Set<AssignmentResponse>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        ConfigureEntity(model.Entity<Organization>());
        model.Entity<Organization>().Property(x => x.Name).HasMaxLength(160);
        model.Entity<Organization>().Property(x => x.TimeZone).HasMaxLength(100);
        ConfigureEntity(model.Entity<User>());
        model.Entity<User>().Property(x => x.DisplayName).HasMaxLength(120);
        model.Entity<User>().Property(x => x.Email).HasMaxLength(254);
        model.Entity<User>().HasIndex(x => x.Email).IsUnique();

        ConfigureScoped(model.Entity<OrganizationMembership>());
        model.Entity<OrganizationMembership>().HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        model.Entity<OrganizationMembership>().Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        model.Entity<OrganizationMembership>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrganizationMembership>().ToTable(t =>
            t.HasCheckConstraint("CK_Memberships_Role", "\"Role\" IN ('Volunteer', 'Leader')"));

        ConfigureScoped(model.Entity<Team>());
        model.Entity<Team>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<Team>().Property(x => x.Description).HasMaxLength(1000);
        model.Entity<Team>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

        ConfigureScoped(model.Entity<TeamMember>());
        model.Entity<TeamMember>().HasIndex(x => new { x.OrganizationId, x.TeamId, x.MembershipId }).IsUnique();
        model.Entity<TeamMember>().HasOne(x => x.Team).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.TeamId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TeamMember>().HasOne(x => x.Membership).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.MembershipId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        ConfigureScoped(model.Entity<Event>());
        model.Entity<Event>().Property(x => x.Title).HasMaxLength(160);
        model.Entity<Event>().Property(x => x.Description).HasMaxLength(4000);
        model.Entity<Event>().Property(x => x.Location).HasMaxLength(240);
        model.Entity<Event>().HasIndex(x => new { x.OrganizationId, x.StartsAt });
        model.Entity<Event>().ToTable(t => t.HasCheckConstraint("CK_Events_Dates", "\"EndsAt\" > \"StartsAt\""));

        ConfigureScoped(model.Entity<VolunteerAssignment>());
        model.Entity<VolunteerAssignment>().Property(x => x.RoleName).HasMaxLength(120);
        model.Entity<VolunteerAssignment>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        model.Entity<VolunteerAssignment>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<VolunteerAssignment>().HasIndex(x => new { x.OrganizationId, x.EventId, x.TeamMemberId, x.RoleName }).IsUnique();
        model.Entity<VolunteerAssignment>().HasOne(x => x.Event).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.EventId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VolunteerAssignment>().HasOne(x => x.TeamMember).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.TeamMemberId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VolunteerAssignment>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Assignments_Status", "\"Status\" IN ('Pending', 'Accepted', 'Declined')");
            t.HasCheckConstraint("CK_Assignments_Version", "\"Version\" >= 1");
            t.HasCheckConstraint("CK_Assignments_Response", "(\"Status\" = 'Pending' AND \"RespondedAt\" IS NULL) OR (\"Status\" <> 'Pending' AND \"RespondedAt\" IS NOT NULL)");
        });

        ConfigureScoped(model.Entity<AssignmentResponse>());
        model.Entity<AssignmentResponse>().Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        model.Entity<AssignmentResponse>().HasIndex(x => new { x.OrganizationId, x.AssignmentId, x.AssignmentVersion }).IsUnique();
        model.Entity<AssignmentResponse>().HasOne<VolunteerAssignment>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AssignmentResponse>().HasOne<OrganizationMembership>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.MembershipId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AssignmentResponse>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Responses_Decision", "\"Decision\" IN ('Accepted', 'Declined')");
            t.HasCheckConstraint("CK_Responses_Version", "\"AssignmentVersion\" >= 2");
        });
    }

    private static void ConfigureEntity<T>(EntityTypeBuilder<T> entity) where T : Entity
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.UpdatedAt).IsRequired();
    }

    private static void ConfigureScoped<T>(EntityTypeBuilder<T> entity) where T : OrganizationEntity
    {
        ConfigureEntity(entity);
        entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
