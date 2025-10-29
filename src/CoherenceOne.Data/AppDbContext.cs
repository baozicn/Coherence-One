using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using CoherenceOne.Core.Models;
using System.Linq;

namespace CoherenceOne.Data;

public class AppDbContext : DbContext
{
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<TimelineEvent> Timeline => Set<TimelineEvent>();
    public DbSet<JournalNote> Journal => Set<JournalNote>();
    public DbSet<Session> Sessions => Set<Session>();

    private readonly string? _dbPassword;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(string databasePath, string? password = null)
    {
        _dbPassword = password;
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoherenceOne", "app.db");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            var connectionString = $"Data Source={DatabasePath}";
            if (!string.IsNullOrEmpty(_dbPassword))
            {
                connectionString += $";Password={_dbPassword}";
            }
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v) ?? new());
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<Scene>(entity =>
        {
            entity.ToTable("Scenes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LaunchApps).HasConversion(stringListConverter).Metadata.SetValueComparer(stringListComparer);
            entity.Property(x => x.Whitelist).HasConversion(stringListConverter).Metadata.SetValueComparer(stringListComparer);
            entity.Property(x => x.PomodoroDefaults)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v) ? new PomodoroDefaults(1500, 300, 900) : JsonSerializer.Deserialize<PomodoroDefaults>(v) ?? new PomodoroDefaults(1500, 300, 900));
        });

        modelBuilder.Entity<TimelineEvent>(entity =>
        {
            entity.ToTable("Timeline");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Ts);
            entity.Property(x => x.Type).IsRequired();
            entity.Property(x => x.PayloadJson);
        });

        modelBuilder.Entity<JournalNote>(entity =>
        {
            entity.ToTable("Journal");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Date).HasConversion(
                v => v.ToString("O"),
                v => DateOnly.Parse(v));
            entity.Property(x => x.Tags).HasConversion(stringListConverter);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("Sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Date).HasConversion(
                v => v.ToString("O"),
                v => DateOnly.Parse(v));
        });

        SeedScenes(modelBuilder);
    }

    private static void SeedScenes(ModelBuilder modelBuilder)
    {
        var scenes = new[]
        {
            new Scene
            {
                Id = "writing",
                Name = "写作",
                LaunchApps = new() { "notepad" },
                Whitelist = new() { "notepad", "CoherenceOne" },
                PomodoroDefaults = new PomodoroDefaults(1500, 300, 900)
            },
            new Scene
            {
                Id = "study",
                Name = "学习",
                LaunchApps = new() { "notepad" },
                Whitelist = new() { "notepad", "CoherenceOne" },
                PomodoroDefaults = new PomodoroDefaults(1500, 300, 900)
            },
            new Scene
            {
                Id = "meeting",
                Name = "会议",
                LaunchApps = new() { "msteams" },
                Whitelist = new() { "msteams", "CoherenceOne" },
                PomodoroDefaults = new PomodoroDefaults(1500, 300, 900)
            }
        };

        foreach (var scene in scenes)
        {
            modelBuilder.Entity<Scene>().HasData(scene);
        }
    }
}
