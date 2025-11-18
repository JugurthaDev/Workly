using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace tp_aspire_samy_jugurtha.WebApp.Pages;

public class LoginOidcModel : PageModel
{
    public IActionResult OnGet(string returnUrl = "/dashboard")
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl);
        }

        return Challenge(new AuthenticationProperties
        {
            RedirectUri = returnUrl
        }, "oidc");
    }
}
