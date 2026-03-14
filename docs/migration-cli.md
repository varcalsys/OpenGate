## Migration CLI (`OpenGate.Migration`)

Guia da CLI de migração usada para importar configuração de clientes e scopes para um banco OpenGate.

### Status atual

- fontes suportadas:
  - `opengate-json`
  - `duende`
  - `is4`
- providers suportados:
  - `sqlite`
  - `sqlserver`
  - `postgresql`
- modos suportados:
  - `--dry-run` (default)
  - `--apply`

### Executar a CLI

Atualmente você pode usar a CLI de duas formas.

#### Opção 1: rodar pelo projeto

- `dotnet run --project src/OpenGate.Migration -- --help`
- `dotnet run --project src/OpenGate.Migration -- migrate --source opengate-json --provider sqlite --connection-string "Data Source=opengate.db" --input .\config.json --dry-run`

#### Opção 2: empacotar e instalar como `dotnet tool`

Gerar o pacote localmente:

- `dotnet pack src/OpenGate.Migration/OpenGate.Migration.csproj -c Release --no-restore -o .\artifacts\packages`

Instalar em um `tool-path` local:

- `dotnet tool install --tool-path .\artifacts\tools OpenGate.Migration --add-source .\artifacts\packages --version 0.1.0-alpha`

Atualizar uma instalação já existente:

- `dotnet tool update --tool-path .\artifacts\tools OpenGate.Migration --add-source .\artifacts\packages --version 0.1.0-alpha`

Se o comando estiver disponível no `PATH` (por instalação global ou local manifest), a invocação final fica:

- `dotnet opengate --help`
- `dotnet opengate migrate --source duende --provider sqlite --connection-string "Data Source=opengate.db" --input .\duende.json --dry-run --output-plan .\artifacts\migration-plan.json`

Executar a tool empacotada:

- `.\artifacts\tools\dotnet-opengate --help`
- `.\artifacts\tools\dotnet-opengate migrate --source duende --provider sqlite --connection-string "Data Source=opengate.db" --input .\duende.json --dry-run --output-plan .\artifacts\migration-plan.json`

### Argumentos

- `--source`
  - `opengate-json | duende | is4`
- `--provider`
  - `sqlite | sqlserver | postgresql`
- `--connection-string`
  - connection string do banco OpenGate de destino
- `--input`
  - caminho do arquivo JSON de entrada
- `--output-plan`
  - caminho opcional para persistir o plano da migração em JSON
- `--dry-run`
  - calcula o plano sem aplicar mudanças
- `--apply`
  - aplica a migração no banco alvo

### Comportamento por modo

#### `--dry-run`

- valida o documento de entrada
- inspeciona o banco alvo e calcula quantos clients/scopes serão criados ou atualizados
- não aplica alterações
- não executa migrations do banco
- se usado com `--output-plan`, grava o plano em JSON

> Importante: no modo `--dry-run`, o schema OpenGate já precisa existir no banco de destino.

#### `--apply`

- executa `Database.Migrate()` no banco alvo
- cria/atualiza scopes
- cria/atualiza clients
- grava auditoria em `Migration.ConfigurationImported`
- se usado com `--output-plan`, grava o plano final em JSON

### Fonte `opengate-json`

Esta fonte usa o mesmo contrato de import/export da Admin API.

Formato esperado:

```json
{
  "format": "opengate-admin-configuration",
  "version": 1,
  "generatedAt": "2026-03-14T12:00:00Z",
  "notes": ["Exported from OpenGate Admin API."],
  "clients": [
    {
      "clientId": "orders-cli",
      "displayName": "Orders CLI",
      "clientType": "public",
      "consentType": "explicit",
      "redirectUris": ["http://localhost/orders-cli/callback"],
      "permissions": ["ept:authorization", "ept:token", "gt:authorization_code", "rst:code", "scp:openid", "scp:orders-api"],
      "requirements": ["ft:pkce"]
    }
  ],
  "scopes": [
    {
      "name": "orders-api",
      "displayName": "Orders API",
      "description": "Orders scope",
      "resources": ["orders_resource"]
    }
  ]
}
```

Exemplo versionado no repositório:

- `docs/examples/migration/opengate-config.json`

### Fontes `duende` e `is4`

As fontes `duende` e `is4` compartilham o mesmo adapter de compatibilidade.

O JSON de entrada pode vir em um destes formatos:

1. raiz direta com `clients`, `apiScopes`, `apiResources`, `identityResources`
2. envelope `identityServer: { ... }`

Exemplo mínimo:

