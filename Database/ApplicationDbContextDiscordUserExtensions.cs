using DiscordBotApi.Data.DiscordUsers;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Database
{
    public static class ApplicationDbContextDiscordUserExtensions
    {
        public static DiscordUser? GetUser(this ApplicationDbContext ctx, ulong id)
        {
            return ctx.DiscordUsers
                .AsNoTracking()
                .FirstOrDefault(ctx => ctx.Id == id);
        }

        public static async Task<List<DiscordUser>> GetUsers(this ApplicationDbContext ctx, CancellationToken cancellationToken)
        {
            return await ctx.DiscordUsers
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
