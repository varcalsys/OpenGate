## Uso via API sem UI

Este guia esclarece o que o OpenGate já suporta hoje em cenários sem interface gráfica.

### Resposta curta

Sim, o OpenGate pode ser usado via API sem a UI para os fluxos de protocolo OAuth 2.0/OpenID Connect.

Exemplos atuais no repositório:

- discovery document em `/.well-known/openid-configuration`
- token endpoint em `/connect/token`
- outros endpoints padrão em `/connect/authorize`, `/connect/logout` e `/connect/userinfo`

Referências:

- [API Reference](api-reference.md)
- [Quickstart 3 - Client Credentials (curl)](quickstarts/03-client-credentials.md)

### O que funciona sem UI

Os endpoints de protocolo podem ser consumidos diretamente por qualquer cliente HTTP.

Casos de uso típicos:

- `client_credentials` para comunicação serviço-serviço
- descoberta automática do servidor OIDC
- consumo por Postman, `curl`, SDKs OAuth/OIDC e aplicações backend

Exemplo com `curl`:

```bash
curl -s -X POST http://localhost:5148/connect/token \
  -H "content-type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=machine-demo&client_secret=machine-demo-secret-change-in-prod&scope=api"
```

O retorno esperado contém `access_token`, `token_type` e `expires_in`.

### Admin API

O projeto também expõe uma Admin API REST em `/admin/api`, com endpoints para:

- `clients`
- `scopes`
- `users`
- `sessions`
- `audit-logs`
- import/export de configuração

Isso permite administrar o OpenGate por HTTP em vez de usar a Admin UI.

### Limitação atual da autenticação da Admin API

Embora a superfície administrativa seja REST, a autenticação padrão atual da Admin API ainda depende do login web/cookie do servidor.

Na prática:

- uma chamada anônima para `/admin/api/me` redireciona para `/Account/Login`
- o acesso administrativo "out of the box" ainda usa a UI de login para estabelecer a sessão

Isso significa que hoje há dois cenários distintos:

1. Uso dos endpoints OAuth/OIDC sem UI: suportado.
2. Automação administrativa 100% headless sem sessão web prévia: ainda não está pronta por padrão.

### Quando a UI ainda é necessária

A UI continua sendo a opção padrão para:

- login interativo de usuários
- consentimento
- estabelecimento da sessão administrativa usada pela Admin API no estado atual

Se o seu cenário exigir administração totalmente headless, o caminho é expor ou adicionar um mecanismo de autenticação não interativo para a Admin API.

Exemplos possíveis:

- bearer token para administradores
- client credentials com escopos administrativos
- autenticação mútua entre serviços
- API keys internas protegidas por gateway

### Resumo prático

Use sem UI quando quiser:

- emitir tokens
- integrar serviços
- consumir discovery e endpoints OIDC
- operar clientes OAuth via HTTP

Considere a UI ou uma extensão de autenticação quando quiser:

- administrar o ambiente sem sessão web
- fazer automação operacional da Admin API sem login humano

### Como habilitar Admin API headless no produto

Se o objetivo do produto for suportar administração totalmente sem UI, a direção mais consistente é adicionar autenticação por bearer token na Admin API, mantendo cookies para a experiência web existente.

Abordagem recomendada:

- manter a UI e o login por cookie para administradores humanos
- adicionar um esquema `Bearer` para chamadas de automação
- proteger `/admin/api` com políticas que aceitem claims ou scopes administrativos
- continuar exigindo roles administrativas como `Admin` e `SuperAdmin`

Desenho sugerido:

1. Registrar autenticação bearer além do cookie atual.
2. Validar tokens emitidos pelo próprio OpenGate para consumo interno da Admin API.
3. Definir scopes administrativos explícitos, por exemplo `admin_api`.
4. Exigir combinação de scope e role para operações críticas.
5. Permitir `client_credentials` apenas para clients marcados como automação administrativa.
6. Auditar todas as chamadas headless com `client_id`, subject, IP e operação.

Resultado esperado:

- humano usando `/admin` continua autenticando por login web
- automação usa `Authorization: Bearer <token>` contra `/admin/api`
- a superfície REST deixa de depender de redirecionamento para `/Account/Login`

Cuidados de segurança:

- não reutilizar indiscriminadamente qualquer access token da plataforma para a Admin API
- separar scopes administrativos dos scopes de negócio
- restringir emissão a clients confidenciais
- aplicar expiração curta e rotação de segredo
- registrar audit trail completo para create, update, delete, import e revoke

Impacto técnico provável no código atual:

- `OpenGate.Server` hoje define cookie como esquema padrão de autenticação
- a Admin API hoje usa `RequireAuthorization(...)`, então a evolução natural é adicionar policies compatíveis com bearer token
- o sample [samples/OpenGate.Sample.ProtectedApi](../samples/OpenGate.Sample.ProtectedApi/README.md) já mostra o padrão de consumo com `JwtBearer` em uma API protegida

Em outras palavras, a lacuna principal não é a ausência de endpoints REST, e sim a falta de um mecanismo nativo de autenticação não interativa para a superfície administrativa.
