namespace DiscordBotApi.Utilities.Result;

public abstract class Result
{
    protected Result()
    {
        Message = string.Empty;
    }

    protected Result(string message)
    {
        Message = message;
    }

    public string Message { get; set; }
}

public abstract class Result<T>
{
    protected Result()
    {
        Message = string.Empty;
    }

    protected Result(string message)
    {
        Message = message;
    }

    public string Message { get; set; }
}