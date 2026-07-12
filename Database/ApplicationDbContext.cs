using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Data.Roles;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Database
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<ApiUser> ApiUsers { get; set; }
        public DbSet<DiscordGuild> Guilds { get; set; }
        public DbSet<DiscordChannel> Channels { get; set; }
        public DbSet<DiscordGuildRole> DiscordChannelRole { get; set; }
        public DbSet<DiscordUser> DiscordUsers { get; set; }

        //Raffle
        public DbSet<RaffleSettings> RaffleSettings { get; set; }
        public DbSet<Raffle> Rafles { get; set; }
        public DbSet<UserBet> UserBets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiUser>(entity =>
            {
                entity.HasAlternateKey(u => u.Id);
                entity.HasIndex(u => u.Login).IsUnique();
            });

            modelBuilder.Entity<DiscordUser>(entity =>
            {
                entity.HasIndex(u => u.Id);
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

            modelBuilder.Entity<DiscordChannel>(entity =>
            {
                entity.HasAlternateKey(c => c.Id);
            });
        }
    }
}
