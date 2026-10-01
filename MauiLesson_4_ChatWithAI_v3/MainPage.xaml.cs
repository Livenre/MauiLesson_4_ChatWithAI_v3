using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using CommunityToolkit.Maui.Media;
using Plugin.Maui.Audio;
using System.Text.Json;
using System.Net.Http.Headers;

namespace MauiLesson_4_ChatWithAI_v3;

public partial class MainPage : ContentPage
{
    private readonly AiService _aiService = new();
    private readonly IAudioRecorder _audioRecorder;
    private const string GroqApiKey = "";
    // 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
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();

    public List<AiModel> AvailableModels { get; set; } = new()
    {
        new AiModel { Name = "Автовыбор (OpenRouter Free)", Id = "openrouter/free" },
        new AiModel { Name = "Meta Llama 3.2 3B (Free)", Id = "openrouter/free" },
        new AiModel { Name = "Google Gemma 2 9B (Free)", Id = "openrouter/free" },
        new AiModel { Name = "Mistral 7B (Free)", Id = "openrouter/free" }
    };

    public MainPage()
    {
        InitializeComponent();
        _audioRecorder = AudioManager.Current.CreateRecorder();

        ChatList.ItemsSource = Messages;

        ModelPicker.ItemsSource = AvailableModels;
        ModelPicker.SelectedIndex = 0;

#if WINDOWS
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("EnterKeyHandling", (handler, view) =>
        {
            if (view is Editor editor && editor == MessageInput)
            {
                handler.PlatformView.PreviewKeyDown += (sender, args) =>
                {
                    if (args.Key == Windows.System.VirtualKey.Enter)
                    {
                        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                        bool isShiftDown = shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

                        if (!isShiftDown)
                        {
                            args.Handled = true;
                            if (SendButton.IsEnabled)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    OnSendClicked(this, EventArgs.Empty);
                                });
                            }
                        }
                    }
                };
            }
        });
#endif
    }

    private void OnMessageInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageInput.Text);
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = MessageInput.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var selectedModel = (ModelPicker.SelectedItem as AiModel)?.Id ?? "openrouter/free";

        Messages.Add(new ChatMessage { Text = text, IsUser = true });
        MessageInput.Text = string.Empty;
        ScrollToBottom();

        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        MessageInput.IsEnabled = false;
        SendButton.IsEnabled = false;
        MicButton.IsEnabled = false;

        try
        {
            var response = await _aiService.AskAi(text, selectedModel);
            Messages.Add(new ChatMessage { Text = response, IsUser = false });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Text = $"Помилка: {ex.Message}", IsUser = false });
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            MessageInput.IsEnabled = true;
            MicButton.IsEnabled = true;
            MessageInput.Focus();
            ScrollToBottom();
        }
    }

    private async void ScrollToBottom()
    {
        if (Messages.Count > 0)
        {
            await Task.Delay(100);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ChatList.ScrollTo(Messages.Last(), position: ScrollToPosition.End, animate: true);
            });
        }
    }

    private async void OnMicClicked(object? sender, EventArgs e)
    {
        var isGranted = await CheckAndRequestMicrophonePermission();
        if (!isGranted)
        {
            await this.DisplayAlertAsync("Помилка", "Немає доступу до мікрофона!", "OK");
            return;
        }

        if (_audioRecorder.IsRecording)
        {
            MicButton.Text = "⧖";
            MicButton.IsEnabled = false;

            try
            {
                var audioSource = await _audioRecorder.StopAsync();
                using var audioStream = audioSource.GetAudioStream();

                var transcribedText = await TranscribeAudioWithGroqAsync(audioStream);

                if (!string.IsNullOrWhiteSpace(transcribedText))
                {
                    MessageInput.Text = transcribedText;
                    OnSendClicked(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                await this.DisplayAlertAsync("Помилка", $"Помилка розпізнавання: {ex.Message}", "OK");
            }
            finally
            {
                MicButton.Text = "🎙";
                MicButton.IsEnabled = true;
            }
        }
        else
        {
            await _audioRecorder.StartAsync();
            MicButton.Text = "⭕";
        }
    }

    private async Task<string> TranscribeAudioWithGroqAsync(Stream audioStream)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GroqApiKey);

        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        content.Add(streamContent, "file", "audio.wav");
        content.Add(new StringContent("whisper-large-v3-turbo"), "model");

        var response = await client.PostAsync("https://api.groq.com/openai/v1/audio/transcriptions", content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("text").GetString() ?? "";
        }

        throw new Exception($"Помилка API: {jsonResponse}");
    }


    private async Task<bool> CheckAndRequestMicrophonePermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted)
            return true;

        status = await Permissions.RequestAsync<Permissions.Microphone>();
        return status == PermissionStatus.Granted;
    }
}