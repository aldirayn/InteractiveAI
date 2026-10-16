using InteractiveAI.Backend.Models.DB;
using Microsoft.EntityFrameworkCore;
using InteractiveAI.Backend.Models.DB;

namespace InteractiveAI.Backend.Data;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New chat";
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatSession? Session { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ChatSession> Sessions => Set<ChatSession>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<VehicleUnit> VehicleUnits => Set<VehicleUnit>();
    public DbSet<DriverPairing> DriverPairings => Set<DriverPairing>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ChatSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Model).HasMaxLength(100);
        });
        b.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne(x => x.Session).WithMany(s => s.Messages)
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SessionId);
        });
        b.Entity<VehicleUnit>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NopolNorm).HasMaxLength(50);
            e.HasIndex(x => x.NopolNorm).IsUnique();
        });
        b.Entity<DriverPairing>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NopolNorm).HasMaxLength(50);
            e.HasIndex(x => x.NopolNorm);
        });
    }
}
