namespace HallieDomain
{

    public class AiOrchestrorModel
    {
        public string UserInput { get; set; } = "";
        public bool IsShowUserInput { get; set; } = false;
        public ChatConversation? SelectedConversation { get; set; } = new();
        public bool IsSaveConv { get; set; } = true;
    }
}
