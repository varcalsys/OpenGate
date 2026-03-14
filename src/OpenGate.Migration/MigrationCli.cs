using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Admin.Api.Contracts;
using OpenGate.Data.EFCore;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Migration;

public static class MigrationCli
{
    private const string OpenGateJsonSource = "opengate-json";
    private const string DuendeSource = "duende";
    private const string IdentityServer4Source = "is4";
    private const string ConfigurationDocumentFormat = "opengate-admin-configuration";
    private const int ConfigurationDocumentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (!TryParse(args, out var command, out var message, out var showHelp))
        {
            await (showHelp ? stdout : stderr).WriteLineAsync(showHelp ? HelpText : message);
            return showHelp ? 0 : 1;
        }

        var result = await ExecuteAsync(command!, cancellationToken);
        foreach (var line in result.OutputLines)
        {
            await stdout.WriteLineAsync(line);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            await stderr.WriteLineAsync(result.Error);
        }

        return result.ExitCode;
    }

    public static bool TryParse(
        IReadOnlyList<string> args,
        out MigrationCommand? command,
        out string message,
        out bool showHelp)
    {
        command = null;
        message = string.Empty;
        showHelp = false;

        if (args.Count == 0 || args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)))
        {
            showHelp = true;
            return false;
        }

        var index = 0;
        if (string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        string? source = null;
        string? provider = null;
        string? connectionString = null;
        string? inputPath = null;
        string? outputPlanPath = null;
        var dryRun = true;

        while (index < args.Count)
        {
            var current = args[index++];
            switch (current)
            {
                case "--source":
                    source = ReadValue(args, ref index, current, out message);
                    if (!string.IsNullOrWhiteSpace(message)) return false;
                    break;
                case "--provider":
                    provider = ReadValue(args, ref index, current, out message);
                    if (!string.IsNullOrWhiteSpace(message)) return false;
                    break;
                case "--connection-string":
                    connectionString = ReadValue(args, ref index, current, out message);
                    if (!string.IsNullOrWhiteSpace(message)) return false;
                    break;
                case "--input":
                    inputPath = ReadValue(args, ref index, current, out message);
                    if (!string.IsNullOrWhiteSpace(message)) return false;
                    break;
                case "--output-plan":
                    outputPlanPath = ReadValue(args, ref index, current, out message);
                    if (!string.IsNullOrWhiteSpace(message)) return false;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--apply":
                    dryRun = false;
                    break;
                default:
                    message = $"Argumento não reconhecido: {current}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(inputPath))
        {
            message = "Os argumentos --source, --provider, --connection-string e --input são obrigatórios.";
            return false;
        }

        provider = provider.Trim().ToLowerInvariant() switch
        {
            "sqlite" => "sqlite",
            "sqlserver" => "sqlserver",
            "postgresql" or "postgres" => "postgresql",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(provider))
        {
            message = "Provider inválido. Use: sqlite, sqlserver ou postgresql.";
            return false;
        }

        command = new MigrationCommand(
            source.Trim(),
            provider,
            connectionString.Trim(),
            Path.GetFullPath(inputPath.Trim()),
            dryRun,
            string.IsNullOrWhiteSpace(outputPlanPath) ? null : Path.GetFullPath(outputPlanPath.Trim()));
        return true;
    }

    public static async Task<MigrationExecutionResult> ExecuteAsync(MigrationCommand command, CancellationToken cancellationToken)
    {
        if (!File.Exists(command.InputPath))
        {
            return Failure($"Arquivo de entrada não encontrado: {command.InputPath}");
        }

        var documentResult = await LoadDocumentAsync(command, cancellationToken);
        if (!string.IsNullOrWhiteSpace(documentResult.Error))
        {
            return Failure(documentResult.Error, documentResult.ExitCode);
        }

        var document = documentResult.Document;
        if (document is null)
        {
            return Failure("Não foi possível desserializar o documento JSON de configuração.");
        }

        var services = BuildServices(command.Provider, command.ConnectionString);
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();

        if (!command.DryRun)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        MigrationPlan plan;
        try
        {
            plan = await BuildPlanAsync(document, applicationManager, scopeManager, cancellationToken);
        }
        catch (Exception exception) when (command.DryRun)
        {
            return Failure($"Falha ao inspecionar o banco alvo em dry-run. Garanta que o schema OpenGate já exista. Detalhe: {exception.Message}");
        }

        if (plan.Errors.Count > 0)
        {
            return Failure(string.Join(Environment.NewLine, plan.Errors));
        }

        plan = plan with
        {
            Warnings = MergeMessages(plan.Warnings, documentResult.Warnings),
            Notes = MergeMessages(plan.Notes, NormalizeValues(document.Notes))
        };

        var outputLines = BuildReportLines(command, plan);
        if (command.DryRun)
        {
            await WritePlanFileAsync(command, document, plan, cancellationToken);
            return new MigrationExecutionResult(0, outputLines, null);
        }

        await ApplyScopesAsync(document.Scopes ?? [], scopeManager, cancellationToken);
        await ApplyClientsAsync(document.Clients ?? [], applicationManager, cancellationToken);

        db.AuditLogs.Add(new()
        {
            EventType = "Migration.ConfigurationImported",
            Details = JsonSerializer.Serialize(new
            {
                command.Source,
                command.Provider,
                command.InputPath,
                plan.ScopeCreates,
                plan.ScopeUpdates,
                plan.ClientCreates,
                plan.ClientUpdates,
                plan.Warnings,
                ScopeNames = (document.Scopes ?? []).Select(item => item.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                ClientIds = (document.Clients ?? []).Select(item => item.ClientId).Where(clientId => !string.IsNullOrWhiteSpace(clientId)).ToArray()
            }),
            Succeeded = true,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        await WritePlanFileAsync(command, document, plan, cancellationToken);

        return new MigrationExecutionResult(0, [.. outputLines, "Execução concluída com sucesso."], null);
    }

    public static string HelpText =>
        "OpenGate Migration CLI\n" +
        "Uso:\n" +
        "  dotnet run --project src/OpenGate.Migration -- migrate --source opengate-json --provider sqlite --connection-string \"Data Source=opengate.db\" --input .\\config.json [--dry-run|--apply]\n" +
        "  dotnet run --project src/OpenGate.Migration -- migrate --source duende --provider sqlite --connection-string \"Data Source=opengate.db\" --input .\\duende.json [--dry-run|--apply]\n" +
        "  dotnet opengate migrate --source is4 --provider sqlite --connection-string \"Data Source=opengate.db\" --input .\\is4.json --dry-run --output-plan .\\artifacts\\migration-plan.json\n\n" +
        "Opções:\n" +
        "  --source              opengate-json | duende | is4\n" +
        "  --provider            sqlite | sqlserver | postgresql\n" +
        "  --connection-string   connection string do banco OpenGate de destino\n" +
        "  --input               caminho para o arquivo JSON\n" +
        "  --output-plan         caminho opcional para salvar o plano em JSON\n" +
        "  --dry-run             apenas calcula o plano (default)\n" +
        "  --apply               aplica a migração no banco alvo\n\n" +
        "Notas:\n" +
        "  - 'duende' e 'is4' aceitam um JSON representativo com Clients, ApiScopes, ApiResources e IdentityResources.\n" +
        "  - A CLI aceita tanto raiz direta quanto envelope 'IdentityServer'.\n" +
        "  - Quando '--output-plan' é usado, a CLI persiste um resumo JSON com warnings, notes e contagens.\n" +
        "  - Como dotnet tool, o comando exposto é 'dotnet opengate'.";

    private static async Task<LoadedDocumentResult> LoadDocumentAsync(MigrationCommand command, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(command.InputPath);

        if (string.Equals(command.Source, OpenGateJsonSource, StringComparison.OrdinalIgnoreCase))
        {
            return new LoadedDocumentResult(
                await JsonSerializer.DeserializeAsync<AdminConfigurationDocument>(stream, JsonOptions, cancellationToken),
                [],
                null,
                0);
        }

        if (string.Equals(command.Source, DuendeSource, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.Source, IdentityServer4Source, StringComparison.OrdinalIgnoreCase))
        {
            var sourceDocument = await JsonSerializer.DeserializeAsync<DuendeConfigurationEnvelope>(stream, JsonOptions, cancellationToken);
            var sourceLabel = string.Equals(command.Source, IdentityServer4Source, StringComparison.OrdinalIgnoreCase)
                ? "IdentityServer4"
                : "Duende IdentityServer";
            var converted = sourceDocument is null ? null : ConvertDuendeDocument(sourceDocument.Normalize(), sourceLabel);
            return sourceDocument is null
                ? new LoadedDocumentResult(null, [], $"Não foi possível desserializar o documento JSON de '{command.Source}'.", 1)
                : new LoadedDocumentResult(converted!.Document, converted.Warnings, null, 0);
        }

        return new LoadedDocumentResult(
            null,
            [],
            $"Source '{command.Source}' não reconhecido. Use '{OpenGateJsonSource}', '{DuendeSource}' ou '{IdentityServer4Source}'.",
            2);
    }

    private static ServiceCollection BuildServices(string provider, string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddOpenGate(options => options.SecurityPreset = OpenGateSecurityPreset.Development);
        _ = provider switch
        {
            "sqlite" => builder.UseSqlite(connectionString),
            "sqlserver" => builder.UseSqlServer(connectionString),
            "postgresql" => builder.UsePostgreSql(connectionString),
            _ => throw new InvalidOperationException($"Provider não suportado: {provider}")
        };

        builder.Build();
        return services;
    }

    private static async Task<MigrationPlan> BuildPlanAsync(
        AdminConfigurationDocument document,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDocumentShape(document);
        var warnings = new List<string>();
        var scopeCreateNames = new List<string>();
        var scopeUpdateNames = new List<string>();

        foreach (var scope in document.Scopes ?? [])
        {
            var existingScope = await scopeManager.FindByNameAsync(scope.Name!, cancellationToken);
            if (existingScope is null) scopeCreateNames.Add(scope.Name!);
            else scopeUpdateNames.Add(scope.Name!);
        }

        var clientCreateIds = new List<string>();
        var clientUpdateIds = new List<string>();
        foreach (var client in document.Clients ?? [])
        {
            var existingApplication = await applicationManager.FindByClientIdAsync(client.ClientId!, cancellationToken);
            if (existingApplication is null)
            {
                clientCreateIds.Add(client.ClientId!);
                if (string.Equals(client.ClientType, ClientTypes.Confidential, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(client.ClientSecret))
                {
                    errors.Add($"Client confidential '{client.ClientId}' exige clientSecret para criação.");
                }
            }
            else
            {
                clientUpdateIds.Add(client.ClientId!);
                if (string.Equals(client.ClientType, ClientTypes.Confidential, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(client.ClientSecret))
                {
                    warnings.Add($"Client confidential '{client.ClientId}' será atualizado sem alterar clientSecret.");
                }
            }
        }

        return new MigrationPlan(
            ScopeCreates: scopeCreateNames.Count,
            ScopeUpdates: scopeUpdateNames.Count,
            ClientCreates: clientCreateIds.Count,
            ClientUpdates: clientUpdateIds.Count,
            Errors: errors,
            Warnings: warnings,
            Notes: NormalizeValues(document.Notes),
            ScopeCreateNames: scopeCreateNames.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ScopeUpdateNames: scopeUpdateNames.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ClientCreateIds: clientCreateIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ClientUpdateIds: clientUpdateIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ConvertedDocumentResult ConvertDuendeDocument(DuendeConfigurationDocument source, string sourceLabel)
    {
        var resourceLookup = BuildApiScopeResourceLookup(source.ApiResources);
        var scopes = new List<AdminScopeRequest>();
        var warnings = new List<string>();

        foreach (var apiScope in source.ApiScopes ?? [])
        {
            scopes.Add(new AdminScopeRequest
            {
                Name = apiScope.Name,
                DisplayName = apiScope.DisplayName,
                Description = apiScope.Description,
                Resources = resourceLookup.TryGetValue(apiScope.Name ?? string.Empty, out var resources) ? resources : []
            });
        }

        foreach (var identityResource in source.IdentityResources ?? [])
        {
            if (ShouldSkipBuiltInIdentityScope(identityResource.Name))
            {
                continue;
            }

            scopes.Add(new AdminScopeRequest
            {
                Name = identityResource.Name,
                DisplayName = identityResource.DisplayName,
                Description = identityResource.Description,
                Resources = []
            });
        }

        var clients = (source.Clients ?? [])
            .Select(BuildClientRequestFromDuende)
            .ToArray();

        var skippedBuiltInScopes = NormalizeValues(source.IdentityResources?
            .Where(item => ShouldSkipBuiltInIdentityScope(item.Name))
            .Select(item => item.Name));
        if (skippedBuiltInScopes.Length > 0)
        {
            warnings.Add($"Built-in identity resources serão ignorados na persistência de scopes: {string.Join(", ", skippedBuiltInScopes)}.");
        }

        var clientsWithMultipleSecrets = (source.Clients ?? [])
            .Where(client => (client.ClientSecrets?.Count ?? 0) > 1)
            .Select(client => client.ClientId)
            .Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .Select(clientId => clientId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(clientId => clientId, StringComparer.Ordinal)
            .ToArray();
        if (clientsWithMultipleSecrets.Length > 0)
        {
            warnings.Add($"Somente o primeiro clientSecret será preservado para: {string.Join(", ", clientsWithMultipleSecrets)}.");
        }

        var clientsWithAdvancedGrantTypes = (source.Clients ?? [])
            .Select(client => new
            {
                client.ClientId,
                GrantTypes = NormalizeValues(client.AllowedGrantTypes)
                    .Where(grantType => grantType is not GrantTypes.AuthorizationCode and not GrantTypes.RefreshToken and not GrantTypes.ClientCredentials)
                    .ToArray()
            })
            .Where(item => item.GrantTypes.Length > 0 && !string.IsNullOrWhiteSpace(item.ClientId))
            .OrderBy(item => item.ClientId, StringComparer.Ordinal)
            .ToArray();
        if (clientsWithAdvancedGrantTypes.Length > 0)
        {
            var advancedGrantTypeSummary = string.Join(
                "; ",
                clientsWithAdvancedGrantTypes.Select(item => $"{item.ClientId}=[{string.Join(", ", item.GrantTypes)}]"));
            warnings.Add($"Alguns grant types exigem validação manual no OpenGate/OpenIddict: {advancedGrantTypeSummary}.");
        }

        return new ConvertedDocumentResult(
            new AdminConfigurationDocument
            {
                Format = ConfigurationDocumentFormat,
                Version = ConfigurationDocumentVersion,
                GeneratedAt = DateTimeOffset.UtcNow,
                Notes =
                [
                    $"Converted from {sourceLabel} configuration JSON.",
                    "Built-in identity resources (openid, profile, email, roles, offline_access) are not materialized as stored OpenIddict scopes."
                ],
                Clients = clients,
                Scopes = scopes
            },
            warnings);
    }

    private static List<string> ValidateDocumentShape(AdminConfigurationDocument document)
    {
        var errors = new List<string>();
        if (!string.Equals(document.Format, ConfigurationDocumentFormat, StringComparison.Ordinal))
        {
            errors.Add($"Format inválido. Esperado: '{ConfigurationDocumentFormat}'.");
        }

        if (document.Version != ConfigurationDocumentVersion)
        {
            errors.Add($"Version inválida. Esperado: '{ConfigurationDocumentVersion}'.");
        }

        if ((document.Clients?.Count ?? 0) == 0 && (document.Scopes?.Count ?? 0) == 0)
        {
            errors.Add("O documento precisa conter ao menos um client ou scope.");
        }

        AddDuplicateErrors(errors, "scope", document.Scopes?.Select(item => item.Name) ?? []);
        AddDuplicateErrors(errors, "client", document.Clients?.Select(item => item.ClientId) ?? []);

        foreach (var scope in document.Scopes ?? [])
        {
            if (string.IsNullOrWhiteSpace(scope.Name))
            {
                errors.Add("Todos os scopes precisam ter name.");
            }
        }

        foreach (var client in document.Clients ?? [])
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
            {
                errors.Add("Todos os clients precisam ter clientId.");
            }

            foreach (var uri in NormalizeValues(client.RedirectUris))
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                {
                    errors.Add($"Redirect URI inválida no client '{client.ClientId}': {uri}");
                }
            }
        }

        return errors;
    }

    private static Dictionary<string, string[]> BuildApiScopeResourceLookup(IReadOnlyList<DuendeApiResource>? resources)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var resource in resources ?? [])
        {
            foreach (var scopeName in NormalizeValues(resource.Scopes))
            {
                if (!result.TryGetValue(scopeName, out var values))
                {
                    values = new HashSet<string>(StringComparer.Ordinal);
                    result[scopeName] = values;
                }

                if (!string.IsNullOrWhiteSpace(resource.Name))
                {
                    values.Add(resource.Name.Trim());
                }
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static AdminClientRequest BuildClientRequestFromDuende(DuendeClient source)
    {
        var allowedScopes = NormalizeValues(source.AllowedScopes).ToList();
        if (source.AllowOfflineAccess == true && !allowedScopes.Contains(Scopes.OfflineAccess, StringComparer.Ordinal))
        {
            allowedScopes.Add(Scopes.OfflineAccess);
        }

        var permissions = BuildPermissionsFromDuende(source, allowedScopes);
        var secrets = NormalizeValues(source.ClientSecrets?.Select(secret => secret.Value));
        var hasSecret = secrets.Length > 0;
        var requireSecret = source.RequireClientSecret ?? hasSecret;

        return new AdminClientRequest
        {
            ClientId = source.ClientId,
            ClientSecret = hasSecret ? secrets[0] : null,
            DisplayName = string.IsNullOrWhiteSpace(source.ClientName) ? source.ClientId : source.ClientName,
            ClientType = requireSecret ? ClientTypes.Confidential : ClientTypes.Public,
            ConsentType = source.RequireConsent == true ? ConsentTypes.Explicit : ConsentTypes.Implicit,
            RedirectUris = NormalizeValues(source.RedirectUris),
            PostLogoutRedirectUris = NormalizeValues(source.PostLogoutRedirectUris),
            Permissions = permissions,
            Requirements = source.RequirePkce == true ? [Requirements.Features.ProofKeyForCodeExchange] : []
        };
    }

    private static string[] BuildPermissionsFromDuende(DuendeClient source, IReadOnlyCollection<string> allowedScopes)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var grantTypes = NormalizeValues(source.AllowedGrantTypes);

        foreach (var grantType in grantTypes)
        {
            switch (grantType)
            {
                case GrantTypes.AuthorizationCode:
                    permissions.Add(Permissions.Endpoints.Authorization);
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add(Permissions.ResponseTypes.Code);
                    permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    break;
                case GrantTypes.ClientCredentials:
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add(Permissions.GrantTypes.ClientCredentials);
                    break;
                case GrantTypes.RefreshToken:
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;
                case "implicit":
                    permissions.Add(Permissions.Endpoints.Authorization);
                    permissions.Add("gt:implicit");
                    break;
                case "hybrid":
                    permissions.Add(Permissions.Endpoints.Authorization);
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add(Permissions.ResponseTypes.Code);
                    permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    break;
                case "password":
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add("gt:password");
                    break;
                case "device_code":
                case "urn:ietf:params:oauth:grant-type:device_code":
                    permissions.Add("ept:device_authorization");
                    permissions.Add(Permissions.Endpoints.Token);
                    permissions.Add("gt:urn:ietf:params:oauth:grant-type:device_code");
                    break;
                default:
                    permissions.Add($"gt:{grantType}");
                    break;
            }
        }

        if (NormalizeValues(source.PostLogoutRedirectUris).Length > 0 || grantTypes.Contains(GrantTypes.AuthorizationCode, StringComparer.Ordinal))
        {
            permissions.Add(Permissions.Endpoints.EndSession);
        }

        foreach (var scope in allowedScopes)
        {
            permissions.Add($"{Permissions.Prefixes.Scope}{scope}");
        }

        return permissions.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool ShouldSkipBuiltInIdentityScope(string? name)
        => string.Equals(name, Scopes.OpenId, StringComparison.Ordinal)
           || string.Equals(name, Scopes.Profile, StringComparison.Ordinal)
           || string.Equals(name, Scopes.Email, StringComparison.Ordinal)
           || string.Equals(name, Scopes.Roles, StringComparison.Ordinal)
           || string.Equals(name, Scopes.OfflineAccess, StringComparison.Ordinal);

    private static async Task ApplyScopesAsync(
        IReadOnlyList<AdminScopeRequest> requests,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            var scope = await scopeManager.FindByNameAsync(request.Name!, cancellationToken);
            if (scope is null)
            {
                var descriptor = new OpenIddictScopeDescriptor();
                ApplyScopeRequest(descriptor, request, isCreate: true);
                await scopeManager.CreateAsync(descriptor, cancellationToken);
                continue;
            }

            var updatedDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(updatedDescriptor, scope, cancellationToken);
            ApplyScopeRequest(updatedDescriptor, request, isCreate: false);
            await scopeManager.UpdateAsync(scope, updatedDescriptor, cancellationToken);
        }
    }

    private static async Task ApplyClientsAsync(
        IReadOnlyList<AdminClientRequest> requests,
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            var application = await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken);
            if (application is null)
            {
                var descriptor = new OpenIddictApplicationDescriptor();
                ApplyClientRequest(descriptor, request, isCreate: true);
                await applicationManager.CreateAsync(descriptor, cancellationToken);
                continue;
            }

            var updatedDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(updatedDescriptor, application, cancellationToken);
            ApplyClientRequest(updatedDescriptor, request, isCreate: false);
            await applicationManager.UpdateAsync(application, updatedDescriptor, cancellationToken);
        }
    }

    private static void ApplyScopeRequest(OpenIddictScopeDescriptor descriptor, AdminScopeRequest request, bool isCreate)
    {
        if (isCreate)
        {
            descriptor.Name = request.Name?.Trim();
        }

        descriptor.DisplayName = NullIfWhiteSpace(request.DisplayName);
        descriptor.Description = NullIfWhiteSpace(request.Description);
        descriptor.Resources.Clear();

        foreach (var resource in NormalizeValues(request.Resources))
        {
            descriptor.Resources.Add(resource);
        }
    }

    private static void ApplyClientRequest(OpenIddictApplicationDescriptor descriptor, AdminClientRequest request, bool isCreate)
    {
        if (isCreate)
        {
            descriptor.ClientId = request.ClientId?.Trim();
        }

        descriptor.DisplayName = NullIfWhiteSpace(request.DisplayName);
        descriptor.ClientType = string.IsNullOrWhiteSpace(request.ClientType) ? ClientTypes.Public : request.ClientType.Trim();
        descriptor.ConsentType = string.IsNullOrWhiteSpace(request.ConsentType) ? ConsentTypes.Explicit : request.ConsentType.Trim();

        if (isCreate || !string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            descriptor.ClientSecret = NullIfWhiteSpace(request.ClientSecret);
        }

        descriptor.RedirectUris.Clear();
        foreach (var redirectUri in NormalizeValues(request.RedirectUris))
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (var postLogoutRedirectUri in NormalizeValues(request.PostLogoutRedirectUris))
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri, UriKind.Absolute));
        }

        descriptor.Permissions.Clear();
        foreach (var permission in NormalizeValues(request.Permissions))
        {
            descriptor.Permissions.Add(permission);
        }

        descriptor.Requirements.Clear();
        foreach (var requirement in NormalizeValues(request.Requirements))
        {
            descriptor.Requirements.Add(requirement);
        }
    }

    private static List<string> BuildReportLines(MigrationCommand command, MigrationPlan plan)
    {
        var lines = new List<string>
        {
            $"Source: {command.Source}",
            $"Provider: {command.Provider}",
            $"Modo: {(command.DryRun ? "dry-run" : "apply")}",
            $"Input: {command.InputPath}",
            $"Scopes: criar={plan.ScopeCreates}, atualizar={plan.ScopeUpdates}",
            $"Clients: criar={plan.ClientCreates}, atualizar={plan.ClientUpdates}"
        };

        if (!string.IsNullOrWhiteSpace(command.OutputPlanPath))
        {
            lines.Add($"Plano JSON: {command.OutputPlanPath}");
        }

        AppendEntitySummary(lines, "Scopes a criar", plan.ScopeCreateNames);
        AppendEntitySummary(lines, "Scopes a atualizar", plan.ScopeUpdateNames);
        AppendEntitySummary(lines, "Clients a criar", plan.ClientCreateIds);
        AppendEntitySummary(lines, "Clients a atualizar", plan.ClientUpdateIds);

        if (plan.Warnings.Count > 0)
        {
            lines.Add($"Warnings: {plan.Warnings.Count}");
            lines.AddRange(plan.Warnings.Select(warning => $"WARNING: {warning}"));
        }

        if (plan.Notes.Count > 0)
        {
            lines.Add($"Notes: {plan.Notes.Count}");
            lines.AddRange(plan.Notes.Select(note => $"NOTE: {note}"));
        }

        return lines;
    }

    private static async Task WritePlanFileAsync(
        MigrationCommand command,
        AdminConfigurationDocument document,
        MigrationPlan plan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.OutputPlanPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(command.OutputPlanPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(command.OutputPlanPath);
        await JsonSerializer.SerializeAsync(
            stream,
            BuildPlanArtifact(command, document, plan),
            JsonOptions,
            cancellationToken);
    }

    private static MigrationPlanArtifact BuildPlanArtifact(
        MigrationCommand command,
        AdminConfigurationDocument document,
        MigrationPlan plan)
        => new(
            command.Source,
            command.Provider,
            command.DryRun ? "dry-run" : "apply",
            command.InputPath,
            command.OutputPlanPath,
            DateTimeOffset.UtcNow,
            plan.ScopeCreates,
            plan.ScopeUpdates,
            plan.ClientCreates,
            plan.ClientUpdates,
            plan.Warnings,
            plan.Notes,
            plan.Errors,
            NormalizeValues((document.Scopes ?? []).Select(item => item.Name)),
            NormalizeValues((document.Clients ?? []).Select(item => item.ClientId)),
            plan.ScopeCreateNames,
            plan.ScopeUpdateNames,
            plan.ClientCreateIds,
            plan.ClientUpdateIds);

    private static MigrationExecutionResult Failure(string error, int exitCode = 1)
        => new(exitCode, [], error);

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AppendEntitySummary(List<string> lines, string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        const int maxItemsInPreview = 10;
        var preview = values.Take(maxItemsInPreview).ToArray();
        var suffix = values.Count > maxItemsInPreview ? $" (+{values.Count - maxItemsInPreview} mais)" : string.Empty;
        lines.Add($"{label}: {string.Join(", ", preview)}{suffix}");
    }

    private static string[] MergeMessages(params IEnumerable<string?>[] collections)
        => collections
            .SelectMany(collection => collection)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] NormalizeValues(IEnumerable<string?>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? [];

    private static void AddDuplicateErrors(List<string> errors, string label, IEnumerable<string?> values)
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
            errors.Add($"Há {label}s duplicados no documento: {string.Join(", ", duplicates)}");
        }
    }

    private static string? ReadValue(IReadOnlyList<string> args, ref int index, string optionName, out string message)
    {
        if (index >= args.Count)
        {
            message = $"O argumento {optionName} exige um valor.";
            return null;
        }

        message = string.Empty;
        return args[index++];
    }
}

public sealed record MigrationCommand(string Source, string Provider, string ConnectionString, string InputPath, bool DryRun, string? OutputPlanPath = null);

public sealed record MigrationPlan(
    int ScopeCreates,
    int ScopeUpdates,
    int ClientCreates,
    int ClientUpdates,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> ScopeCreateNames,
    IReadOnlyList<string> ScopeUpdateNames,
    IReadOnlyList<string> ClientCreateIds,
    IReadOnlyList<string> ClientUpdateIds);

public sealed record MigrationExecutionResult(int ExitCode, IReadOnlyList<string> OutputLines, string? Error);

public sealed record MigrationPlanArtifact(
    string Source,
    string Provider,
    string Mode,
    string InputPath,
    string? OutputPlanPath,
    DateTimeOffset GeneratedAt,
    int ScopeCreates,
    int ScopeUpdates,
    int ClientCreates,
    int ClientUpdates,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ScopeNames,
    IReadOnlyList<string> ClientIds,
    IReadOnlyList<string> ScopeCreateNames,
    IReadOnlyList<string> ScopeUpdateNames,
    IReadOnlyList<string> ClientCreateIds,
    IReadOnlyList<string> ClientUpdateIds);

internal sealed record LoadedDocumentResult(AdminConfigurationDocument? Document, IReadOnlyList<string> Warnings, string? Error, int ExitCode);

internal sealed record ConvertedDocumentResult(AdminConfigurationDocument Document, IReadOnlyList<string> Warnings);