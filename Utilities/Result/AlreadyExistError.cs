namespace DiscordBotApi.Utilities.Result
{
    public class AlreadyExistError : Error
    {
        public AlreadyExistError() { }

        public AlreadyExistError(string message) : base(message) { }
    }
}
