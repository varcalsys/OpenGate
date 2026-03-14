using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenGate.Data.EFCore.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.UI.Pages.Connect;

[ValidateAntiForgeryToken]
public sealed class AuthorizeModel(
    IOpenIddictScopeManager scopeManager,
    IOpenIddictApplicationManager applicationManager,
    UserManager<OpenGateUser> userManager) : PageModel
{
    public string?              ClientId        { get; private set; }
    public string?              ApplicationName { get; private set; }
    public IReadOnlyList<ScopeViewModel> RequestedScopes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        // If user is not authenticated, redirect to Login.
        if (!User.Identity?.IsAuthenticated == true)
        {
            var redirectUri = Request.PathBase + Request.Path + Request.QueryString;
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri });
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null || !currentUser.IsActive)
        {
            return await ChallengeForActiveAccountAsync();
        }

        ClientId = request.ClientId;

        var app = ClientId is not null
            ? await applicationManager.FindByClientIdAsync(ClientId)
            : null;

        ApplicationName = app is not null
            ? await applicationManager.GetDisplayNameAsync(app)
            : ClientId;

        RequestedScopes = await BuildScopeListAsync(request.GetScopes());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string decision)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (decision is "deny")
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error]
                        = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription]
                        = "The user denied the authorization request."
                }));
        }

        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("The current user cannot be found.");

        if (!user.IsActive)
        {
            return await ChallengeForActiveAccountAsync();
        }

        var identity = new ClaimsIdentity(
            authenticationType: "OpenIddict",
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject,   await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Email,     await userManager.GetEmailAsync(user))
                .SetClaim(Claims.Name,      await userManager.GetUserNameAsync(user))
                .SetClaims(Claims.Role,     [.. (await userManager.GetRolesAsync(user))]);

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager
            .ListResourcesAsync(identity.GetScopes())
            .ToListAsync());

        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ScopeViewModel>> BuildScopeListAsync(
        IEnumerable<string> requestedScopes)
    {
        var list = new List<ScopeViewModel>();

        foreach (var name in requestedScopes)
        {
            var scope       = await scopeManager.FindByNameAsync(name);
            var description = scope is not null
                ? await scopeManager.GetDescriptionAsync(scope)
                : null;

            list.Add(new ScopeViewModel { Name = name, Description = description });
        }

        return list;
    }

    private async Task<IActionResult> ChallengeForActiveAccountAsync()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        var redirectUri = Request.PathBase + Request.Path + Request.QueryString;
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri });
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Name or Claims.Subject
                => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true
                => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role when claim.Subject?.HasScope(Scopes.Roles) == true
                => [Destinations.AccessToken, Destinations.IdentityToken],
            _   => [Destinations.AccessToken]
        };
    }
}

public sealed record ScopeViewModel
{
    public string  Name        { get; init; } = string.Empty;
    public string? Description { get; init; }
}

