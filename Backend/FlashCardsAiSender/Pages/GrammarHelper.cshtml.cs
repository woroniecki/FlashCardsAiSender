using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public enum PromptType
{
    WordTo_ExplanationEng_WordTranslation,
    WordTo_SentancePl_SentanceEng,
    GrammarRuleTo_SentancePl_SentanceEng,
    SentenceTo_SentencePl_SentanceEng,
    WordsListTo_ExplanationEng_WordTranslation
}

public class Flashcard
{
    public string Question { get; set; }
    public string Answer { get; set; }
}

[Authorize(AuthenticationSchemes = "Bearer")]
public class GrammarHelperModel : PageModel
{
    [BindProperty]
    public string UserQuestion { get; set; }

    [BindProperty]
    public PromptType SelectedPromptType { get; set; }

    [BindProperty]
    public string Answer { get; set; }

    [BindProperty]
    public List<Flashcard> Flashcards { get; set; } = new();

    public string SendTime { get; set; }

    private readonly SupermemoFlashcardService _supermemoFlashcardService;

    private readonly IConfiguration _configuration;

    public GrammarHelperModel(SupermemoFlashcardService supermemoFlashcardService, IConfiguration configuration)
    {
        _supermemoFlashcardService = supermemoFlashcardService;
        _configuration = configuration;
    }

    private readonly Dictionary<PromptType, string> PromptTemplates = new()
{
    // English word / Example English sentence and translation in Polish
    { PromptType.WordTo_SentancePl_SentanceEng,
        "I send you English word: {0}.\nPlease give me list of flashcards with all meanings used in the sentance example and their translations in the format below.\nGive me only this JSON answer, nothing more, only it.\n{\n\t\"flashcards\": [\n\t\t{\n\t\t\t\"Question\": \"Przetłumaczone przykładowe zdanie po polsku (tłumaczenie podanego słowa: {0})\",\n\t\t\t\"Answer\": \"Example sentence in English and english word({0})\"\n\t\t}\n\t]\n}" },

    // English explanation of word / Word in English and translation in Polish
    { PromptType.WordTo_ExplanationEng_WordTranslation,
        "I send you English word: {0}.\nPlease give me list of flashcards with the all meanings of the given word in the format below.\nGive me only this JSON answer, nothing more, only it.\n{\n\t\"flashcards\": [\n\t\t{\n\t\t\t\"Question\": \"Explanation in English of the given word\",\n\t\t\t\"Answer\": \"English word({0}) and (translation in Polish)\"\n\t\t}\n\t]\n}" },

    // English grammar rule / Translated example usage sentence in Polish
    { PromptType.GrammarRuleTo_SentancePl_SentanceEng,
        "I send you English grammar rule: {0}.\nPlease give me a couple of different usage examples.\nGive me only this JSON answer, nothing more, only it.\n{\n\t\"flashcards\": [\n\t\t{\n\t\t\t\"Question\": \"Przetłumaczone przykładowe zdanie po polsku\",\n\t\t\t\"Answer\": \"Example sentence in English and grammar rule ({0})\"\n\t\t}\n\t]\n}" },

    // English sentence / Translated sentence in Polish
    { PromptType.SentenceTo_SentencePl_SentanceEng,
        "I send you English sentence: {0}.\nPlease translate it.\nGive me only this JSON answer, nothing more, only it.\n{\n\t\"flashcards\": [\n\t\t{\n\t\t\t\"Question\": \"Przetłumaczone przykładowe zdanie po polsku\",\n\t\t\t\"Answer\": \"Example sentence in English\"\n\t\t}\n\t]\n}" },

    // List of English words / Translations with optional short definitions
    { PromptType.WordsListTo_ExplanationEng_WordTranslation,
        "I send you list of English words: {0}.\nPlease give me list of flashcards with the all meanings of the given words list in the format below. Question is explanation of the meaning of the word, answer is provided word and it's translation in polish\nGive me only this JSON answer, nothing more, only it.\n{\n\t\"flashcards\": [\n\t\t{\n\t\t\t\"Question\": \"Explanation of the meaning in English of the given word\",\n\t\t\t\"Answer\": \"Given english word and (translation in Polish)\"\n\t\t}\n\t]\n}"  }
    };

    public async Task<IActionResult> OnPostAskAsync()
    {
        if (string.IsNullOrWhiteSpace(UserQuestion))
        {
            Answer = "Please enter a question.";
            return Page();
        }

        SendTime = "";

        var rawAnswer = await AskOpenAi(GeneratePrompt(UserQuestion, SelectedPromptType));

        try
        {
            using var doc = JsonDocument.Parse(rawAnswer);
            var flashcards = doc.RootElement.GetProperty("flashcards")
                .EnumerateArray()
                .Select(fc => new Flashcard
                {
                    Question = fc.GetProperty("Question").GetString(),
                    Answer = fc.GetProperty("Answer").GetString()
                }).ToList();

            Flashcards = flashcards;
        }
        catch
        {
            Answer = "Invalid JSON format returned from OpenAI. " + rawAnswer;
        }

        return Page();
    }


    private string GeneratePrompt(string userInput, PromptType type)
    {
        if (type == PromptType.WordsListTo_ExplanationEng_WordTranslation)
        {
            userInput = userInput.Replace("\n", ",\n");
        }
        return PromptTemplates[type].Replace("{0}", userInput);
    }

    private async Task<string> AskOpenAi(string prompt)
    {
        var apiKey = _configuration["OpenAiKey"];

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model = "gpt-4.1",
            messages = new[] { new { role = "user", content = prompt } }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var message = doc.RootElement
                         .GetProperty("choices")[0]
                         .GetProperty("message")
                         .GetProperty("content")
                         .GetString();

        return message?.Trim();
    }

    public async Task<IActionResult> OnPostSendFlashcardAsync(string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            Answer = "Invalid flashcard data.";
            return Page();
        }

        await _supermemoFlashcardService.SendFlashcardAsync(question, answer);
        SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return Page();
    }

    public async Task<IActionResult> OnPostSendAllAsync()
    {
        if (Flashcards == null || !Flashcards.Any())
        {
            ModelState.AddModelError(string.Empty, "No flashcards to send.");
            return Page();
        }

        foreach (var card in Flashcards)
        {
            await _supermemoFlashcardService.SendFlashcardAsync(card.Question, card.Answer);
        }

        SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return Page();
    }

}
