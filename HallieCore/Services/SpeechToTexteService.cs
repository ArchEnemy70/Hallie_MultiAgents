using HallieDomain;

namespace Hallie.Services
{
    #region Interface pour la reconnaissance vocale
    public interface ISpeechToText
    {
        event Action? SpeechStarted;
        event Action? SpeechEnded;

        //event Action<string>? PartialResult;
        event Action<(string, ChatConversation)>? FinalResult;

        event Action<float>? AudioLevelDetected;

        // événement pour la détection de mot-clé d'interruption
        event Action? InterruptKeywordDetected;

        Task StartAsync();
        Task StopAsync();

        // méthodes pour gérer le mode mot-clé
        void EnableKeywordOnlyMode();
        void DisableKeywordOnlyMode();
    }
    #endregion

}
