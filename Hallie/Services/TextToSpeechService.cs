using ExternalServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using HallieDomain;

namespace Hallie.Services
{
    #region Service qui implémente la synthèse vocale (Piper)
    public class PiperTtsService : ITextToSpeech
    {
        public event Action? SpeechStarted;
        public event Action? SpeechEnded;
        public event Action<float>? AudioLevel;

        private readonly string _piperExe;
        private readonly string _voiceModelPath;

        private WaveOutEvent? _output;
        private AudioFileReader? _reader;
        private CancellationTokenSource? _playCts;

        public PiperTtsService()
        {
            string pathPiper = Path.Combine(AppContext.BaseDirectory, "piper");
            _piperExe = @$"{pathPiper}\piper.exe";
            _voiceModelPath = @$"{pathPiper}\voices\{Params.TTS_Piper_Voice}.onnx";
        }

        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            Stop();

            // 1) Génère wav temporaire
            var tempWav = Path.Combine(Path.GetTempPath(), $"piper_{Guid.NewGuid():N}.wav");
            var b = await RunPiperAsync(text, tempWav, ct);
            if (!b)
            {
                return;
            }

            // 2) Lecture via NAudio + niveau audio
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _reader = new AudioFileReader(tempWav); // convertit en float en interne
            var meter = new MeteringSampleProvider(_reader.ToSampleProvider(), 100);
            meter.StreamVolume += (s, e) =>
            {
                // e.MaxSampleValues[0] ~ [0..1] (mono ou stéréo)
                var level = e.MaxSampleValues.Length > 0 ? e.MaxSampleValues[0] : 0f;
                AudioLevel?.Invoke(level);
            };

            var waveProvider = meter.ToWaveProvider(); // back to IWaveProvider
            _output = new WaveOutEvent();
            _output.Init(waveProvider);

            SpeechStarted?.Invoke();

            _output.Play();

            // Attendre fin lecture
            while (_output.PlaybackState == PlaybackState.Playing && !_playCts.IsCancellationRequested)
                await Task.Delay(25, _playCts.Token).ConfigureAwait(false); // 25 : compromis entre réactivité et charge CPU

            // Nettoyage
            CleanupPlayback();

            // Supprimer wav
            TryDelete(tempWav);

            SpeechEnded?.Invoke();
        }

        public void Stop()
        {
            try
            {
                _playCts?.Cancel();
            }
            catch { /* ignore */ }

            CleanupPlayback();
            AudioLevel?.Invoke(0f);
        }

        private void CleanupPlayback()
        {
            try
            {
                _output?.Stop();
            }
            catch { /* ignore */ }

            _output?.Dispose();
            _output = null;

            _reader?.Dispose();
            _reader = null;

            _playCts?.Dispose();
            _playCts = null;
        }

        private async Task<bool> RunPiperAsync(string text, string outputWavPath, CancellationToken ct)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _piperExe,
                    Arguments = $"-m \"{_voiceModelPath}\" -f \"{outputWavPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,

                    StandardInputEncoding = System.Text.Encoding.UTF8,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var p = new Process { StartInfo = psi };

                if (!p.Start())
                {
                    LoggerService.LogError($"RunPiperAsync --> Impossible de démarrer piper.exe");
                    return false;
                }

                var stdErrTask = p.StandardError.ReadToEndAsync();
                var stdOutTask = p.StandardOutput.ReadToEndAsync();

                await p.StandardInput.WriteAsync(text.AsMemory(), ct);
                await p.StandardInput.WriteLineAsync(); // fin input
                p.StandardInput.Close();

                try
                {
                    await p.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // Annulation : tuer le process (sinon zombie possible)
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    throw;
                }

                var err = await stdErrTask;
                var outp = await stdOutTask;

                if (p.ExitCode != 0)
                {
                    LoggerService.LogError($"RunPiperAsync --> Piper a échoué (exit {p.ExitCode}). STDERR: {err}");
                    if (!string.IsNullOrWhiteSpace(outp))
                        LoggerService.LogError($"RunPiperAsync --> STDOUT: {outp}");
                    return false;
                }

                if (!File.Exists(outputWavPath) || new FileInfo(outputWavPath).Length < 1000)
                {
                    LoggerService.LogError($"RunPiperAsync --> WAV non généré ou vide. STDERR: {err}");
                    if (!string.IsNullOrWhiteSpace(outp))
                        LoggerService.LogError($"RunPiperAsync --> STDOUT: {outp}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"RunPiperAsync: {ex.Message}");
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }
    #endregion

    #region Service qui implémente la synthèse vocale (Microsoft)
    public class TextToSpeechService : ITextToSpeech
    {
        #region Events
        public event Action<float>? AudioLevel;
        public event Action? SpeechStarted;
        public event Action? SpeechEnded;
        #endregion

        #region Variables
        private WaveOutEvent? _output;
        private CancellationTokenSource? _currentCts;
        private readonly object _lock = new object();
        #endregion

        #region Méthodes publiques
        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            LoggerService.LogDebug($"WindowsTtsService.SpeakAsync : {text}");

            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                // Arrêter toute lecture en cours
                Stop();

                lock (_lock)
                {
                    _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                }

                SpeechStarted?.Invoke();

                using var synth = new SpeechSynthesizer();
                synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                synth.Rate = 2; // 0 : Vitesse normale

                using var stream = new MemoryStream();
                synth.SetOutputToWaveStream(stream);
                synth.Speak(text);

                stream.Position = 0;

                using var reader = new WaveFileReader(stream);
                var sampleProvider = reader.ToSampleProvider();

                var meter = new MeteringSampleProvider(sampleProvider);
                meter.StreamVolume += (_, e) =>
                {
                    if (e.MaxSampleValues.Length > 0)
                        AudioLevel?.Invoke(Math.Abs(e.MaxSampleValues[0]));
                };

                lock (_lock)
                {
                    if (_currentCts?.Token.IsCancellationRequested ?? true)
                        return;

                    _output = new WaveOutEvent();
                    _output.Init(meter);
                    _output.Play();
                }

                // Attendre la fin de la lecture
                while (_output?.PlaybackState == PlaybackState.Playing)
                {
                    if (_currentCts?.Token.IsCancellationRequested ?? true)
                    {
                        Stop();
                        throw new OperationCanceledException();
                    }
                    await Task.Delay(50, _currentCts?.Token ?? CancellationToken.None);
                }

                SpeechEnded?.Invoke();
            }
            catch (OperationCanceledException)
            {
                LoggerService.LogError("[TTS] Lecture interrompue");
                throw;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[TTS] Erreur: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_lock)
                {
                    _output?.Dispose();
                    _output = null;
                }
            }
        }

        public void Stop()
        {
            LoggerService.LogDebug("WindowsTtsService.Stop");

            lock (_lock)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _currentCts = null;

                if (_output != null)
                {
                    _output.Stop();
                    _output.Dispose();
                    _output = null;
                }
            }
        }
        #endregion

    }
    #endregion
}
