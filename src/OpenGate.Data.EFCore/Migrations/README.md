## EF Core Migrations por Provider

Organização:

- `sqlserver`: migrations no próprio projeto `OpenGate.Data.EFCore`
- `postgresql`: projeto dedicado `OpenGate.Data.EFCore.Migrations.PostgreSql`
- `sqlite`: projeto dedicado `OpenGate.Data.EFCore.Migrations.Sqlite`

### Gerar migration

```powershell
./scripts/ef-add-migration.ps1 -Name AddSomething -Provider sqlserver
./scripts/ef-add-migration.ps1 -Name AddSomething -Provider postgresql
./scripts/ef-add-migration.ps1 -Name AddSomething -Provider sqlite
```

### Aplicar migrations

```powershell
./scripts/ef-update-db.ps1 -Provider sqlserver
./scripts/ef-update-db.ps1 -Provider postgresql
./scripts/ef-update-db.ps1 -Provider sqlite
```

### Conexão design-time

Você pode sobrescrever a connection string de design-time com `OPENGATE_EF_CONNECTION` antes de rodar os scripts.
