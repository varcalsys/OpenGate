## Guia de Samples

### 1) OpenGate.Sample.Basic

- Caminho: `samples/OpenGate.Sample.Basic`
- O que mostra: servidor OpenGate completo (UI + seeding + endpoints OIDC)
- Rodar: `dotnet run --project samples/OpenGate.Sample.Basic`

### 2) OpenGate.Sample.ProtectedApi

- Caminho: `samples/OpenGate.Sample.ProtectedApi`
- O que mostra: API protegida por JWT Bearer
- Rodar: `dotnet run --project samples/OpenGate.Sample.ProtectedApi`

### 3) OpenGate.Sample.ConsoleM2M

- Caminho: `samples/OpenGate.Sample.ConsoleM2M`
- O que mostra: fluxo `client_credentials` consumindo API protegida
- Rodar: `dotnet run --project samples/OpenGate.Sample.ConsoleM2M`

### 4) OpenGate.Sample.DeviceFlow

- Caminho: `samples/OpenGate.Sample.DeviceFlow`
- O que mostra: fluxo `device authorization`
- Rodar: `dotnet run --project samples/OpenGate.Sample.DeviceFlow`

### 5) OpenGate.Sample.ReactSpa

- Caminho: `samples/OpenGate.Sample.ReactSpa`
- O que mostra: guia para SPA React com Authorization Code + PKCE

### 6) OpenGate.Sample.BlazorWasmBff

- Caminho: `samples/OpenGate.Sample.BlazorWasmBff`
- O que mostra: guia para arquitetura Blazor WASM + BFF

### 7) OpenGate.Sample.PostgreSql

- Caminho: `samples/OpenGate.Sample.PostgreSql`
- O que mostra: servidor OpenGate com provider PostgreSQL pronto para validar
- Subir banco: `docker compose -f samples/OpenGate.Sample.PostgreSql/docker-compose.postgres.yml up -d`
- Rodar: `dotnet run --project samples/OpenGate.Sample.PostgreSql`

### 8) OpenGate.Sample.Sqlite

- Caminho: `samples/OpenGate.Sample.Sqlite`
- O que mostra: servidor OpenGate com provider SQLite pronto para validar
- Rodar: `dotnet run --project samples/OpenGate.Sample.Sqlite`
