using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class SupermemoFlashcardService
{
    private readonly HttpClient _httpClient;
    private readonly SupermemoJwtTokenService _jwtTokenService;
    private const string EndpointUrl = "https://learn.supermemo.com/api/users/2549032/pages?contentType=json";

    const string ENGLISH = "15";
    const string POLISH = "47";

    public static readonly Dictionary<PromptType, (string FromLang, string ToLang)> LanguageMap =
        new()
        {
            { PromptType.WordTo_ExplanationEng_WordTranslation, (ENGLISH, ENGLISH) },
            { PromptType.WordTo_SentancePl_SentanceEng, (POLISH, ENGLISH) },
            { PromptType.GrammarRuleTo_SentancePl_SentanceEng, (POLISH, ENGLISH) },
            { PromptType.SentenceTo_SentencePl_SentanceEng, (POLISH, ENGLISH) },
            { PromptType.WordsListTo_ExplanationEng_WordTranslation, (ENGLISH, ENGLISH) }
        };

    public SupermemoFlashcardService(IHttpClientFactory httpClientFactory, SupermemoJwtTokenService jwtTokenService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _jwtTokenService = jwtTokenService;
    }

    public async Task SendFlashcardAsync(string question, string answer, PromptType promptType)
    {
        var token = await _jwtTokenService.GetValidTokenAsync();

        var flashcard = new
        {
            question = JsonSerializer.Serialize(new { content = question, media = new object[] { } }),
            answer = JsonSerializer.Serialize(new { content = answer, media = new object[] { } }),
            speechSettings = JsonSerializer.Serialize(new
            {
                question = new { tts = true, stt = (string?)null, lang = LanguageMap[promptType].FromLang },
                answer = new { tts = true, stt = true, lang = LanguageMap[promptType].ToLang }
            })
        };

        var jsonContent = JsonSerializer.Serialize(flashcard);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsync(EndpointUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to send flashcard: {response.StatusCode} - {error}");
        }
    }
}
