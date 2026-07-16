using DiscordBotApi.Data.Guilds;
using NetCord;

namespace DiscordBotApi.Data.Roles;

public static class DiscordGuildRoleExtensions
{
    public static List<DiscordGuildRole> Convert(this IReadOnlyList<Role>? roles, DiscordGuild guild)
    {
        List<DiscordGuildRole> res = [];

        if (roles == null || roles.Count == 0)
            return res;

        for (int i = 0; i < roles.Count; i++)
        {
            DiscordGuildRole dbRole = new()
            {
                Id = roles[i].Id,
                Name = roles[i].Name,
                DiscordGuild = guild,
            };

            res.Add(dbRole);
        }

        return res;
    }
}
