using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>The Events module's context — a pure mapper over the SQL-owned schema in <c>Migrations/</c>, on snake_case columns.</summary>
/// <remarks>
///   - internal: consumers reach the store through the Events brokers only, never its tables
///   - enumerations are text holding the contract's camelCase labels; structured facts are jsonb mapped as strings and
///     read through <c>EventsJson</c>
/// </remarks>
/// <param name="options">The context options, configured by <c>AddEvents</c>.</param>
internal sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : AppDbContextBase(options)
{
    public DbSet<CatalogEntity> Catalogs => Set<CatalogEntity>();
    public DbSet<CatalogEventEntity> Events => Set<CatalogEventEntity>();
    public DbSet<EventDeadlineEntity> Deadlines => Set<EventDeadlineEntity>();
    public DbSet<EventSourceEntity> Sources => Set<EventSourceEntity>();
    public DbSet<ObservationEntity> Observations => Set<ObservationEntity>();
    public DbSet<SourcePostEntity> SourcePosts => Set<SourcePostEntity>();
    public DbSet<IntakeCursorEntity> IntakeCursors => Set<IntakeCursorEntity>();
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<TrackingEntity> Tracking => Set<TrackingEntity>();
    public DbSet<TrackingHistoryEntity> TrackingHistory => Set<TrackingHistoryEntity>();
    public DbSet<ImportMapEntity> ImportMap => Set<ImportMapEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(EventsModuleConstants.SchemaName);

        var catalog = modelBuilder.Entity<CatalogEntity>();
        catalog.ToTable("events_catalogs");
        catalog.HasKey(row => row.Id);
        catalog.Property(row => row.Slug).HasMaxLength(64);
        catalog.Property(row => row.Name).HasMaxLength(200);
        catalog.Property(row => row.OwnerIssuer).HasMaxLength(512);
        catalog.Property(row => row.OwnerOrganizationId).HasMaxLength(128);
        catalog.Property(row => row.Visibility).HasMaxLength(16);
        catalog.Property(row => row.Locales).HasColumnType("jsonb");
        catalog.Property(row => row.Kinds).HasColumnType("jsonb");
        catalog.Property(row => row.Revision).IsConcurrencyToken();

        var item = modelBuilder.Entity<CatalogEventEntity>();
        item.ToTable("events_catalog_events");
        item.HasKey(row => row.Id);
        item.Property(row => row.Key).HasMaxLength(64);
        item.Property(row => row.Review).HasMaxLength(16);
        item.Property(row => row.Facts).HasColumnType("jsonb");
        item.Property(row => row.Kind).HasMaxLength(64);
        item.Property(row => row.Status).HasMaxLength(16);
        item.Property(row => row.Country).HasMaxLength(2);
        item.Property(row => row.Revision).IsConcurrencyToken();

        var deadline = modelBuilder.Entity<EventDeadlineEntity>();
        deadline.ToTable("events_event_deadlines");
        deadline.HasKey(row => new { row.EventId, row.Ordinal });
        deadline.Property(row => row.Kind).HasMaxLength(16);

        var source = modelBuilder.Entity<EventSourceEntity>();
        source.ToTable("events_event_sources");
        source.HasKey(row => new { row.CatalogId, row.Kind, row.SourceId });
        source.Property(row => row.Kind).HasMaxLength(16);
        source.Property(row => row.SourceId).HasMaxLength(256);

        var observation = modelBuilder.Entity<ObservationEntity>();
        observation.ToTable("events_observations");
        observation.HasKey(row => row.Id);
        observation.Property(row => row.Seq).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        observation.Property(row => row.SourceKind).HasMaxLength(16);
        observation.Property(row => row.SourceInstance).HasMaxLength(128);
        observation.Property(row => row.SubscriptionId).HasMaxLength(64);
        observation.Property(row => row.PipelineId).HasMaxLength(64);
        observation.Property(row => row.ItemId).HasMaxLength(128);
        observation.Property(row => row.Type).HasMaxLength(64);
        observation.Property(row => row.Reason).HasMaxLength(32);
        observation.Property(row => row.MappingVersion).HasMaxLength(64);
        observation.Property(row => row.Links).HasColumnType("jsonb");
        observation.Property(row => row.Payload).HasColumnType("jsonb");
        observation.Property(row => row.Proposed).HasColumnType("jsonb");
        observation.Property(row => row.State).HasMaxLength(16);
        observation.Property(row => row.ResolvedByIssuer).HasMaxLength(512);
        observation.Property(row => row.ResolvedBySubject).HasMaxLength(256);

        var post = modelBuilder.Entity<SourcePostEntity>();
        post.ToTable("events_source_posts");
        post.HasKey(row => new { row.SourceInstance, row.SubscriptionId, row.PostId });
        post.Property(row => row.SourceInstance).HasMaxLength(128);
        post.Property(row => row.SubscriptionId).HasMaxLength(64);

        var cursor = modelBuilder.Entity<IntakeCursorEntity>();
        cursor.ToTable("events_intake_cursors");
        cursor.HasKey(row => row.SourceName);
        cursor.Property(row => row.SourceName).HasMaxLength(64);
        cursor.Property(row => row.SubscriptionId).HasMaxLength(64);

        var workflow = modelBuilder.Entity<WorkflowEntity>();
        workflow.ToTable("events_workflows");
        workflow.HasKey(row => new { row.Issuer, row.OrganizationId });
        workflow.Property(row => row.Issuer).HasMaxLength(512);
        workflow.Property(row => row.OrganizationId).HasMaxLength(128);
        workflow.Property(row => row.Stages).HasColumnType("jsonb");
        workflow.Property(row => row.DefaultStage).HasMaxLength(64);
        workflow.Property(row => row.Fields).HasColumnType("jsonb");
        workflow.Property(row => row.Revision).IsConcurrencyToken();

        var tracking = modelBuilder.Entity<TrackingEntity>();
        tracking.ToTable("events_tracking");
        tracking.HasKey(row => row.Id);
        tracking.Property(row => row.Issuer).HasMaxLength(512);
        tracking.Property(row => row.OrganizationId).HasMaxLength(128);
        tracking.Property(row => row.Stage).HasMaxLength(64);
        tracking.Property(row => row.Priority).HasMaxLength(8);
        tracking.Property(row => row.Assignee).HasMaxLength(256);
        tracking.Property(row => row.Fields).HasColumnType("jsonb");
        tracking.Property(row => row.Refs).HasColumnType("jsonb");
        tracking.Property(row => row.Revision).IsConcurrencyToken();

        var history = modelBuilder.Entity<TrackingHistoryEntity>();
        history.ToTable("events_tracking_history");
        history.HasKey(row => row.Id);
        history.Property(row => row.FromStage).HasMaxLength(64);
        history.Property(row => row.ToStage).HasMaxLength(64);
        history.Property(row => row.ActorIssuer).HasMaxLength(512);
        history.Property(row => row.ActorSubject).HasMaxLength(256);

        var map = modelBuilder.Entity<ImportMapEntity>();
        map.ToTable("events_import_map");
        map.HasKey(row => new { row.CatalogId, row.Issuer, row.OrganizationId, row.LegacyId });
        map.Property(row => row.Issuer).HasMaxLength(512);
        map.Property(row => row.OrganizationId).HasMaxLength(128);
        map.Property(row => row.LegacyId).HasMaxLength(256);
        map.Property(row => row.Source).HasMaxLength(128);

        base.OnModelCreating(modelBuilder);
    }
}
