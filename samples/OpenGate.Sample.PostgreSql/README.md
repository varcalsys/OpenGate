# OpenGate.Sample.PostgreSql

Servidor OpenGate configurado para PostgreSQL.

## Subir PostgreSQL

```powershell
docker compose -f samples/OpenGate.Sample.PostgreSql/docker-compose.postgres.yml up -d
```

## Rodar sample

```powershell
dotnet run --project samples/OpenGate.Sample.PostgreSql
```

## Validar

- Login: `http://localhost:5148/Account/Login`
- Discovery: `http://localhost:5148/.well-known/openid-configuration`
- Health: `http://localhost:5148/health`

Credenciais seed:

- `demo@opengate.test`
- `Demo@1234!abcd`
