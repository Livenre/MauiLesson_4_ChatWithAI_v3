using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MauiLesson_4_ChatWithAI_v3;

public class AiService
{
    private readonly HttpClient _httpClient = new();
    private const string ApiKey = "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";
    public async Task<string> AskAi(string prompt, string modelId = "openrouter/free")
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);

        var request = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseJson);

        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}