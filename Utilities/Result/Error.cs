namespace DiscordBotApi.Utilities.Result;

public class Error : Result
{
    static readonly Error _instance = new();

    public Error() { }

    public Error(string message) : base(message) { }

    public static Error Empty => _instance;
}

public class Error<T> : Result<T>
{
    static readonly Error _instance = new();

    public Error() { }

    public Error(string message) : base(message) { }

    public static Error Empty => _instance;
}