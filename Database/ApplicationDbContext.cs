using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.AudioPanels;
using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Data.Roles;
using DiscordBotApi.Data.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Database;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<ApiUser> ApiUsers { get; set; }
    public DbSet<DiscordGuild> Guilds { get; set; }
    public DbSet<DiscordChannel> Channels { get; set; }
    public DbSet<DiscordGuildRole> DiscordGuildRoles { get; set; }
    public DbSet<DiscordUser> DiscordUsers { get; set; }

    //Raffle
    public DbSet<Settings> Settings { get; set; }
    public DbSet<Raffle> Rafles { get; set; }
    public DbSet<UserBet> UserBets { get; set; }

    //Audio
    public DbSet<AudioTrack> AudioTracks { get; set; }
    public DbSet<AudioPanel> AudioPanels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DiscordGuild>()
            .Property(u => u.Id)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<DiscordChannel>()
            .Property(u => u.Id)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<DiscordGuildRole>()
            .Property(u => u.Id)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<DiscordUser>()
            .Property(u => u.Id)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<DiscordUser>()
            .HasMany(u => u.Guilds)
            .WithMany(g => g.Users)
            .UsingEntity(j => j.ToTable("UserGuilds"));

        modelBuilder.Entity<DiscordGuild>()
            .HasOne(g => g.Owner)
            .WithMany()
            .HasForeignKey("OwnerId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApiUser>(entity =>
        {
            entity.HasAlternateKey(u => u.Id);
            entity.HasIndex(u => u.Login).IsUnique();
        });

        modelBuilder.Entity<DiscordUser>(entity =>
        {
            entity.HasIndex(u => u.Id);
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
