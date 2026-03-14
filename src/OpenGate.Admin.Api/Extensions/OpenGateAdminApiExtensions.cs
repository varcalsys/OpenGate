using System.Security.Claims;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenGate.Admin.Api.Security;
using OpenGate.Admin.Api.Contracts;
using OpenGate.Data.EFCore;
using OpenGate.Data.EFCore.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Admin.Api.Extensions;

/// <summary>
/// Registers and maps the OpenGate Admin API.
/// </summary>
public static class OpenGateAdminApiExtensions
{
    private static readonly string[] Features =
    [
        "me",
        "users",
        "audit-logs",
        "sessions",
        "clients",
        "scopes",
        "configuration"
    ];

    private const string ConfigurationDocumentFormat = "opengate-admin-configuration";
    private const int ConfigurationDocumentVersion = 1;
    private static readonly string[] ConfigurationExportNotes =
    [
        "Client secrets are never exported.",
        "To recreate a confidential client in a fresh environment, provide a clientSecret before importing."
    ];

    public static IServiceCollection AddOpenGateAdminApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorizationBuilder()
            .AddPolicy(OpenGateAdminPolicies.Viewer, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(OpenGateAdminRoles.Viewer, OpenGateAdminRoles.Admin, OpenGateAdminRoles.SuperAdmin))
            .AddPolicy(OpenGateAdminPolicies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(OpenGateAdminRoles.Admin, OpenGateAdminRoles.SuperAdmin))
            .AddPolicy(OpenGateAdminPolicies.SuperAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(OpenGateAdminRoles.SuperAdmin));

