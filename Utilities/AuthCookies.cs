namespace DiscordBotApi.Utilities;

public static class AuthCookies
{
    public static CookieOptions RefreshCookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/users",
        Expires = expires
    };
}
