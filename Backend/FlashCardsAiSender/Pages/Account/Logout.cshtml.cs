using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlashCardsAiSender.Pages.Account
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnPost()
        {
            Response.Cookies.Delete("jwt");
            return RedirectToPage("/Account/Login");
        }
    }
}
