# OpenGate.Sample.BlazorWasmBff

Sample de arquitetura Blazor WASM + BFF (Backend for Frontend).

## Escopo

- BFF gerencia cookies e troca de tokens no servidor
- Blazor WASM chama apenas endpoints do BFF
- Logout centralizado em `/connect/logout`

## Próximos passos

1. Criar host ASP.NET Core com autenticação cookie.
2. Criar app Blazor WASM hospedado.
3. Expor endpoints BFF para proxy seguro de chamadas API.
