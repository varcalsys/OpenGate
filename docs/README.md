## OpenGate Docs

Índice da documentação do repositório.

### Quickstarts

1. [Criar e rodar um servidor com `dotnet new opengate-server`](quickstarts/01-criar-e-rodar.md)
2. [Authorization Code + PKCE (Postman)](quickstarts/02-auth-code-pkce.md)
3. [Client Credentials (curl)](quickstarts/03-client-credentials.md)

### Referência

- [API Reference](api-reference.md)
- [Architecture Overview](architecture.md)
- [Migration CLI](migration-cli.md)
- [Status da Fase 1 (MVP)](fase1-status.md)
- [CI/CD e Qualidade](ci-cd.md)
- [Migrations por provider](../src/OpenGate.Data.EFCore/Migrations/README.md)

### Samples

- `samples/OpenGate.Sample.Basic` (servidor base + UI + seed)
- `samples/OpenGate.Sample.ProtectedApi` (API protegida por bearer token)
- `samples/OpenGate.Sample.ConsoleM2M` (client credentials)
- `samples/OpenGate.Sample.DeviceFlow` (device authorization flow)
- `samples/OpenGate.Sample.ReactSpa` (guia de SPA React + PKCE)
- `samples/OpenGate.Sample.BlazorWasmBff` (guia de BFF com Blazor WASM)
- `samples/OpenGate.Sample.PostgreSql` (sample pronto para validação com PostgreSQL)
- `samples/OpenGate.Sample.Sqlite` (sample pronto para validação com SQLite)
- [Guia de execução dos samples](samples.md)
