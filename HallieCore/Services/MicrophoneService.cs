namespace Hallie.Services
{
    #region interface pour microphone 
    public interface IMicrophoneService
    {
        bool IsMicOn { get; }
        void Enable();
        void Disable();
    }
    #endregion

    #region Service qui implémente le microphone
    public class MicrophoneService : IMicrophoneService
    {
        private readonly ISpeechToText _sst;

        public bool IsMicOn { get; private set; }

        public MicrophoneService(ISpeechToText sst)
        {
            _sst = sst;
        }

        public void Enable()
        {
            _sst.StartAsync();
            IsMicOn = true;
        }

        public void Disable()
        {
            _sst.StopAsync();
            IsMicOn = false;
        }
    }
    #endregion
}
