# OpenGate.Sample.ReactSpa

Sample de SPA React com Authorization Code + PKCE.

## Escopo

- Login via `/connect/authorize`
- Callback para `http://localhost:5173/callback`
- Uso de access token para consumir API protegida

## Próximos passos

1. Criar app React (`npm create vite@latest`).
2. Configurar cliente `interactive-demo` com redirect URI local.
3. Implementar fluxo PKCE conforme `docs/quickstarts/02-auth-code-pkce.md`.
