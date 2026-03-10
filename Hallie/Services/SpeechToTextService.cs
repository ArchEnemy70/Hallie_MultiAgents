using ExternalServices;
using Hallie.ViewModels;
using NAudio.Wave;
using System.IO;
using Whisper.net;
using HallieDomain;

namespace Hallie.Services
{

    #region Service qui implémente la reconnaissance vocale
    public class SpeechToTextService : ISpeechToText
    {
        #region Events
        public event Action? SpeechStarted;
        public event Action? SpeechEnded;
        //public event Action<string>? PartialResult;
        public event Action<(string, ChatConversation)>? FinalResult;
        public event Action<float>? AudioLevelDetected;
        public event Action? InterruptKeywordDetected; // Nouveau événement
        #endregion

        #region Propriétés
        public bool IsListening { get; private set; }
        #endregion

        #region Variables privées
        private readonly WhisperFactory _factory;
        private readonly string _modelPath;
        private bool _isRunning;

        private WaveInEvent? _waveIn;
        private readonly List<byte> _buffer = [];
        private DateTime _lastUserSpeechTime = DateTime.MinValue;
        private bool _isUserSpeaking = false;
        // Mode d'écoute : Normal ou OnlyKeywords
        private bool _keywordOnlyMode = false;

        #endregion

        #region Constantes
        // Seuil plus élevé pour éviter de détecter la voix du TTS
        private const int SILENCE_THRESHOLD = 1500; // Augmenté de 500 à 1500
        private const double SILENCE_DURATION_SEC = 1.5;

        // Mots-clés d'interruption (en minuscules)
        private readonly string[] _interruptKeywords = new[]
        {
            "ok stop",
            "okay stop",
            "stop",
            "arrête",
            "arrête",
            "arrête toi",
            "tais-toi",
            "silence"
        };
        #endregion

        #region Constructeur
        public SpeechToTextService()
        {
            string model = Params.WHisperModele!;// "ggml-base.bin";// "ggml-medium.bin"; 
            _modelPath = Path.Combine(AppContext.BaseDirectory, "Models", model);
            _factory = WhisperFactory.FromPath(_modelPath);
        }
        #endregion

        #region Méthodes publiques
        public Task StartAsync()
        {
            LoggerService.LogDebug("SpeechToTextService.StartAsync");
            IsListening = true;

            if (_isRunning)
                return Task.CompletedTask;

            _isRunning = true;
            _buffer.Clear();

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 30//100
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();

            _ = Task.Run(CheckSilenceLoop);

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            LoggerService.LogDebug("SpeechToTextService.StopAsync");

            IsListening = false;

            _isRunning = false;
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
            _buffer.Clear();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Active le mode où seuls les mots-clés sont détectés (pendant que le TTS parle)
        /// </summary>
        public void EnableKeywordOnlyMode()
        {
            LoggerService.LogDebug("SpeechToTextService.EnableKeywordOnlyMode : Mode mot-clé uniquement activé");
            _keywordOnlyMode = true;
            _buffer.Clear(); // Reset buffer
        }

        /// <summary>
        /// Désactive le mode mot-clé (écoute normale)
        /// </summary>
        public void DisableKeywordOnlyMode()
        {
            LoggerService.LogDebug("SpeechToTextService.DisableKeywordOnlyMode : Mode écoute normale activé");
            _keywordOnlyMode = false;
            _buffer.Clear(); // Reset buffer
        }
        #endregion

        #region Méthodes privées
        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isRunning)
                return;

            _buffer.AddRange(e.Buffer.Take(e.BytesRecorded));

            double rms = CalculateRMS(e.Buffer, e.BytesRecorded);

            AudioLevelDetected?.Invoke((float)rms);

            if (rms > SILENCE_THRESHOLD)
            {
                if (!_isUserSpeaking)
                {
                    _isUserSpeaking = true;
                    SpeechStarted?.Invoke();
                }
                _lastUserSpeechTime = DateTime.Now;
            }
        }

        private async Task CheckSilenceLoop()
        {
            while (_isRunning)
            {
                await Task.Delay(100);

                if (_isUserSpeaking && (DateTime.Now - _lastUserSpeechTime).TotalSeconds > SILENCE_DURATION_SEC)
                {
                    _isUserSpeaking = false;
                    SpeechEnded?.Invoke();

                    if (_buffer.Count > 16000)
                    {
                        var segment = _buffer.ToArray();
                        _buffer.Clear();
                        _ = Task.Run(() => TranscribeAudioSegmentAsync(segment));
                    }
                    else
                    {
                        _buffer.Clear();
                    }
                }
            }
        }

        private static double CalculateRMS(byte[] buffer, int length)
        {
            double sum = 0;
            int sampleCount = length / 2;

            for (int i = 0; i < length - 1; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sum += sample * sample;
            }

            return Math.Sqrt(sum / sampleCount);
        }

        private async Task TranscribeAudioSegmentAsync(byte[] buffer)
        {
            try
            {
                if (buffer.Length < 16000)
                    return;

                using var processor = _factory.CreateBuilder()
                    .WithLanguage("fr")
                    .Build();

                using var ms = PcmToWav(buffer);

                var transcription = new System.Text.StringBuilder();

                await foreach (var result in processor.ProcessAsync(ms))
                {
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        var text = result.Text.Trim();
                        if (text != "[BLANK_AUDIO]" && text != "[Musique]" && text != "[Music]")
                        {
                            transcription.Append(result.Text);
                        }
                    }
                }

                var finalText = transcription.ToString().Trim();
                if (!string.IsNullOrEmpty(finalText))
                {
                    LoggerService.LogDebug($"SpeechToTextService.TranscribeAudioSegmentAsync --> Transcription: {finalText} (Mode: {(_keywordOnlyMode ? "Mot-clé" : "Normal")})");

                    // Si on est en mode mot-clé uniquement, vérifier la présence d'un mot-clé
                    if (_keywordOnlyMode)
                    {
                        if (ContainsInterruptKeyword(finalText))
                        {
                            LoggerService.LogDebug($"SpeechToTextService.TranscribeAudioSegmentAsync --> Mot-clé d'interruption détecté: {finalText}");
                            InterruptKeywordDetected?.Invoke();
                        }
                        else
                        {
                            LoggerService.LogDebug($"SpeechToTextService.TranscribeAudioSegmentAsync --> Ignoré (pas de mot-clé): {finalText}");
                        }
                    }
                    else
                    {
                        // Mode normal : envoyer le résultat
                        var conv = new ChatConversation();
                        if (AvatarViewModel.SelectedConversation != null)
                            conv = AvatarViewModel.SelectedConversation!;
                        FinalResult?.Invoke((finalText, conv));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SpeechToTextService.TranscribeAudioSegmentAsync --> Erreur: {ex.Message}");
            }
        }

        private bool ContainsInterruptKeyword(string text)
        {
            var lowerText = text.ToLowerInvariant();

            foreach (var keyword in _interruptKeywords)
            {
                if (lowerText.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static MemoryStream PcmToWav(byte[] pcm, int sampleRate = 16000, int channels = 1, int bitsPerSample = 16)
        {
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int subChunk2Size = pcm.Length;

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + subChunk2Size);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)(channels * bitsPerSample / 8));
                bw.Write((short)bitsPerSample);

                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(subChunk2Size);
                bw.Write(pcm);
            }

            ms.Position = 0;
            return ms;
        }
        #endregion
    }
    #endregion
}