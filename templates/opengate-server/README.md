## Template: `opengate-server`

Este template **in-repo** foi feito para gerar um novo servidor dentro do próprio repositório OpenGate, usando `ProjectReference` para `src/`.

### Instalar localmente (sem afetar seu ambiente)

Para testar sem instalar no seu perfil de usuário, use um hive customizado:

1. No root do repositório:
   - `dotnet new install templates/opengate-server --debug:custom-hive artifacts/template-hive`
2. Criar um servidor dentro de `samples/`:
   - `dotnet new opengate-server -n MinhaEmpresa.Identity -o samples/MinhaEmpresa.Identity --debug:custom-hive artifacts/template-hive`

> Dica: se você gerar fora de `samples/`, ajuste `--opengateSrcPath` para apontar para `src/`.

### Parâmetros

- `--opengateSrcPath` (default: `../../src`) — caminho relativo para `src/`.
- `--seed` (default: `true`) — inclui o seeding de demo (usuário + clients + migrations).
- `--issuerUri` (default: `http://localhost:5001`) — valor para `OpenGate:IssuerUri`.

### Banco de dados

No `appsettings.json`, configure `OpenGate:DatabaseProvider` com:

- `sqlserver` (default)
- `postgresql`
- `sqlite`
