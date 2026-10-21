using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.TelegramBotImages;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DiscordBotApi.Controllers;

[ApiController]
public class TelegramMiniAppController(
    IConfiguration configuration,
    ApplicationDbContext context,
    PasswordHasher passwordHasher,
    TokenProvider tokenProvider) : ControllerBase
{
    readonly IConfiguration _configuration = configuration;
    readonly ApplicationDbContext _context = context;
    readonly PasswordHasher _passwordHasher = passwordHasher;
    readonly TokenProvider _tokenProvider = tokenProvider;

    [HttpPost("telegram-login")]
    public IActionResult TelegramLogin([FromBody] TelegramLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InitData))
            return BadRequest("Missing initData");

        if (!ValidateTelegramData(dto.InitData, out var userId, out var username))
            return Unauthorized("Invalid Telegram data");

        if (string.IsNullOrWhiteSpace(username))
            username = "anon";

        var searchName = username + "_tguser_" + userId;

        var apiUser = _context.ApiUsers
            .FirstOrDefault(u => u.Login == searchName);

        if (apiUser == null)
        {
            var newUser = new ApiUser()
            {
                Login = searchName,
                PasswordHash = _passwordHasher.GetHash(userId + username),
                IsTelegramUser = true,
            };

            _context.ApiUsers.Add(newUser);
            _context.SaveChanges();
            apiUser = newUser;
        }

        var token = _tokenProvider.Create(apiUser);
        var rawRefreshToken = _tokenProvider.CreateRawRefreshToken();
        var refreshToken = _tokenProvider.BuildRefreshToken(apiUser, rawRefreshToken);

        _context.RefreshTokens.Add(refreshToken);
        _context.SaveChanges();

        Response.Cookies.Append(
            _configuration["Jwt:RefreshCookieName"] ?? "dba_refresh",
            rawRefreshToken,
            AuthCookies.RefreshCookieOptions());

        return Ok(token);
    }



    private static bool ValidateTelegramData(string initData, out long userId, out string username)
    {
        userId = 0; username = "";

        var dataDict = new Dictionary<string, string>();
        var dataCheckStringList = new List<string>();

        foreach (var item in initData.Split('&'))
        {
            var parts = item.Split('=');
            if (parts.Length != 2)
                continue;

            var key = parts[0];
            var value = Uri.UnescapeDataString(parts[1]);
            dataDict[key] = value;

            if (key != "hash")
                dataCheckStringList.Add($"{key}={value}");
        }

        if (!dataDict.TryGetValue("hash", out var hash))
            return false;

        dataCheckStringList.Sort();
        var dataCheckString = string.Join("\n", dataCheckStringList);

        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? ""));

        var computedHash = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var computedHashHex = Convert.ToHexString(computedHash).ToLower();

        if (!computedHashHex.Equals(hash, StringComparison.OrdinalIgnoreCase))
            return false;

        if (dataDict.TryGetValue("user", out var userJson))
        {
            var user = JsonSerializer.Deserialize<JsonElement>(userJson);
            userId = user.GetProperty("id").GetInt64();
            if (user.TryGetProperty("username", out var u))
                username = u.GetString() ?? "";
            else
                username = "";

            return true;
        }

        return false;
    }
}
