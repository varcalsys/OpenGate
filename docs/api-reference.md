## API Reference (v0.1)

### OpenGate.Server

#### `IServiceCollection.AddOpenGate(Action<OpenGateOptions>? configure = null)`

- Arquivo: `src/OpenGate.Server/Extensions/OpenGateServiceCollectionExtensions.cs`
- Retorna: `OpenGateBuilder`
- Faz:
  - registra OpenIddict (Core + Server + Validation)
  - aplica endpoints/flows/defaults e o preset de segurança

#### `OpenGateBuilder.UseSqlServer(string connectionString)`

- Arquivo: `src/OpenGate.Server/OpenGateBuilder.cs`
- Configura `OpenGateDbContext` com SQL Server + `UseOpenIddict()`

#### `OpenGateBuilder.UsePostgreSql(string connectionString)`

- Arquivo: `src/OpenGate.Server/OpenGateBuilder.cs`
- Configura `OpenGateDbContext` com PostgreSQL + `UseOpenIddict()`

#### `OpenGateBuilder.UseSqlite(string connectionString)`

- Arquivo: `src/OpenGate.Server/OpenGateBuilder.cs`
- Configura `OpenGateDbContext` com SQLite + `UseOpenIddict()`

#### `OpenGateBuilder.UseDatabase(Action<DbContextOptionsBuilder> optionsAction)`

- Arquivo: `src/OpenGate.Server/OpenGateBuilder.cs`
- Permite plugar outros providers (ex.: Npgsql/SQLite).

#### `OpenGateBuilder.Build()`

- Registra:
  - EF Core + Identity via `AddOpenGateData(...)`
  - `SignInManager`
  - Cookies do Identity (`AddIdentityCookies()`)

### OpenGateOptions

- Arquivo: `src/OpenGate.Server/Options/OpenGateOptions.cs`
- Principais opções:
  - `SecurityPreset`: `Development | Production | HighSecurity`
  - `IssuerUri`: define o `issuer` no discovery
  - Paths (defaults):
    - `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo`
