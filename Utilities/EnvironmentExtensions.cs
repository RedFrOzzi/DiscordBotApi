namespace DiscordBotApi.Utilities;

public static class EnvironmentExtensions
{
    extension(Environment)
    {
        public static List<ulong> GetEnvironmentVariablesArrayAsUlong(string name)
        {
            var varStrings = Environment.GetEnvironmentVariable(name)?.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (varStrings == null || varStrings.Length == 0)
                return [];

            List<ulong> res = [];
            for (int i = 0; i < varStrings.Length; i++)
            {
                if (ulong.TryParse(varStrings[i], out var result))
                    res.Add(result);
            }

            return res;
        }
    }
}
