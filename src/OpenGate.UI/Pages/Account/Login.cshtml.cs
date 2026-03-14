using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OpenGate.Data.EFCore.Entities;

namespace OpenGate.UI.Pages.Account;

public sealed partial class LoginModel(
    SignInManager<OpenGateUser> signInManager,
    UserManager<OpenGateUser> userManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar neste dispositivo")]
        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        // If the user is already authenticated, don't show the login form again.
        // This also prevents the "login succeeded but nothing happened" feeling
        // when the sample redirects / -> /Account/Login.
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ReturnUrl);
        }

        // Clear existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user is null || !user.IsActive)
        {
            if (user is not null && !user.IsActive)
            {
                Log.UserInactive(logger, Input.Email);
            }

            ErrorMessage = "E-mail ou senha incorretos.";
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            Log.UserLoggedIn(logger, Input.Email);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            Log.UserLockedOut(logger, Input.Email);
            ErrorMessage = "Conta bloqueada temporariamente por excesso de tentativas. Tente novamente mais tarde.";
            return Page();
        }

        // Invalid credentials — generic message to prevent user enumeration
        ErrorMessage = "E-mail ou senha incorretos.";
        return Page();
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} logged in.")]
        public static partial void UserLoggedIn(ILogger logger, string email);

        [LoggerMessage(Level = LogLevel.Warning, Message = "User {Email} account locked out.")]
        public static partial void UserLockedOut(ILogger logger, string email);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Inactive user {Email} attempted to log in.")]
        public static partial void UserInactive(ILogger logger, string email);
    }
}

