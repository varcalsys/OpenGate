param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [ValidateSet("sqlserver", "postgresql", "sqlite")]
    [string]$Provider = "sqlserver"
)

$ErrorActionPreference = "Stop"

$config = switch ($Provider) {
    "postgresql" {
        @{
            Project = "src/OpenGate.Data.EFCore.Migrations.PostgreSql/OpenGate.Data.EFCore.Migrations.PostgreSql.csproj"
            Startup = "src/OpenGate.Data.EFCore.Migrations.PostgreSql/OpenGate.Data.EFCore.Migrations.PostgreSql.csproj"
            OutputDir = "Migrations"
        }
    }
    "sqlite" {
        @{
            Project = "src/OpenGate.Data.EFCore.Migrations.Sqlite/OpenGate.Data.EFCore.Migrations.Sqlite.csproj"
            Startup = "src/OpenGate.Data.EFCore.Migrations.Sqlite/OpenGate.Data.EFCore.Migrations.Sqlite.csproj"
            OutputDir = "Migrations"
        }
    }
    default {
        @{
            Project = "src/OpenGate.Data.EFCore/OpenGate.Data.EFCore.csproj"
            Startup = "src/OpenGate.Data.EFCore/OpenGate.Data.EFCore.csproj"
            OutputDir = "Migrations/sqlserver"
        }
    }
}

Write-Host "Adding migration '$Name' for provider '$Provider'..."
dotnet ef migrations add $Name `
  --project $config.Project `
  --startup-project $config.Startup `
  --output-dir $config.OutputDir

Write-Host "Done. Migration generated in $outputDir"
