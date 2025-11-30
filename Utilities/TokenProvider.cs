using DiscordBotApi.Data.Users;
using DiscordBotApi.DiscordBot.Services.Secrets;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DiscordBotApi.Utilities
{
    public class TokenProvider
    {
        private readonly IConfiguration _configuration;
        private readonly string _secret;

        public TokenProvider(IConfiguration configuration, string secret)
        {
            _configuration = configuration;
            _secret = secret;
        }

        public string Create(ApiUser apiUser)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            List<Claim> claims = [new Claim(JwtRegisteredClaimNames.Sub, apiUser.ApiUserId.ToString())];
            if (apiUser.IsAdmin)
            {
                claims.Add(new(ClaimTypes.Role, "Admin"));
            }

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
    }
}
