namespace OpenGate.Admin.Api.Contracts;

public sealed record AdminClientRequest
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? DisplayName { get; init; }
    public string? ClientType { get; init; }
    public string? ConsentType { get; init; }
    public IReadOnlyList<string>? RedirectUris { get; init; }
    public IReadOnlyList<string>? PostLogoutRedirectUris { get; init; }
    public IReadOnlyList<string>? Permissions { get; init; }
    public IReadOnlyList<string>? Requirements { get; init; }
}

public sealed record AdminScopeRequest
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Resources { get; init; }
}

public sealed record AdminUserRequest
{
    public string? Email { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool? IsActive { get; init; }
    public bool? EmailConfirmed { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? DisplayName { get; init; }
    public string? Locale { get; init; }
    public string? TimeZone { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
}

public sealed record AdminUserRolesRequest
{
    public IReadOnlyList<string>? Roles { get; init; }
}

public sealed record AdminConfigurationDocument
{
    public string? Format { get; init; }
    public int Version { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }
    public IReadOnlyList<AdminClientRequest>? Clients { get; init; }
    public IReadOnlyList<AdminScopeRequest>? Scopes { get; init; }
}

public sealed record AdminConfigurationImportResult
{
    public int ClientCreatedCount { get; init; }
    public int ClientUpdatedCount { get; init; }
    public int ScopeCreatedCount { get; init; }
    public int ScopeUpdatedCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}