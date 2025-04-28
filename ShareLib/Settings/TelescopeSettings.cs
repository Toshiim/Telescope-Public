namespace ShareLib.Settings
{
    public class AppSettings
    {
        public SQLConnectionStrings SQLConnectionStrings { get; set; }
        public TelegramBot TelegramBot { get; set; }
        public TelegramUserApi TelegramUserApi { get; set; }
        public Qdrant Qdrant { get; set; }
        public Ollama Ollama { get; set; }
    }

    public class SQLConnectionStrings
    {
        public string DefaultConnection { get; set; }
    }

    public class TelegramBot
    {
        public string BotToken { get; set; }
    }

    public class TelegramUserApi
    {
        public int ApiId { get; set; }
        public string ApiHash { get; set; }
    }

    public class Qdrant
    {
        public string CollectionName { get; set; }
        public ulong VectorSize { get; set; }
        public string ConnectionString { get; set; }
    }

    public class Ollama
    {
        public string Model { get; set; }
        public string ConnectionString { get; set; }
    }

}
