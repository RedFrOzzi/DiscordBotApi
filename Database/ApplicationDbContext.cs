using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Data.Users;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<ApiUser> ApiUsers { get; set; }
        public DbSet<DiscordGuild> Guilds { get; set; }
        public DbSet<DiscordChannel> Channels { get; set; }
        public DbSet<DiscordUser> Users { get; set; }

        //Raffle
        public DbSet<Raffle> Rafles { get; set; }
        public DbSet<UserBet> UserBets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiUser>(entity =>
            {
                entity.HasAlternateKey(u => u.ApiUserId);
            });

            modelBuilder.Entity<DiscordUser>(entity =>
            {
                entity.HasAlternateKey(u => u.Id);
                entity.HasAlternateKey(u => u.Username);
            });

            modelBuilder.Entity<DiscordGuild>(entity =>
            {
                entity.HasAlternateKey(g =>  g.Id);
            });

            modelBuilder.Entity<DiscordChannel>(entity =>
            {
                entity.HasAlternateKey(c => c.Id);
            });
        }
    }
}
