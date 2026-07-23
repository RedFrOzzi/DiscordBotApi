namespace DiscordBotApi.Utilities.Result;

public class InternalError : Error
{
    public InternalError() { }

    public InternalError(string message) : base(message) { }
}