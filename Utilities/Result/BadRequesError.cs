namespace DiscordBotApi.Utilities.Result;

public class BadRequesError : Error
{
    public BadRequesError() { }

    public BadRequesError(string message) : base(message) { }
}
