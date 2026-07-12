using DiscordBotApi.Data.DiscordUsers.Dtos;
using NetCord;
using NetCord.Rest;

namespace DiscordBotApi.Data.DiscordUsers
{
    public static class UserExtensions
    {
        public static IEnumerable<DiscordUserGetDto> ConvertToDto(this IEnumerable<DiscordUser> users)
        {
            foreach (var user in users)
            {
                yield return new DiscordUserGetDto()
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    Nickname = user.Nickname,
                    GlobalName = user.GlobalName,
                    ImageURL = user.ImageURL,
                    UserResource = user.UserSpendingResource,
                };
            }
        }

        public static DiscordUserGetDto ConverToDto(this DiscordUser user)
        {
            return new()
            {
                Id = user.Id.ToString(),
                GlobalName = user.GlobalName,
                Username = user.Username,
                Nickname = user.Nickname,
                ImageURL= user.ImageURL,
                UserResource = user.UserSpendingResource,
            };
        }

        public static DiscordUserGetDto ConvertToDiscordUser(this GuildUser user, RestGuild guild)
        {
            return new()
            {
                Id = user.Id.ToString(),
                Username = user.Username,
                Nickname = user.Nickname,
                GlobalName = user.GlobalName,
                ImageURL = user.HasAvatar ? user.GetAvatarUrl()?.ToString() : null,
            };
        }

        public static DiscordUser ConvertToDiscordUser(this GuildUser user)
        {
            return new()
            {
                Id = user.Id,
                Username = user.Username,
                Nickname = user.Nickname,
                GlobalName = user.GlobalName,
                ImageURL = null,
            };
        }
    }
}
