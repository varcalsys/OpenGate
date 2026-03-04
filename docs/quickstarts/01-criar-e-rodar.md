## Quickstart 1 — Criar e rodar com `dotnet new opengate-server`

### Pré-requisitos

- .NET SDK conforme `global.json` (atualmente: `10.0.103`)
- Docker (opcional, para PostgreSQL/Redis via compose)

### 1) Instalar o template localmente (sem afetar seu usuário)

No root do repo:

- `dotnet new install templates/opengate-server --debug:custom-hive artifacts/template-hive`

### 2) Gerar um servidor

Exemplo (gera em `samples/MinhaEmpresa.Identity`):

- `dotnet new opengate-server -n MinhaEmpresa.Identity -o samples/MinhaEmpresa.Identity --debug:custom-hive artifacts/template-hive`

Se você gerar fora de `samples/`, ajuste o caminho para `src/`:

- `--opengateSrcPath ../../src`

### 3) Escolher provider de banco

No projeto gerado, configure em `appsettings.json`:

- `OpenGate:DatabaseProvider`: `sqlserver` (default), `postgresql` ou `sqlite`
- `ConnectionStrings:OpenGate`: string correspondente ao provider

### 4) Rodar com Docker Compose (opcional)

No root do repositório:

- `docker compose up --build`

Isso sobe:

- `opengate` (app)
- `postgres` (PostgreSQL)
- `redis` (Redis)

### 5) Rodar localmente

Na pasta do projeto gerado:

- `dotnet run`

Abra:

- Login: `http://localhost:5148/Account/Login`
- Discovery: `http://localhost:5148/.well-known/openid-configuration`
- Health: `http://localhost:5148/health`

### 6) Migrations por provider

Scripts (root do repo):

- `./scripts/ef-add-migration.ps1 -Name AddSomething -Provider sqlserver|postgresql|sqlite`
- `./scripts/ef-update-db.ps1 -Provider sqlserver|postgresql|sqlite`

### Credenciais de demo (com `--seed true`)

- Usuário: `demo@opengate.test`
- Senha: `Demo@1234!abcd`
