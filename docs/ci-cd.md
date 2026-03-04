## CI/CD e Qualidade

### Workflows

- CI: `.github/workflows/ci.yml`
  - restore, build, testes
  - coleta/merge de cobertura (`XPlat Code Coverage` + `reportgenerator`)
  - gate mínimo de cobertura: `80%`
  - upload para Codecov

- Segurança estática: `.github/workflows/codeql.yml`
  - análise CodeQL para C#
  - executa em push, PR e agendamento semanal

- Empacotamento: `.github/workflows/release-pack.yml`
  - dispara manualmente (`workflow_dispatch`) ou em tag `v*`
  - gera `nupkg` e publica como artifact

### Codecov

O workflow de CI envia `coverage/Cobertura.xml` para o Codecov.

### Como validar localmente

```powershell
dotnet restore OpenGate.slnx
dotnet build OpenGate.slnx -c Release
dotnet test OpenGate.slnx -c Release --collect:"XPlat Code Coverage"
```
