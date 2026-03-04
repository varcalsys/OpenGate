# OpenGate.Sample.ProtectedApi

API protegida por JWT Bearer para validar access tokens emitidos pelo OpenGate.

## Como usar

1. Rode `samples/OpenGate.Sample.Basic` (issuer `https://localhost:7001`).
2. Rode este sample: `dotnet run --project samples/OpenGate.Sample.ProtectedApi`.
3. Obtenha um token (`machine-demo`) e chame `GET /api/me` com `Authorization: Bearer <token>`.
