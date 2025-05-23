using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

public class SupermemoJwtTokenService
{
    private readonly IMemoryCache _memoryCache;
    private readonly HttpClient _httpClient;

    private const string TokenCacheKey = "JWT-TOKEN";
    private const string LoginUrl = "https://learn.supermemo.com/authapi/login";

    private readonly string _username;
    private readonly string _password;

    public SupermemoJwtTokenService(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory, string username, string password)
    {
        _memoryCache = memoryCache;
        _httpClient = httpClientFactory.CreateClient();
        _username = username;
        _password = password;
    }

    public async Task<string> GetValidTokenAsync()
    {
        if (_memoryCache.TryGetValue<string>(TokenCacheKey, out var token) && IsTokenValid(token))
        {
            return token;
        }

        var newToken = await LoginAndGetTokenAsync();
        if (!string.IsNullOrEmpty(newToken))
        {
            _memoryCache.Set(TokenCacheKey, newToken, TimeSpan.FromMinutes(60));
        }

        return newToken;
    }

    private bool IsTokenValid(string token)
    {
        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtToken = jwtHandler.ReadJwtToken(token);

            return jwtToken.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> LoginAndGetTokenAsync()
    {
        var loginData = new
        {
            username = _username,
            password = _password
        };

        var content = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(LoginUrl, content);

        if (!response.IsSuccessStatusCode)
            return null;

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("jwtToken", out var jwtTokenElement))
        {
            return jwtTokenElement.GetString();
        }

        return null;
    }
}
