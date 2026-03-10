namespace Hallie.Services
{
    #region Interface pour la synthèse vocale
    public interface ITextToSpeech
    {
        event Action<float>? AudioLevel;   // 0.0 → 1.0
        event Action? SpeechStarted;
        event Action? SpeechEnded;

        Task SpeakAsync(string text, CancellationToken ct = default);
        void Stop();

    }
    #endregion

}
