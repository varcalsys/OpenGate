## OpenGate.Migration

`OpenGate.Migration` é a CLI de migração do OpenGate.

### O que faz

- importa configuração para um banco OpenGate
- suporta as fontes:
  - `opengate-json`
  - `duende`
  - `is4`
- suporta os providers:
  - `sqlite`
  - `sqlserver`
  - `postgresql`

### Instalar como tool local

```powershell
dotnet tool install --tool-path .\artifacts\tools OpenGate.Migration --add-source .\artifacts\packages --version 0.1.0-alpha
```

Se a tool estiver no `PATH`, o comando exposto será:

```powershell
dotnet opengate --help
```

### Comando

```powershell
.\artifacts\tools\dotnet-opengate --help
```

### Exemplo

```powershell
.\artifacts\tools\dotnet-opengate migrate --source duende --provider sqlite --connection-string "Data Source=opengate.db" --input .\duende.json --dry-run --output-plan .\artifacts\migration-plan.json
```

### Documentação completa

Veja `docs/migration-cli.md` no repositório.