```json
{
  "identityServer": {
    "clients": [
      {
        "clientId": "spa-app",
        "clientName": "SPA App",
        "requireClientSecret": false,
        "requireConsent": true,
        "requirePkce": true,
        "allowedGrantTypes": ["authorization_code", "refresh_token"],
        "redirectUris": ["http://localhost:5003/signin-oidc"],
        "postLogoutRedirectUris": ["http://localhost:5003/signout-callback-oidc"],
        "allowedScopes": ["openid", "profile", "orders-api", "tenant"]
      }
    ],
    "apiScopes": [
      {
        "name": "orders-api",
        "displayName": "Orders API"
      }
    ],
    "apiResources": [
      {
        "name": "orders_resource",
        "scopes": ["orders-api"]
      }
    ],
    "identityResources": [
      {
        "name": "tenant",
        "displayName": "Tenant"
      }
    ]
  }
}
```

Exemplos versionados no repositório:

- `docs/examples/migration/duende-config.json`
- `docs/examples/migration/is4-config.json`

### Mapeamentos suportados hoje

#### Clients

- `clientId`
- `clientName` -> `displayName`
- `requireClientSecret` / `clientSecrets`
- `requireConsent`
- `requirePkce`
- `allowedGrantTypes`
- `redirectUris`
- `postLogoutRedirectUris`
- `allowedScopes`
- `allowOfflineAccess`

#### Scopes

- `apiScopes` -> scopes persistidos no OpenGate
- `apiResources[].scopes` -> `resources` do scope
- `identityResources` customizados -> scopes persistidos

### Warnings e limitações conhecidas

O plano pode emitir warnings como:

- built-in identity resources ignorados (`openid`, `profile`, `email`, `roles`, `offline_access`)
- múltiplos `clientSecrets`: só o primeiro é preservado
- grant types mais avançados/legados exigem validação manual no OpenGate/OpenIddict
- client confidential existente sem novo `clientSecret`: o segredo atual não é alterado

Limitações atuais da baseline:

- importa apenas **clients** e **scopes**
- não importa usuários, roles, sessões ou tokens
- não materializa built-in identity resources como scopes armazenados
- o stdout mostra preview das listas por entidade; o JSON do plano mantém a lista completa

### `--output-plan`

Quando `--output-plan` é informado, a CLI grava um JSON com resumo da execução:

- `source`, `provider`, `mode`
- `inputPath`, `outputPlanPath`, `generatedAt`
- `scopeCreates`, `scopeUpdates`
- `clientCreates`, `clientUpdates`
- `warnings`, `notes`, `errors`
- `scopeNames`, `clientIds`
- `scopeCreateNames`, `scopeUpdateNames`
- `clientCreateIds`, `clientUpdateIds`

Isso permite diferenciar explicitamente, por entidade, o que será:

- criado
- atualizado

No stdout, a CLI também mostra um preview legível dessas listas, por exemplo:

- `Scopes a criar: billing-api`
- `Scopes a atualizar: orders-api`
- `Clients a criar: billing-cli`
- `Clients a atualizar: orders-cli`

Exemplo de uso:

- `dotnet run --project src/OpenGate.Migration -- migrate --source duende --provider sqlite --connection-string "Data Source=opengate.db" --input .\duende.json --dry-run --output-plan .\artifacts\migration-plan.json`
- `.\artifacts\tools\opengate-migration migrate --source duende --provider sqlite --connection-string "Data Source=opengate.db" --input .\duende.json --dry-run --output-plan .\artifacts\migration-plan.json`

### Fluxo recomendado

1. executar `--dry-run`
2. revisar contagens e warnings no stdout
3. se necessário, inspecionar o arquivo de `--output-plan`
4. corrigir o JSON de origem ou ajustar manualmente os pontos não mapeados
5. executar `--apply`

### Observações operacionais

- `--apply` grava auditoria em `opengate.AuditLogs`
- o provider do banco controla o assembly de migrations automaticamente
- para SQLite, SQL Server e PostgreSQL o comportamento da CLI é o mesmo do ponto de vista funcional

### Checklist operacional recomendado

Antes de executar uma migração real:

1. confirmar o provider e a connection string do ambiente de destino
2. garantir que existe backup/restore testado do banco alvo
3. executar `--dry-run`
4. revisar contagens, warnings e o arquivo de `--output-plan`
5. validar manualmente os pontos não mapeados automaticamente

Na execução real:

1. usar `--apply` com o mesmo arquivo validado no dry-run
2. registrar o arquivo de plano gerado junto ao change record da migração
3. validar no banco alvo se clients/scopes esperados foram criados ou atualizados
4. revisar a auditoria `Migration.ConfigurationImported`

Após a migração:

1. testar login/token com pelo menos um client migrado
2. validar redirect URIs, scopes e consentimento
3. registrar eventuais ajustes manuais aplicados após a importação

### Próximos refinamentos desejáveis

- diff mais rico por entidade (por exemplo, metadados resumidos por client/scope)