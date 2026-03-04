# OpenGate.Sample.Sqlite

Servidor OpenGate configurado para SQLite.

## Rodar sample

```powershell
dotnet run --project samples/OpenGate.Sample.Sqlite
```

Banco local gerado automaticamente:

- `samples/OpenGate.Sample.Sqlite/opengate.sample.sqlite.db`

## Validar

- Login: `http://localhost:5148/Account/Login`
- Discovery: `http://localhost:5148/.well-known/openid-configuration`
- Health: `http://localhost:5148/health`

Credenciais seed:

- `demo@opengate.test`
- `Demo@1234!abcd`
