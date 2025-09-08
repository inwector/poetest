using Microsoft.EntityFrameworkCore;
using PoETest.API.Models;

namespace PoETest.API.Data
{
    public class PoETestContext : DbContext
    {
        public PoETestContext(DbContextOptions<PoETestContext> options) : base(options) { }

        public DbSet<Item> Items { get; set; }
        public DbSet<ItemType> ItemTypes { get; set; }
        public DbSet<Modifier> Modifiers { get; set; }
        public DbSet<LeaderboardEntry> Leaderboard { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Item>()
                .HasOne(i => i.Type)
                .WithMany(t => t.Items)
                .HasForeignKey(i => i.TypeId);

            modelBuilder.Entity<Modifier>()
                .HasOne(m => m.Item)
                .WithMany(i => i.Modifiers)
                .HasForeignKey(m => m.ItemId);
        }
    }
}
