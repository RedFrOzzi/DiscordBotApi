using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Utilities.Result;

namespace DiscordBotApi.Database
{
    public static class ApplicationDbContextApiUserExtensions
    {
        public static Result CreateApiUser(this ApplicationDbContext ctx, string login, string passwordHash)
        {
            var user = new ApiUser
            {
                Login = login,
                PasswordHash = passwordHash
            };

            if (ctx.ApiUsers.Any(u => u.Login == login))
            {
                return new AlreadyExistError("User alreaady exist");
            }

            ctx.ApiUsers.Add(user);
            if (ctx.SaveChanges() > 0)
            {
                return Success.Empty;
            }

            return new Error("User not saved");
        }

        public static ApiUser? GetApiUser(this ApplicationDbContext ctx, string login)
        {
            return ctx.ApiUsers.FirstOrDefault(x => x.Login == login);
        }

        public static bool FindUserAndAddAdminStatus(this ApplicationDbContext ctx, string userLogin)
        {
            var user = ctx.ApiUsers.FirstOrDefault(u => u.Login == userLogin);
            if (user == null)
                return false;

            user.IsAdmin = true;
            return ctx.SaveChanges() > 0;
        }
    }
}
