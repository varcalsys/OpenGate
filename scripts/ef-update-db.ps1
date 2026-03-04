param(
    [ValidateSet("sqlserver", "postgresql", "sqlite")]
    [string]$Provider = "sqlserver",

    [string]$Migration = ""
)

$ErrorActionPreference = "Stop"

$config = switch ($Provider) {
    "postgresql" {
        @{
            Project = "src/OpenGate.Data.EFCore.Migrations.PostgreSql/OpenGate.Data.EFCore.Migrations.PostgreSql.csproj"
            Startup = "src/OpenGate.Data.EFCore.Migrations.PostgreSql/OpenGate.Data.EFCore.Migrations.PostgreSql.csproj"
        }
    }
    "sqlite" {
        @{
            Project = "src/OpenGate.Data.EFCore.Migrations.Sqlite/OpenGate.Data.EFCore.Migrations.Sqlite.csproj"
            Startup = "src/OpenGate.Data.EFCore.Migrations.Sqlite/OpenGate.Data.EFCore.Migrations.Sqlite.csproj"
        }
    }
    default {
        @{
            Project = "src/OpenGate.Data.EFCore/OpenGate.Data.EFCore.csproj"
            Startup = "src/OpenGate.Data.EFCore/OpenGate.Data.EFCore.csproj"
        }
    }
}

Write-Host "Applying migrations for provider '$Provider'..."

if ([string]::IsNullOrWhiteSpace($Migration)) {
  dotnet ef database update --project $config.Project --startup-project $config.Startup
} else {
  dotnet ef database update $Migration --project $config.Project --startup-project $config.Startup
}

Write-Host "Done."
