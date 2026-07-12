namespace DiscordBotApi.Utilities.Result;

public sealed class Success<T> : Result
{
    public Success() { }

    public Success(string message) : base(message) { }

    public Success(string message, T value) : base(message) 
    {
        Value = value;
    }

    public T? Value { get; set; }
}

public sealed class Success : Result
{
    static readonly Success _instance = new();

    public Success() { }

    public Success(string message) : base(message) { }

    public static Success Empty => _instance;
}