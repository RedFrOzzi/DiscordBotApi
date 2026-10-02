using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.RefreshTokens;
using DiscordBotApi.Database;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBotApi.Utilities
{
    public class TokenProvider(IConfiguration configuration)
    {
        readonly string _secret = Environment.GetEnvironmentVariable("SECURITY_KEY") ?? throw new("Secret was null");
        readonly IConfiguration _configuration = configuration;

        public string Create(ApiUser apiUser)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            List<Claim> claims = [new Claim(JwtRegisteredClaimNames.Sub, apiUser.Id.ToString())];
            if (apiUser.IsAdmin)
                claims.Add(new(ClaimTypes.Role, "Admin"));

            if (apiUser.IsModerator)
                claims.Add(new(ClaimTypes.Role, "Moderator"));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpirationInMinutes")),
                SigningCredentials = credentials,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var handler = new JsonWebTokenHandler();
            string token = handler.CreateToken(tokenDescriptor);

            return token;
        }

        public RefreshToken BuildRefreshToken(ApiUser apiUser, string rawToken)
        {
            var hash = Hash(rawToken);

            var rt = new RefreshToken
            {
                User = apiUser,
                TokenHash = hash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jwt:RefreshExpirationInDays"))
            };

            return rt;
        }

        public string CreateRawRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        public static string Hash(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
