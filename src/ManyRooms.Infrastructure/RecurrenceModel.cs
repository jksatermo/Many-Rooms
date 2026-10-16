using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

public static class RecurrenceModel
{
    public static void Configure(ModelBuilder model)
    {
        var template = model.Entity<EventTemplate>();
        template.ToTable("EventTemplates", table =>
        {
            table.HasCheckConstraint("CK_EventTemplates_Day", "\"DayOfWeek\" BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_EventTemplates_Duration", "\"DurationMinutes\" BETWEEN 15 AND 720");
        });
        template.HasKey(x => x.Id);
        template.Property(x => x.Id).ValueGeneratedNever();
        template.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        template.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        template.Property(x => x.CreatedAt).IsRequired();
        template.Property(x => x.UpdatedAt).IsRequired();
        template.Property(x => x.Title).HasMaxLength(160);
        template.Property(x => x.Description).HasMaxLength(4000);
        template.Property(x => x.Location).HasMaxLength(240);
        template.Property(x => x.TimeZone).HasMaxLength(100);
        template.HasIndex(x => new { x.OrganizationId, x.StartsOn });
        model.Entity<Event>().HasOne<EventTemplate>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.TemplateId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Event>().HasIndex(x => new { x.OrganizationId, x.TemplateId, x.OccurrenceDate }).IsUnique()
            .HasFilter("\"TemplateId\" IS NOT NULL");
        model.Entity<Event>().ToTable(table => table.HasCheckConstraint("CK_Events_TemplateOccurrence",
            "(\"TemplateId\" IS NULL AND \"OccurrenceDate\" IS NULL) OR (\"TemplateId\" IS NOT NULL AND \"OccurrenceDate\" IS NOT NULL)"));
    }
}
