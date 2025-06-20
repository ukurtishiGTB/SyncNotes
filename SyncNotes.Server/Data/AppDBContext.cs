using Microsoft.EntityFrameworkCore;
using SyncNotes.Shared.Models;

namespace SyncNotes.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<WhiteboardElement> WhiteboardElements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure Note
            modelBuilder.Entity<Note>()
                .HasKey(n => n.Id);

            modelBuilder.Entity<Note>()
                .Property(n => n.Content)
                .IsRequired();

            modelBuilder.Entity<Note>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.LastModifiedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure WhiteboardElement
            modelBuilder.Entity<WhiteboardElement>()
                .HasKey(w => w.Id);

            modelBuilder.Entity<WhiteboardElement>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(w => w.LastModifiedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<WhiteboardElement>()
                .OwnsMany(e => e.Points, b =>
                {
                    b.WithOwner().HasForeignKey("WhiteboardElementId");
                    b.Property<int>("Id"); // Needed for collection tracking
                    b.HasKey("Id");
                });
            
            modelBuilder.Entity<WhiteboardElement>()
                .OwnsMany(e => e.Points, b =>
                {
                    b.WithOwner().HasForeignKey("WhiteboardElementId");
                    b.Property<int>("Id");
                    b.HasKey("Id");
                });

            modelBuilder.Entity<User>()
                .HasMany(u => u.Notes)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.OwnerId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.WhiteboardElements)
                .WithOne(w => w.User)
                .HasForeignKey(w => w.OwnerId);
            modelBuilder.Entity<Note>()
                .Property(n => n.SharedWith)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            modelBuilder.Entity<WhiteboardElement>()
                .Property(w => w.SharedWith)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Friendship>()
                .HasKey(f => f.Id);

            modelBuilder.Entity<Friendship>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Friendship>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Friendship>()
                .HasIndex(f => new { f.UserId, f.FriendId })
                .IsUnique();

        }
    }
}