namespace HallieDomain
{
    public class ConversationMessage
    {
        public string Id { get; set; } = "";
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        //public bool? IsGoodTool { get; set; } = null;
        //public string Why { get; set; } = "";
        //public string Feedback { get; set; } = "";
        public string Tool { get; set; } = "";
    }
    public class ChatConversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "LLM";
        public string Title { get; set; } = $"Présentation du {DateTime.Now.ToShortDateString()}";
        public string TitleLong { get; set; } = $"Présentation du {DateTime.Now.ToShortDateString()}";
        public DateTime LastUse { get; set; } = DateTime.Now;
        public List<ConversationMessage> Messages { get; set; } = new();

    }
}
