using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<VersionRecord> Versions => Set<VersionRecord>();
    public DbSet<VersionFile> VersionFiles => Set<VersionFile>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ReleaseRecord> Releases => Set<ReleaseRecord>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<StorageSettings> StorageSettings => Set<StorageSettings>();
    public DbSet<StorageInstance> StorageInstances => Set<StorageInstance>();

    IQueryable<Project> IAppDbContext.Projects => Projects;
    IQueryable<VersionRecord> IAppDbContext.Versions => Versions;
    IQueryable<VersionFile> IAppDbContext.VersionFiles => VersionFiles;
    IQueryable<Channel> IAppDbContext.Channels => Channels;
    IQueryable<ReleaseRecord> IAppDbContext.Releases => Releases;
    IQueryable<User> IAppDbContext.Users => Users;
    IQueryable<ApiKey> IAppDbContext.ApiKeys => ApiKeys;
    IQueryable<StorageSettings> IAppDbContext.StorageSettings => StorageSettings;
    IQueryable<StorageInstance> IAppDbContext.StorageInstances => StorageInstances;

    void IAppDbContext.Add<T>(T entity) => Set<T>().Add(entity);
    void IAppDbContext.Remove<T>(T entity) => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.StorageInstance).WithMany().HasForeignKey(x => x.StorageInstanceId);
            e.HasMany(x => x.Versions).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId);
            e.HasMany(x => x.Channels).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId);
            e.HasMany(x => x.Releases).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId);
        });

        modelBuilder.Entity<StorageInstance>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.LocalRoot).HasMaxLength(1024);
            e.Property(x => x.Prefix).HasMaxLength(256);
            e.Property(x => x.ServiceUrl).HasMaxLength(512);
            e.Property(x => x.Bucket).HasMaxLength(256);
        });

        modelBuilder.Entity<VersionRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();
            e.HasMany(x => x.Files).WithOne(x => x.Version!).HasForeignKey(x => x.VersionId);
        });

        modelBuilder.Entity<VersionFile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RelativePath).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.VersionId, x.RelativePath });
        });

        modelBuilder.Entity<Channel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ReleaseRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChannelName).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        modelBuilder.Entity<StorageSettings>(e =>
        {
            e.HasKey(x => x.Id);
        });

        // Store all DateTimeOffset properties as UTC Unix milliseconds (long).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(DateTimeConverters.ToUnixMs);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(DateTimeConverters.ToUnixMsNullable);
            }
        }
    }
}
