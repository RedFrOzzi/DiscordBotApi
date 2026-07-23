namespace DiscordBotApi.Utilities.Result;

public class NotFoundError : Error
{
    public NotFoundError() { }

    public NotFoundError(string message) : base(message) { }
}