        return services;
    }

    public static IEndpointRouteBuilder MapOpenGateAdminApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/admin/api")
            .WithTags("OpenGate Admin")
            .RequireAuthorization(OpenGateAdminPolicies.Viewer);

        DescribeOk(
            group.MapGet("/", () => TypedResults.Ok(new
            {
                name = "OpenGate Admin API",
                version = "v0",
                features = Features
            })),
            "OpenGateAdmin_GetOverview",
            "Get Admin API overview",
            "Returns the current OpenGate Admin API feature set and version.");

        DescribeOk(
            group.MapGet("/me", GetCurrentUserAsync),
            "OpenGateAdmin_GetMe",
            "Get current admin user",
            "Returns the authenticated administrative user and assigned roles.");

        DescribeOk(
            group.MapGet("/clients", GetClientsAsync),
            "OpenGateAdmin_ListClients",
            "List clients",
            "Returns registered OpenIddict clients with filters and metadata for administration.");

        DescribeOk(
            group.MapGet("/clients/{clientId}", GetClientByIdAsync),
            "OpenGateAdmin_GetClient",
            "Get client",
            "Returns the details of a single OpenIddict client by client identifier.",
            includeNotFound: true);

        DescribeCreated(
            group.MapPost("/clients", CreateClientAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_CreateClient",
            "Create client",
            "Creates a new OpenIddict client for interactive or machine-to-machine flows.");

        DescribeUpdated(
            group.MapPut("/clients/{clientId}", UpdateClientAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_UpdateClient",
            "Update client",
            "Updates mutable settings of an existing OpenIddict client.");

        DescribeDeleted(
            group.MapDelete("/clients/{clientId}", DeleteClientAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_DeleteClient",
            "Delete client",
            "Deletes an OpenIddict client from the administration surface.");

        DescribeOk(
            group.MapGet("/scopes", GetScopesAsync),
            "OpenGateAdmin_ListScopes",
            "List scopes",
            "Returns registered OpenIddict scopes and resources.");

        DescribeOk(
            group.MapGet("/scopes/{name}", GetScopeByNameAsync),
            "OpenGateAdmin_GetScope",
            "Get scope",
            "Returns the details of a single scope by name.",
            includeNotFound: true);

        DescribeCreated(
            group.MapPost("/scopes", CreateScopeAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_CreateScope",
            "Create scope",
            "Creates a new OpenIddict scope and associates resources.");

        DescribeUpdated(
            group.MapPut("/scopes/{name}", UpdateScopeAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_UpdateScope",
            "Update scope",
            "Updates an existing OpenIddict scope.");

        DescribeDeleted(
            group.MapDelete("/scopes/{name}", DeleteScopeAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_DeleteScope",
            "Delete scope",
            "Deletes an OpenIddict scope from the administration surface.");

        DescribeOk(
            group.MapGet("/configuration/export", ExportConfigurationAsync),
            "OpenGateAdmin_ExportConfiguration",
            "Export configuration",
            "Exports clients and scopes to a JSON document that can later be imported into OpenGate.");

        DescribeAction(
            group.MapPost("/configuration/import", ImportConfigurationAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin)
                .Produces(StatusCodes.Status400BadRequest),
            "OpenGateAdmin_ImportConfiguration",
            "Import configuration",
            "Imports a JSON document with clients and scopes, updating existing entries and creating missing ones.");

        DescribeOk(
            group.MapGet("/roles", GetRolesAsync),
            "OpenGateAdmin_ListRoles",
            "List admin roles",
            "Returns available administrative roles used by the Admin API.");

        DescribeOk(
            group.MapGet("/users", GetUsersAsync),
            "OpenGateAdmin_ListUsers",
            "List users",
            "Returns users, profile data, roles and active session counts.");

        DescribeOk(
            group.MapGet("/users/{id}", GetUserByIdAsync),
            "OpenGateAdmin_GetUser",
            "Get user",
            "Returns administrative details for a single user.",
            includeNotFound: true);

        DescribeCreated(
            group.MapPost("/users", CreateUserAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_CreateUser",
            "Create user",
            "Creates a new user and optionally assigns administrative roles.");

        DescribeUpdated(
            group.MapPut("/users/{id}", UpdateUserAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_UpdateUser",
            "Update user",
            "Updates user profile data, activation state and optional role assignments.");

        DescribeUpdated(
            group.MapPut("/users/{id}/roles", SetUserRolesAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_SetUserRoles",
            "Replace user roles",
            "Replaces the full set of roles assigned to a user.");

        DescribeDeleted(
            group.MapDelete("/users/{id}", DeleteUserAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_DeleteUser",
            "Delete user",
            "Deletes a user account from the administration surface.");

        DescribeOk(
            group.MapGet("/audit-logs", GetAuditLogsAsync),
            "OpenGateAdmin_ListAuditLogs",
            "List audit logs",
            "Returns audit events for administrative and authentication activity.");

        DescribeOk(
            group.MapGet("/sessions", GetSessionsAsync),
            "OpenGateAdmin_ListSessions",
            "List sessions",
            "Returns user sessions, activity state and revocation information.");

        DescribeAction(
            group.MapPost("/sessions/{id:guid}/revoke", RevokeSessionAsync)
                .RequireAuthorization(OpenGateAdminPolicies.Admin),
            "OpenGateAdmin_RevokeSession",
            "Revoke session",
            "Revokes an active user session and records an audit event.");

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<OpenGateUser> userManager)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);

        return TypedResults.Ok(new
        {
            user.Id,
            user.Email,
            user.UserName,
            roles
        });
    }

    private static async Task<IResult> GetClientsAsync(
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);

        var items = new List<object>(limit);
        await foreach (var application in applicationManager.ListAsync(null, null, cancellationToken))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

            if (!MatchesClientSearch(descriptor, search))
            {
                continue;
            }

            items.Add(await BuildClientResponseAsync(applicationManager, application, descriptor, cancellationToken));
            if (items.Count >= limit)
            {
                break;
            }
        }

        return TypedResults.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> GetClientByIdAsync(
        string clientId,
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

        return TypedResults.Ok(await BuildClientResponseAsync(applicationManager, application, descriptor, cancellationToken));
    }

    private static async Task<IResult> CreateClientAsync(
        AdminClientRequest request,
        ClaimsPrincipal principal,
        IOpenIddictApplicationManager applicationManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateClientRequest(request, isCreate: true, routeClientId: null);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        ApplyClientRequest(descriptor, request, isCreate: true);

        await applicationManager.CreateAsync(descriptor, cancellationToken);

        var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken);
        if (application is null)
        {
            return TypedResults.Problem("The client was created but could not be retrieved afterwards.");
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ClientCreated",
            details: new { clientId = request.ClientId, request.DisplayName },
            cancellationToken);

        var response = await BuildClientResponseAsync(applicationManager, application, descriptor, cancellationToken);
        return TypedResults.Created(BuildClientLocation(request.ClientId!), response);
    }

    private static async Task<IResult> UpdateClientAsync(
        string clientId,
        AdminClientRequest request,
        ClaimsPrincipal principal,
        IOpenIddictApplicationManager applicationManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateClientRequest(request, isCreate: false, routeClientId: clientId);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        ApplyClientRequest(descriptor, request, isCreate: false);

        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ClientUpdated",
            details: new { clientId, request.DisplayName },
            cancellationToken);

        return TypedResults.Ok(await BuildClientResponseAsync(applicationManager, application, descriptor, cancellationToken));
    }

    private static async Task<IResult> DeleteClientAsync(
        string clientId,
        ClaimsPrincipal principal,
        IOpenIddictApplicationManager applicationManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        await DeleteOpenIddictEntityAsync(
            db,
            application,
            () => applicationManager.DeleteAsync(application, cancellationToken),
            cancellationToken);

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ClientDeleted",
            details: new { clientId },
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetScopesAsync(
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);

        var items = new List<object>(limit);
        await foreach (var scope in scopeManager.ListAsync(null, null, cancellationToken))
        {
            var descriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(descriptor, scope, cancellationToken);

            if (!MatchesScopeSearch(descriptor, search))
            {
                continue;
            }

            items.Add(await BuildScopeResponseAsync(scopeManager, scope, descriptor, cancellationToken));
            if (items.Count >= limit)
            {
                break;
            }
        }

        return TypedResults.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> GetScopeByNameAsync(
        string name,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var scope = await scopeManager.FindByNameAsync(name, cancellationToken);
        if (scope is null)
        {
            return TypedResults.NotFound();
        }

        var descriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(descriptor, scope, cancellationToken);

        return TypedResults.Ok(await BuildScopeResponseAsync(scopeManager, scope, descriptor, cancellationToken));
    }

    private static async Task<IResult> CreateScopeAsync(
        AdminScopeRequest request,
        ClaimsPrincipal principal,
        IOpenIddictScopeManager scopeManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateScopeRequest(request, isCreate: true, routeName: null);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var descriptor = new OpenIddictScopeDescriptor();
        ApplyScopeRequest(descriptor, request, isCreate: true);

        await scopeManager.CreateAsync(descriptor, cancellationToken);

        var scope = await scopeManager.FindByNameAsync(request.Name!, cancellationToken);
        if (scope is null)
        {
            return TypedResults.Problem("The scope was created but could not be retrieved afterwards.");
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ScopeCreated",
            details: new { request.Name, request.DisplayName },
            cancellationToken);

        var response = await BuildScopeResponseAsync(scopeManager, scope, descriptor, cancellationToken);
        return TypedResults.Created(BuildScopeLocation(request.Name!), response);
    }

    private static async Task<IResult> UpdateScopeAsync(
        string name,
        AdminScopeRequest request,
        ClaimsPrincipal principal,
        IOpenIddictScopeManager scopeManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateScopeRequest(request, isCreate: false, routeName: name);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var scope = await scopeManager.FindByNameAsync(name, cancellationToken);
        if (scope is null)
        {
            return TypedResults.NotFound();
        }

        var descriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(descriptor, scope, cancellationToken);
        ApplyScopeRequest(descriptor, request, isCreate: false);

        await scopeManager.UpdateAsync(scope, descriptor, cancellationToken);
        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ScopeUpdated",
            details: new { name, request.DisplayName },
            cancellationToken);

        return TypedResults.Ok(await BuildScopeResponseAsync(scopeManager, scope, descriptor, cancellationToken));
    }

    private static async Task<IResult> DeleteScopeAsync(
        string name,
        ClaimsPrincipal principal,
        IOpenIddictScopeManager scopeManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var scope = await scopeManager.FindByNameAsync(name, cancellationToken);
        if (scope is null)
        {
            return TypedResults.NotFound();
        }

        await DeleteOpenIddictEntityAsync(
            db,
            scope,
            () => scopeManager.DeleteAsync(scope, cancellationToken),
            cancellationToken);

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ScopeDeleted",
            details: new { name },
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ExportConfigurationAsync(
        ClaimsPrincipal principal,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var scopes = new List<AdminScopeRequest>();
        await foreach (var scope in scopeManager.ListAsync(null, null, cancellationToken))
        {
            var descriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(descriptor, scope, cancellationToken);
            scopes.Add(BuildScopeExportRequest(descriptor));
        }

        var clients = new List<AdminClientRequest>();
        await foreach (var application in applicationManager.ListAsync(null, null, cancellationToken))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            clients.Add(BuildClientExportRequest(descriptor));
        }

        var document = new AdminConfigurationDocument
        {
            Format = ConfigurationDocumentFormat,
            Version = ConfigurationDocumentVersion,
            GeneratedAt = DateTimeOffset.UtcNow,
            Notes = ConfigurationExportNotes,
            Clients = clients.OrderBy(item => item.ClientId, StringComparer.Ordinal).ToArray(),
            Scopes = scopes.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray()
        };

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ConfigurationExported",
            details: new { clientCount = clients.Count, scopeCount = scopes.Count, document.Format, document.Version },
            cancellationToken);

        return TypedResults.Ok(document);
    }

    private static async Task<IResult> ImportConfigurationAsync(
        AdminConfigurationDocument document,
        ClaimsPrincipal principal,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = await ValidateConfigurationDocumentAsync(document, applicationManager, cancellationToken);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var scopeCreatedCount = 0;
        var scopeUpdatedCount = 0;
        foreach (var request in document.Scopes ?? [])
        {
            var scope = await scopeManager.FindByNameAsync(request.Name!, cancellationToken);
            if (scope is null)
            {
                var descriptor = new OpenIddictScopeDescriptor();
                ApplyScopeRequest(descriptor, request, isCreate: true);
                await scopeManager.CreateAsync(descriptor, cancellationToken);
                scopeCreatedCount++;
                continue;
            }

            var updatedDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(updatedDescriptor, scope, cancellationToken);
            ApplyScopeRequest(updatedDescriptor, request, isCreate: false);
            await scopeManager.UpdateAsync(scope, updatedDescriptor, cancellationToken);
            scopeUpdatedCount++;
        }

        var clientCreatedCount = 0;
        var clientUpdatedCount = 0;
        foreach (var request in document.Clients ?? [])
        {
            var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken);
            if (application is null)
            {
                var descriptor = new OpenIddictApplicationDescriptor();
                ApplyClientRequest(descriptor, request, isCreate: true);
                await applicationManager.CreateAsync(descriptor, cancellationToken);
                clientCreatedCount++;
                continue;
            }

            var updatedDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(updatedDescriptor, application, cancellationToken);
            ApplyClientRequest(updatedDescriptor, request, isCreate: false);
            await applicationManager.UpdateAsync(application, updatedDescriptor, cancellationToken);
            clientUpdatedCount++;
        }

        var response = new AdminConfigurationImportResult
        {
            ClientCreatedCount = clientCreatedCount,
            ClientUpdatedCount = clientUpdatedCount,
            ScopeCreatedCount = scopeCreatedCount,
            ScopeUpdatedCount = scopeUpdatedCount,
            Warnings = BuildImportWarnings(document)
        };

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.ConfigurationImported",
            details: new
            {
                response.ClientCreatedCount,
                response.ClientUpdatedCount,
                response.ScopeCreatedCount,
                response.ScopeUpdatedCount,
                response.Warnings
            },
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetUsersAsync(
        OpenGateDbContext db,
        UserManager<OpenGateUser> userManager,
        CancellationToken cancellationToken,
        int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        var now = DateTimeOffset.UtcNow;

        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .OrderBy(u => u.Email)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var items = new List<object>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var activeSessionCount = await db.UserSessions.CountAsync(
                s => s.UserId == user.Id && s.RevokedAt == null && s.ExpiresAt > now,
                cancellationToken);

            items.Add(new
            {
                user.Id,
                user.Email,
                user.UserName,
                user.IsActive,
                user.CreatedAt,
                user.LastLoginAt,
                displayName = user.Profile?.DisplayName,
                firstName = user.Profile?.FirstName,
                lastName = user.Profile?.LastName,
                roles,
                activeSessionCount
            });
        }

        return TypedResults.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> GetRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                role.Id,
                role.Name,
                role.NormalizedName
            })
            .ToListAsync();

        return TypedResults.Ok(new { items = roles, count = roles.Count });
    }

    private static async Task<IResult> GetUserByIdAsync(
        string id,
        OpenGateDbContext db,
        UserManager<OpenGateUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        var sessions = await db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.ClientId,
                s.DeviceInfo,
                s.CreatedAt,
                s.ExpiresAt,
                s.RevokedAt,
                isActive = s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new
        {
            user.Id,
            user.Email,
            user.UserName,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            profile = new
            {
                user.Profile?.FirstName,
                user.Profile?.LastName,
                user.Profile?.DisplayName,
                user.Profile?.Locale,
                user.Profile?.TimeZone
            },
            roles,
            sessions
        });
    }

    private static async Task<IResult> CreateUserAsync(
        AdminUserRequest request,
        ClaimsPrincipal principal,
        UserManager<OpenGateUser> userManager,
        RoleManager<IdentityRole> roleManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = await ValidateUserRequestAsync(
            request,
            userManager,
            roleManager,
            isCreate: true,
            existingUserId: null);

        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var user = new OpenGateUser
        {
            Email = request.Email!,
            UserName = request.UserName ?? request.Email,
            EmailConfirmed = request.EmailConfirmed ?? false,
            IsActive = request.IsActive ?? true,
            Profile = BuildOrUpdateProfile(null, request)
        };

        var result = await userManager.CreateAsync(user, request.Password!);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(ToValidationErrors(result));
        }

        if (request.Roles is { Count: > 0 })
        {
            var normalizedRoles = NormalizeDistinctValues(request.Roles);
            var rolesResult = await userManager.AddToRolesAsync(user, normalizedRoles);
            if (!rolesResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return TypedResults.ValidationProblem(ToValidationErrors(rolesResult));
            }
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.UserCreated",
            details: new { user.Id, user.Email, request.Roles },
            cancellationToken);

        var payload = await BuildUserDetailsResponseAsync(db, userManager, user.Id, cancellationToken);
        return TypedResults.Created(BuildUserLocation(user.Id), payload!);
    }

    private static async Task<IResult> UpdateUserAsync(
        string id,
        AdminUserRequest request,
        ClaimsPrincipal principal,
        UserManager<OpenGateUser> userManager,
        RoleManager<IdentityRole> roleManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(candidate => candidate.Profile)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var validationErrors = await ValidateUserRequestAsync(
            request,
            userManager,
            roleManager,
            isCreate: false,
            existingUserId: user.Id);

        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var wasActive = user.IsActive;

        if (request.Email is not null)
        {
            user.Email = request.Email;
        }

        if (request.UserName is not null)
        {
            user.UserName = request.UserName;
        }

        if (request.EmailConfirmed.HasValue)
        {
            user.EmailConfirmed = request.EmailConfirmed.Value;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        user.Profile = BuildOrUpdateProfile(user.Profile, request);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(ToValidationErrors(result));
        }

        if (request.Roles is not null)
        {
            var rolesResult = await ReplaceUserRolesAsync(userManager, user, request.Roles);
            if (!rolesResult.Succeeded)
            {
                return TypedResults.ValidationProblem(ToValidationErrors(rolesResult));
            }
        }

        if (wasActive && request.IsActive == false)
        {
            await RevokeActiveSessionsForUserAsync(db, user.Id, cancellationToken);
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.UserUpdated",
            details: new { user.Id, user.Email, user.IsActive, request.Roles },
            cancellationToken);

        var payload = await BuildUserDetailsResponseAsync(db, userManager, user.Id, cancellationToken);
        return TypedResults.Ok(payload!);
    }

    private static async Task<IResult> SetUserRolesAsync(
        string id,
        AdminUserRolesRequest request,
        ClaimsPrincipal principal,
        UserManager<OpenGateUser> userManager,
        RoleManager<IdentityRole> roleManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var validationErrors = await ValidateRolesAsync(request.Roles, roleManager);
        if (validationErrors is not null)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var rolesResult = await ReplaceUserRolesAsync(userManager, user, request.Roles ?? []);
        if (!rolesResult.Succeeded)
        {
            return TypedResults.ValidationProblem(ToValidationErrors(rolesResult));
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.UserRolesUpdated",
            details: new { user.Id, user.Email, request.Roles },
            cancellationToken);

        var payload = await BuildUserDetailsResponseAsync(db, userManager, user.Id, cancellationToken);
        return TypedResults.Ok(payload!);
    }

    private static async Task<IResult> DeleteUserAsync(
        string id,
        ClaimsPrincipal principal,
        UserManager<OpenGateUser> userManager,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), id, StringComparison.Ordinal))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["The current administrator cannot delete its own account via the Admin API."]
            });
        }

        var user = await db.Users
            .Include(candidate => candidate.Profile)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var email = user.Email;
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(ToValidationErrors(result));
        }

        await WriteAuditLogAsync(
            principal,
            db,
            eventType: "Admin.UserDeleted",
            details: new { id, email },
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAuditLogsAsync(
        OpenGateDbContext db,
        CancellationToken cancellationToken,
        string? userId = null,
        string? eventType = null,
        int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .OrderByDescending(a => a.OccurredAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(a => a.EventType == eventType);
        }

        var items = await query
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                userEmail = a.User!.Email,
                a.EventType,
                a.ClientId,
                a.Succeeded,
                a.IpAddress,
                a.Details,
                a.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> GetSessionsAsync(
        OpenGateDbContext db,
        CancellationToken cancellationToken,
        string? userId = null,
        bool activeOnly = false,
        int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        var now = DateTimeOffset.UtcNow;

        var query = db.UserSessions
            .AsNoTracking()
            .Include(s => s.User)
            .OrderByDescending(s => s.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(s => s.UserId == userId);
        }

        if (activeOnly)
        {
            query = query.Where(s => s.RevokedAt == null && s.ExpiresAt > now);
        }

        var items = await query
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                userEmail = s.User.Email,
                s.ClientId,
                s.IpAddress,
                s.UserAgent,
                s.DeviceInfo,
                s.CreatedAt,
                s.ExpiresAt,
                s.RevokedAt,
                isActive = s.RevokedAt == null && s.ExpiresAt > now
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid id,
        ClaimsPrincipal principal,
        OpenGateDbContext db,
        CancellationToken cancellationToken)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return TypedResults.NotFound();
        }

        if (session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                EventType = "Admin.SessionRevoked",
                ClientId = session.ClientId,
                Succeeded = true,
                Details = $"{{\"sessionId\":\"{session.Id}\",\"targetUserId\":\"{session.UserId}\"}}",
                OccurredAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new
        {
            session.Id,
            session.UserId,
            session.ClientId,
            session.RevokedAt
        });
    }

    private static bool MatchesClientSearch(OpenIddictApplicationDescriptor descriptor, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return (descriptor.ClientId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (descriptor.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool MatchesScopeSearch(OpenIddictScopeDescriptor descriptor, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return (descriptor.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (descriptor.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (descriptor.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static async Task<object> BuildClientResponseAsync(
        IOpenIddictApplicationManager applicationManager,
        object application,
        OpenIddictApplicationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        return new
        {
            id = await applicationManager.GetIdAsync(application, cancellationToken),
            descriptor.ClientId,
            descriptor.DisplayName,
            descriptor.ClientType,
            descriptor.ConsentType,
            redirectUris = descriptor.RedirectUris.Select(uri => uri.AbsoluteUri).ToArray(),
            postLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(uri => uri.AbsoluteUri).ToArray(),
            permissions = descriptor.Permissions.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            requirements = descriptor.Requirements.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static async Task<object> BuildScopeResponseAsync(
        IOpenIddictScopeManager scopeManager,
        object scope,
        OpenIddictScopeDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        return new
        {
            id = await scopeManager.GetIdAsync(scope, cancellationToken),
            descriptor.Name,
            descriptor.DisplayName,
            descriptor.Description,
            resources = descriptor.Resources.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static async Task<object?> BuildUserDetailsResponseAsync(
        OpenGateDbContext db,
        UserManager<OpenGateUser> userManager,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(candidate => candidate.Profile)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var sessions = await db.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new
            {
                session.Id,
                session.ClientId,
                session.DeviceInfo,
                session.CreatedAt,
                session.ExpiresAt,
                session.RevokedAt,
                isActive = session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow
            })
            .ToListAsync(cancellationToken);

        return new
        {
            user.Id,
            user.Email,
            user.UserName,
            user.EmailConfirmed,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            profile = new
            {
                user.Profile?.FirstName,
                user.Profile?.LastName,
                user.Profile?.DisplayName,
                user.Profile?.Locale,
                user.Profile?.TimeZone
            },
            roles,
            sessions
        };
    }

    private static AdminClientRequest BuildClientExportRequest(OpenIddictApplicationDescriptor descriptor)
        => new()
        {
            ClientId = descriptor.ClientId,
            DisplayName = descriptor.DisplayName,
            ClientType = descriptor.ClientType,
            ConsentType = descriptor.ConsentType,
            RedirectUris = descriptor.RedirectUris.Select(uri => uri.AbsoluteUri).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(uri => uri.AbsoluteUri).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Permissions = descriptor.Permissions.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Requirements = descriptor.Requirements.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };

    private static AdminScopeRequest BuildScopeExportRequest(OpenIddictScopeDescriptor descriptor)
        => new()
        {
            Name = descriptor.Name,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            Resources = descriptor.Resources.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };

    private static Dictionary<string, string[]>? ValidateClientRequest(
        AdminClientRequest request,
        bool isCreate,
        string? routeClientId)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (isCreate && string.IsNullOrWhiteSpace(request.ClientId))
        {
            errors[nameof(request.ClientId)] = ["ClientId is required."];
        }

        if (!string.IsNullOrWhiteSpace(routeClientId)
            && !string.IsNullOrWhiteSpace(request.ClientId)
            && !string.Equals(routeClientId, request.ClientId, StringComparison.Ordinal))
        {
            errors[nameof(request.ClientId)] = ["ClientId cannot be changed via the update endpoint."];
        }

        if (!string.IsNullOrWhiteSpace(request.ClientType)
            && request.ClientType is not (ClientTypes.Public or ClientTypes.Confidential))
        {
            errors[nameof(request.ClientType)] = ["ClientType must be 'public' or 'confidential'."];
        }

        if (!string.IsNullOrWhiteSpace(request.ConsentType)
            && request.ConsentType is not (ConsentTypes.Explicit or ConsentTypes.Implicit or ConsentTypes.External or ConsentTypes.Systematic))
        {
            errors[nameof(request.ConsentType)] = ["ConsentType is invalid."];
        }

        if (isCreate && (request.Permissions is null || request.Permissions.Count == 0))
        {
            errors[nameof(request.Permissions)] = ["At least one permission is required when creating a client."];
        }

        if (isCreate
            && string.Equals(request.ClientType, ClientTypes.Confidential, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            errors[nameof(request.ClientSecret)] = ["ClientSecret is required when creating a confidential client."];
        }

        if (request.RedirectUris is not null && !AreValidAbsoluteUris(request.RedirectUris))
        {
            errors[nameof(request.RedirectUris)] = ["All redirect URIs must be absolute URIs."];
        }

        if (request.PostLogoutRedirectUris is not null && !AreValidAbsoluteUris(request.PostLogoutRedirectUris))
        {
            errors[nameof(request.PostLogoutRedirectUris)] = ["All post-logout redirect URIs must be absolute URIs."];
        }

        if (errors.Count == 0)
        {
            return null;
        }

        return errors;
    }

    private static Dictionary<string, string[]>? ValidateScopeRequest(
        AdminScopeRequest request,
        bool isCreate,
        string? routeName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (isCreate && string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["Name is required."];
        }

        if (!string.IsNullOrWhiteSpace(routeName)
            && !string.IsNullOrWhiteSpace(request.Name)
            && !string.Equals(routeName, request.Name, StringComparison.Ordinal))
        {
            errors[nameof(request.Name)] = ["Scope name cannot be changed via the update endpoint."];
        }

        if (errors.Count == 0)
        {
            return null;
        }

        return errors;
    }

    private static async Task<Dictionary<string, string[]>?> ValidateUserRequestAsync(
        AdminUserRequest request,
        UserManager<OpenGateUser> userManager,
        RoleManager<IdentityRole> roleManager,
        bool isCreate,
        string? existingUserId)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (isCreate && string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["Email is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !new EmailAddressAttribute().IsValid(request.Email))
        {
            errors[nameof(request.Email)] = ["Email must be a valid address."];
        }

        if (isCreate && string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required when creating a user."];
        }

        if (!isCreate && !string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password updates are not supported by this endpoint."];
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingByEmail = await userManager.FindByEmailAsync(request.Email);
            if (existingByEmail is not null && !string.Equals(existingByEmail.Id, existingUserId, StringComparison.Ordinal))
            {
                errors[nameof(request.Email)] = ["Another user already uses this email."];
            }
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var existingByUserName = await userManager.FindByNameAsync(request.UserName);
            if (existingByUserName is not null && !string.Equals(existingByUserName.Id, existingUserId, StringComparison.Ordinal))
            {
                errors[nameof(request.UserName)] = ["Another user already uses this username."];
            }
        }

        var roleErrors = await ValidateRolesAsync(request.Roles, roleManager);
        if (roleErrors is not null)
        {
            foreach (var entry in roleErrors)
            {
                errors[entry.Key] = entry.Value;
            }
        }

        return errors.Count == 0 ? null : errors;
    }

    private static async Task<Dictionary<string, string[]>?> ValidateConfigurationDocumentAsync(
        AdminConfigurationDocument document,
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!string.Equals(document.Format, ConfigurationDocumentFormat, StringComparison.Ordinal))
        {
            errors[nameof(document.Format)] = [$"Format must be '{ConfigurationDocumentFormat}'."];
        }

        if (document.Version != ConfigurationDocumentVersion)
        {
            errors[nameof(document.Version)] = [$"Version must be '{ConfigurationDocumentVersion}'."];
        }

        var clients = document.Clients ?? [];
        var scopes = document.Scopes ?? [];
        if (clients.Count == 0 && scopes.Count == 0)
        {
            errors[nameof(AdminConfigurationDocument.Clients)] = ["At least one client or scope must be provided."];
        }

        AddDuplicateErrors(errors, nameof(AdminConfigurationDocument.Scopes), scopes.Select(scope => scope.Name));
        AddDuplicateErrors(errors, nameof(AdminConfigurationDocument.Clients), clients.Select(client => client.ClientId));

        for (var index = 0; index < scopes.Count; index++)
        {
            var scopeErrors = ValidateScopeRequest(scopes[index], isCreate: true, routeName: null);
            MergeValidationErrors(errors, $"scopes[{index}]", scopeErrors);
        }

        for (var index = 0; index < clients.Count; index++)
        {
            var clientRequest = clients[index];
            var clientErrors = ValidateClientRequest(clientRequest, isCreate: true, routeClientId: null);
            var existingApplication = !string.IsNullOrWhiteSpace(clientRequest.ClientId)
                ? await applicationManager.FindByClientIdAsync(clientRequest.ClientId, cancellationToken)
                : null;

            if (existingApplication is null
                && string.Equals(clientRequest.ClientType, ClientTypes.Confidential, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(clientRequest.ClientSecret))
            {
                clientErrors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                clientErrors[nameof(AdminClientRequest.ClientSecret)] =
                ["ClientSecret is required to create a confidential client during import."];
            }

            MergeValidationErrors(errors, $"clients[{index}]", clientErrors);
        }

        return errors.Count == 0 ? null : errors;
    }

    private static async Task<Dictionary<string, string[]>?> ValidateRolesAsync(
        IReadOnlyList<string>? requestedRoles,
        RoleManager<IdentityRole> roleManager)
    {
        if (requestedRoles is null)
        {
            return null;
        }

        var normalizedRoles = NormalizeDistinctValues(requestedRoles);
        var missingRoles = new List<string>();
        foreach (var role in normalizedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                missingRoles.Add(role);
            }
        }

        return missingRoles.Count == 0
            ? null
            : new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(AdminUserRequest.Roles)] =
                [$"The following roles do not exist: {string.Join(", ", missingRoles)}"]
            };
    }

    private static void ApplyClientRequest(
        OpenIddictApplicationDescriptor descriptor,
        AdminClientRequest request,
        bool isCreate)
    {
        if (isCreate)
        {
            descriptor.ClientId = request.ClientId;
        }

        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.ClientType is not null)
        {
            descriptor.ClientType = request.ClientType;
        }

        if (request.ConsentType is not null)
        {
            descriptor.ConsentType = request.ConsentType;
        }

        if (request.ClientSecret is not null || isCreate)
        {
            descriptor.ClientSecret = request.ClientSecret;
        }

        if (request.RedirectUris is not null)
        {
            descriptor.RedirectUris.Clear();
            foreach (var redirectUri in request.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
            }
        }

        if (request.PostLogoutRedirectUris is not null)
        {
            descriptor.PostLogoutRedirectUris.Clear();
            foreach (var postLogoutRedirectUri in request.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri, UriKind.Absolute));
            }
        }

        if (request.Permissions is not null)
        {
            descriptor.Permissions.Clear();
            foreach (var permission in request.Permissions.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                descriptor.Permissions.Add(permission);
            }
        }

        if (request.Requirements is not null)
        {
            descriptor.Requirements.Clear();
            foreach (var requirement in request.Requirements.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                descriptor.Requirements.Add(requirement);
            }
        }
    }

    private static void ApplyScopeRequest(
        OpenIddictScopeDescriptor descriptor,
        AdminScopeRequest request,
        bool isCreate)
    {
        if (isCreate)
        {
            descriptor.Name = request.Name;
        }

        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.Description is not null)
        {
            descriptor.Description = request.Description;
        }

        if (request.Resources is not null)
        {
            descriptor.Resources.Clear();
            foreach (var resource in request.Resources.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                descriptor.Resources.Add(resource);
            }
        }
    }

    private static UserProfile BuildOrUpdateProfile(UserProfile? profile, AdminUserRequest request)
    {
        profile ??= new UserProfile();

        if (request.FirstName is not null)
        {
            profile.FirstName = request.FirstName;
        }

        if (request.LastName is not null)
        {
            profile.LastName = request.LastName;
        }

        if (request.DisplayName is not null)
        {
            profile.DisplayName = request.DisplayName;
        }

        if (request.Locale is not null)
        {
            profile.Locale = request.Locale;
        }

        if (request.TimeZone is not null)
        {
            profile.TimeZone = request.TimeZone;
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        return profile;
    }

    private static async Task<IdentityResult> ReplaceUserRolesAsync(
        UserManager<OpenGateUser> userManager,
        OpenGateUser user,
        IReadOnlyList<string> requestedRoles)
    {
        var targetRoles = NormalizeDistinctValues(requestedRoles);
        var currentRoles = await userManager.GetRolesAsync(user);

        var rolesToRemove = currentRoles.Except(targetRoles, StringComparer.Ordinal).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return removeResult;
            }
        }

        var rolesToAdd = targetRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();
        return rolesToAdd.Length == 0
            ? IdentityResult.Success
            : await userManager.AddToRolesAsync(user, rolesToAdd);
    }

    private static async Task RevokeActiveSessionsForUserAsync(
        OpenGateDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        var activeSessions = await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        if (activeSessions.Count == 0)
        {
            return;
        }

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var session in activeSessions)
        {
            session.RevokedAt = revokedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task WriteAuditLogAsync(
        ClaimsPrincipal principal,
        OpenGateDbContext db,
        string eventType,
        object details,
        CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
            EventType = eventType,
            Succeeded = true,
            Details = JsonSerializer.Serialize(details),
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var error in result.Errors)
        {
            var key = string.IsNullOrWhiteSpace(error.Code) ? string.Empty : error.Code;
            if (!errors.TryGetValue(key, out var messages))
            {
                messages = [];
                errors[key] = messages;
            }

            messages.Add(error.Description);
        }

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }

    private static string[] NormalizeDistinctValues(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> BuildImportWarnings(AdminConfigurationDocument document)
    {
        var hasConfidentialClientsWithoutSecrets = (document.Clients ?? [])
            .Any(client => string.Equals(client.ClientType, ClientTypes.Confidential, StringComparison.Ordinal)
                        && string.IsNullOrWhiteSpace(client.ClientSecret));

        return hasConfidentialClientsWithoutSecrets
            ? ["Some confidential clients were imported without clientSecret. Existing secrets were preserved, but new environments require manual secret injection before import."]
            : [];
    }

    private static void AddDuplicateErrors(
        Dictionary<string, string[]> errors,
        string key,
        IEnumerable<string?> values)
    {
        var duplicates = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length > 0)
        {
            errors[key] = [$"Duplicate identifiers found: {string.Join(", ", duplicates)}"];
        }
    }

    private static void MergeValidationErrors(
        Dictionary<string, string[]> target,
        string prefix,
        Dictionary<string, string[]>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var entry in source)
        {
            var key = string.IsNullOrWhiteSpace(entry.Key) ? prefix : $"{prefix}.{entry.Key}";
            target[key] = entry.Value;
        }
    }

    private static async Task DeleteOpenIddictEntityAsync(
        OpenGateDbContext db,
        object entity,
        Func<ValueTask> deleteUsingManager,
        CancellationToken cancellationToken)
    {
        try
        {
            await deleteUsingManager();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("ExecuteDelete", StringComparison.Ordinal))
        {
            db.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static RouteHandlerBuilder DescribeOk(
        RouteHandlerBuilder builder,
        string name,
        string summary,
        string description,
        bool includeNotFound = false)
    {
        builder
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        if (includeNotFound)
        {
            builder.Produces(StatusCodes.Status404NotFound);
        }

        return builder;
    }

    private static RouteHandlerBuilder DescribeCreated(
        RouteHandlerBuilder builder,
        string name,
        string summary,
        string description)
        => builder
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

    private static RouteHandlerBuilder DescribeUpdated(
        RouteHandlerBuilder builder,
        string name,
        string summary,
        string description)
        => builder
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

    private static RouteHandlerBuilder DescribeDeleted(
        RouteHandlerBuilder builder,
        string name,
        string summary,
        string description)
        => builder
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

    private static RouteHandlerBuilder DescribeAction(
        RouteHandlerBuilder builder,
        string name,
        string summary,
        string description)
        => builder
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

    private static bool AreValidAbsoluteUris(IEnumerable<string> uris)
    {
        foreach (var value in uris)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildClientLocation(string clientId)
        => $"/admin/api/clients/{Uri.EscapeDataString(clientId)}";

    private static string BuildScopeLocation(string name)
        => $"/admin/api/scopes/{Uri.EscapeDataString(name)}";

    private static string BuildUserLocation(string id)
        => $"/admin/api/users/{Uri.EscapeDataString(id)}";
}