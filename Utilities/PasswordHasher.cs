using DiscordBotApi.DiscordBot.Services.Secrets;
using NetCord;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DiscordBotApi.Utilities
{
    public class PasswordHasher
    {
        private readonly byte[] _salt;

        public PasswordHasher()
        {
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
            {
                throw new Exception($"secrets not found in base directory {AppDomain.CurrentDomain.BaseDirectory}");
            }
            else
            {
                var secretsJson = JsonSerializer.Deserialize<SecretsJson>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
                    ?? throw new Exception($"could not deserialize json file");

                if (secretsJson.Salt == null)
                    throw new Exception($"secrets file does not contain token");

                _salt = Encoding.UTF8.GetBytes(secretsJson.Salt);
            }
        }

        public string GetHash(string password)
        {
            var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), _salt, 10000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(20);
            return Convert.ToBase64String(hash);
        }

        public bool IsVarified(string password, string passwordHash)
        {
            byte[] hashBytes = Convert.FromBase64String(passwordHash);
            var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), _salt, 10000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(20);

            for (int i = 0; i < hash.Length; i++)
            {
                if (hash[i] != hashBytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
