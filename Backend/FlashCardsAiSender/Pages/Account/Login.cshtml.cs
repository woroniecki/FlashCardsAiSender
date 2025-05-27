using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class LoginModel : PageModel
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public LoginModel(JwtTokenService jwtTokenService, IConfiguration configuration)
    {
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
    }

    [BindProperty]
    public string Username { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public IActionResult OnGet() => Page();

    public IActionResult OnPost()
    {
        var validUsername = _configuration["Auth:Username"];
        var validPassword = _configuration["Auth:Password"];

        if (Username == validUsername && Password == validPassword)
        {
            var token = _jwtTokenService.GenerateToken(Username);

            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(200)
            });

            return RedirectToPage("/Index");
        }

        ModelState.AddModelError(string.Empty, "Invalid username or password");
        return Page();
    }
}
