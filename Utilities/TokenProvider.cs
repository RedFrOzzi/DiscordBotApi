using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using DiscordBotApi.Data.ApiUsers;

namespace DiscordBotApi.Utilities
{
    public class TokenProvider(IConfiguration configuration)
    {
        private readonly string _secret = Environment.GetEnvironmentVariable("SECURITY_KEY") ?? throw new("Secret was null");

        public string Create(ApiUser apiUser)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            List<Claim> claims = [new Claim(JwtRegisteredClaimNames.Sub, apiUser.Id.ToString())];
            if (apiUser.IsAdmin)
            {
                claims.Add(new(ClaimTypes.Role, "Admin"));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:ExpirationInMinutes")),
                SigningCredentials = credentials,
                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"]
            };

            var handler = new JsonWebTokenHandler();
            string token = handler.CreateToken(tokenDescriptor);

            return token;
        }
    }
}
