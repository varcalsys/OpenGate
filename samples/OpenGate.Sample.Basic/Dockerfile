# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /repo

# Install Node.js (LTS) for Tailwind CSS compilation
RUN apk add --no-cache nodejs npm

# Restore NuGet — copy only csproj files first for better layer caching
COPY global.json Directory.Build.props ./
COPY src/OpenGate.Data.EFCore/OpenGate.Data.EFCore.csproj  src/OpenGate.Data.EFCore/
COPY src/OpenGate.Server/OpenGate.Server.csproj             src/OpenGate.Server/
COPY src/OpenGate.UI/OpenGate.UI.csproj                     src/OpenGate.UI/
COPY samples/OpenGate.Sample.Basic/OpenGate.Sample.Basic.csproj \
                                        samples/OpenGate.Sample.Basic/

RUN dotnet restore samples/OpenGate.Sample.Basic/OpenGate.Sample.Basic.csproj

# Install Tailwind npm dependencies (cached separately from dotnet restore)
COPY src/OpenGate.UI/package.json src/OpenGate.UI/package-lock.json* src/OpenGate.UI/
RUN cd src/OpenGate.UI && npm install

# Copy the rest of the sources
COPY src/   src/
COPY samples/ samples/

# Publish — MSBuild target automatically compiles Tailwind CSS (node_modules exists)
RUN dotnet publish samples/OpenGate.Sample.Basic/OpenGate.Sample.Basic.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser -D appuser
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OpenGate.Sample.Basic.dll"]
