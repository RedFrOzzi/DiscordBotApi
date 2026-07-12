using System.Security.Cryptography;
using System.Text;

namespace DiscordBotApi.Utilities
{
    public class PasswordHasher
    {
        private readonly byte[] _salt;

        public PasswordHasher()
        {
            var salt = Environment.GetEnvironmentVariable("PASSWORD_HASH_SALT") ?? throw new("Salt was null");
            _salt = Encoding.UTF8.GetBytes(salt);
        }

        public string GetHash(string password)
        {
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), _salt, 10000, HashAlgorithmName.SHA256, 20);
            return Convert.ToBase64String(hash);
        }

        public bool IsVarified(string password, string passwordHash)
        {
            byte[] hashBytes = Convert.FromBase64String(passwordHash);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), _salt, 10000, HashAlgorithmName.SHA256, 20);

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
