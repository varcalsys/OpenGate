namespace OpenGate.Migration;

internal sealed class DuendeConfigurationEnvelope
{
    public DuendeConfigurationDocument? IdentityServer { get; init; }
    public IReadOnlyList<DuendeClient>? Clients { get; init; }
    public IReadOnlyList<DuendeApiScope>? ApiScopes { get; init; }
    public IReadOnlyList<DuendeApiResource>? ApiResources { get; init; }
    public IReadOnlyList<DuendeIdentityResource>? IdentityResources { get; init; }

    public DuendeConfigurationDocument Normalize()
        => IdentityServer ?? new DuendeConfigurationDocument
        {
            Clients = Clients,
            ApiScopes = ApiScopes,
            ApiResources = ApiResources,
            IdentityResources = IdentityResources
        };
}

internal sealed class DuendeConfigurationDocument
{
    public IReadOnlyList<DuendeClient>? Clients { get; init; }
    public IReadOnlyList<DuendeApiScope>? ApiScopes { get; init; }
    public IReadOnlyList<DuendeApiResource>? ApiResources { get; init; }
    public IReadOnlyList<DuendeIdentityResource>? IdentityResources { get; init; }
}

internal sealed class DuendeClient
{
    public string? ClientId { get; init; }
    public string? ClientName { get; init; }
    public string? Description { get; init; }
    public bool? RequireClientSecret { get; init; }
    public bool? RequireConsent { get; init; }
    public bool? RequirePkce { get; init; }
    public bool? AllowOfflineAccess { get; init; }
    public IReadOnlyList<string>? AllowedGrantTypes { get; init; }
    public IReadOnlyList<string>? RedirectUris { get; init; }
    public IReadOnlyList<string>? PostLogoutRedirectUris { get; init; }
    public IReadOnlyList<string>? AllowedScopes { get; init; }
    public IReadOnlyList<DuendeSecret>? ClientSecrets { get; init; }
}

internal sealed class DuendeSecret
{
    public string? Value { get; init; }
    public string? Type { get; init; }
}

internal sealed class DuendeApiScope
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
}

internal sealed class DuendeApiResource
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Scopes { get; init; }
}

internal sealed class DuendeIdentityResource
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
}