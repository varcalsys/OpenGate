## Architecture Overview

### Componentes

- **OpenGate.Server** (`src/OpenGate.Server`)
  - registra e configura o OpenIddict com defaults e presets de segurança

- **OpenGate.Data.EFCore** (`src/OpenGate.Data.EFCore`)
  - `OpenGateDbContext` herda de `IdentityDbContext<OpenGateUser>`
  - entidades extras: `UserProfile`, `AuditLog`, `UserSession`
  - migrations SQL Server em `Migrations/sqlserver`

- **OpenGate.Data.EFCore.Migrations.PostgreSql** (`src/OpenGate.Data.EFCore.Migrations.PostgreSql`)
  - migrations versionadas para PostgreSQL

- **OpenGate.Data.EFCore.Migrations.Sqlite** (`src/OpenGate.Data.EFCore.Migrations.Sqlite`)
  - migrations versionadas para SQLite

- **OpenGate.UI** (`src/OpenGate.UI`)
  - Razor Pages para:
    - Login (`/Account/Login`)
    - Register (`/Account/Register`)
    - Consent/Authorize (`/connect/authorize`)
    - Logout (`/connect/logout`)
  - static web assets + Tailwind CSS

### Fluxo (alto nível)

1. Cliente chama `/connect/authorize`
2. OpenIddict (com passthrough habilitado) delega para a Razor Page de consent
3. Se o usuário não estiver autenticado, o app desafia o cookie do Identity e redireciona para o Login
4. Login usa `SignInManager` para criar o cookie
5. Consent aceito: a UI retorna um principal e o OpenIddict emite code/token conforme o flow

### Banco e migrations

- `UseSqlServer(...)` aplica migrations do assembly base (`OpenGate.Data.EFCore`).
- `UsePostgreSql(...)` usa o assembly `OpenGate.Data.EFCore.Migrations.PostgreSql`.
- `UseSqlite(...)` usa o assembly `OpenGate.Data.EFCore.Migrations.Sqlite`.
