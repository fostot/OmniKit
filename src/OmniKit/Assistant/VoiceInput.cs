using System.Globalization;
using System.Speech.Recognition;

namespace OmniKit.Assistant;

/// <summary>
/// Explicit, local Windows speech input. Each Start captures at most one utterance.
/// No recognition callback touches game services, executes actions, or saves audio.
/// </summary>
internal sealed class VoiceInput : IDisposable
{
    private const float MinimumConfidence = 0.65f;
    private readonly object sync = new();
    private CancellationTokenSource? cancellation;
    private string? result;
    private string status = "Voice input is idle.";
    private bool running;
    private bool disposed;

    public string Status { get { lock (sync) return status; } }
    public bool IsListening { get { lock (sync) return running && cancellation is { IsCancellationRequested: false }; } }

    public VoiceInput() { }

    public void Start(IEnumerable<string> customPhrases)
    {
        // Snapshot caller-owned collections before moving to the recognition worker.
        var phrases = customPhrases.Where(phrase => !string.IsNullOrWhiteSpace(phrase)
                && phrase.Length <= 96 && !phrase.Any(char.IsControl))
            .Select(phrase => phrase.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(300).ToArray();

        lock (sync)
        {
            if (disposed || running)
                return;
            result = null;
            if (!OperatingSystem.IsWindows())
            {
                status = "Voice input requires Windows speech recognition.";
                return;
            }
            running = true;
            status = "Starting the microphone for one phrase…";
            cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var session = cancellation;
            _ = Task.Run(() => RecognizeOneAsync(phrases, session));
        }
    }

    public void Cancel()
    {
        lock (sync)
        {
            result = null;
            if (disposed)
                return;
            status = running ? "Stopping voice input…" : "Voice input is idle.";
            // Cancellation is observed by the worker; no COM work runs on the game thread.
            cancellation?.Cancel();
        }
    }

    public bool TryTakeResult(out string text)
    {
        lock (sync)
        {
            text = result ?? string.Empty;
            result = null;
            return !disposed && text.Length != 0;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            result = null;
            status = "Voice input is off.";
            cancellation?.Cancel();
            // The active worker owns and disposes its recognizer and token source.
        }
    }

    private async Task RecognizeOneAsync(string[] phrases, CancellationTokenSource session)
    {
        string? accepted = null;
        var outcome = "No clear phrase was heard. Try again.";
        SpeechRecognitionEngine? engine = null;
        try
        {
            session.Token.ThrowIfCancellationRequested();
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            var selected = recognizers.FirstOrDefault(info => info.Culture.Equals(CultureInfo.CurrentUICulture))
                ?? recognizers.FirstOrDefault(info => info.Culture.TwoLetterISOLanguageName == "en")
                ?? recognizers.FirstOrDefault();
            if (selected == null)
            {
                outcome = "Install a Windows speech recognition language to use voice input.";
                return;
            }

            engine = new SpeechRecognitionEngine(selected)
            {
                InitialSilenceTimeout = TimeSpan.FromSeconds(4),
                BabbleTimeout = TimeSpan.FromSeconds(7),
                EndSilenceTimeout = TimeSpan.FromMilliseconds(650),
                EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(1),
            };
            engine.LoadGrammar(new DictationGrammar { Name = "Search dictation" });
            if (phrases.Length != 0)
            {
                var grammar = new GrammarBuilder(new Choices(phrases)) { Culture = selected.Culture };
                engine.LoadGrammar(new Grammar(grammar) { Name = "Local command phrases" });
            }

            var completed = new TaskCompletionSource<RecognitionResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            engine.RecognizeCompleted += (_, args) =>
            {
                if (args.Error != null)
                    completed.TrySetException(new InvalidOperationException("Speech recognition failed."));
                else if (args.Cancelled)
                    completed.TrySetCanceled();
                else
                    completed.TrySetResult(args.Result);
            };

            session.Token.ThrowIfCancellationRequested();
            engine.SetInputToDefaultAudioDevice();
            session.Token.ThrowIfCancellationRequested();
            lock (sync)
                if (!disposed && !session.IsCancellationRequested)
                    status = "Listening for one phrase…";

            session.Token.ThrowIfCancellationRequested();
            engine.RecognizeAsync(RecognizeMode.Single);
            var recognized = await completed.Task.WaitAsync(session.Token).ConfigureAwait(false);
            if (recognized is { Confidence: >= MinimumConfidence } && !string.IsNullOrWhiteSpace(recognized.Text)
                && recognized.Text.Length <= 280)
            {
                accepted = recognized.Text.Trim();
                outcome = "Phrase recognized. Microphone stopped.";
            }
            else if (recognized != null)
                outcome = "That phrase was not clear enough. Try again.";
        }
        catch (OperationCanceledException)
        {
            outcome = "Voice input stopped. Press the voice button to try again.";
        }
        catch (Exception)
        {
            // Native exception text can include device/user details; keep it out of logs/UI.
            outcome = "Voice input is unavailable. Check the microphone and Windows speech language.";
        }
        finally
        {
            if (engine != null)
            {
                try { engine.RecognizeAsyncCancel(); } catch (Exception) { }
                try { engine.Dispose(); } catch (Exception) { }
            }
            lock (sync)
            {
                if (!disposed)
                {
                    if (!session.IsCancellationRequested)
                        result = accepted;
                    status = session.IsCancellationRequested ? "Voice input stopped. Press the voice button to try again." : outcome;
                }
                running = false;
                cancellation = null;
                session.Dispose();
            }
        }
    }
}
