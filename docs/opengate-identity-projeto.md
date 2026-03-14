# PLANEJAMENTO DE PROJETO

## OpenGate Identity Server

**Produto Turnkey sobre OpenIddict • Instalar e Usar**

*A solução de identity que o ecossistema .NET precisa*

Documento Confidencial — Março 2026 | Versão Final

---

## Sumário

1. [Resumo Executivo](#1-resumo-executivo)
2. [O Problema que Resolvemos](#2-o-problema-que-resolvemos)
3. [O Produto: OpenGate Identity](#3-o-produto-opengate-identity)
4. [Roadmap](#4-roadmap)
5. [Cronograma Visual](#5-cronograma-visual)
6. [Equipe](#6-equipe)
7. [Orçamento](#7-orçamento)
8. [Posicionamento Competitivo](#8-posicionamento-competitivo)
9. [Modelo de Monetização](#9-modelo-de-monetização)
10. [Riscos](#10-riscos)
11. [Próximos Passos](#11-próximos-passos)

---

## 1. Resumo Executivo

O OpenGate Identity Server é um produto turnkey de identity management para .NET que usa o OpenIddict como motor de protocolos via dependência NuGet. O conceito é simples: o OpenIddict já resolve a parte difícil (OAuth 2.1, OpenID Connect, PKCE, DPoP, mTLS). O que falta no ecossistema é tudo ao redor: Admin UI, templates prontos, migration tools, observabilidade, documentação passo-a-passo e uma experiência de "instalar e usar em 5 minutos".

> **Analogia:** OpenIddict é o React. OpenGate é o Next.js. O OpenIddict é o ASP.NET Core. OpenGate é o ABP Framework. Nós não reinventamos o motor — nós construímos o carro completo.

Não há fork, não há cópia de código, não há obrigação de contribuir ao upstream. O OpenIddict é uma dependência do projeto, assim como o Entity Framework Core é dependência de qualquer app .NET que usa banco de dados. Simples assim.

| Item | Detalhe |
|------|---------|
| **Projeto** | OpenGate Identity Server |
| **O que é** | Produto turnkey para identity management, construído SOBRE o OpenIddict |
| **Relação c/ OpenIddict** | Dependência via NuGet. Sem fork, sem PRs, sem obrigações. |
| **Licença** | Apache 2.0 (código nosso) + OpenIddict Apache 2.0 (dependência) |
| **Plataforma** | .NET 8+ / ASP.NET Core 8+ |
| **Timeline** | 10–14 meses até v1.0 |
| **Equipe** | 4–5 desenvolvedores + 1 security |
| **Orçamento** | R$ 1M–1.6M (primeiros 14 meses) |

---

## 2. O Problema que Resolvemos

### 2.1 O Desenvolvedor .NET Hoje

Quando um desenvolvedor .NET precisa implementar identity management em 2026, ele enfrenta estas opções:

| Opção | Vantagem | Problema |
|-------|----------|----------|
| **Duende IS** | Completo, documentado, certificado OpenID | Pago (US$1.5K–35K/ano); licença restritiva |
| **Keycloak** | Gratuito, Admin UI completa, pronto pra usar | Java (não é .NET); pesado; difícil de customizar |
| **OpenIddict** | Gratuito, .NET nativo, excelente motor de protocolos | Sem Admin UI, sem templates prontos, bare metal demais |
| **Auth0/Okta** | SaaS fácil de usar | Vendor lock-in; caro em escala; sem self-hosting |
| **ASP.NET Identity** | Built-in, simples para apps básicas | Não é um identity server; sem OAuth/OIDC; sem SSO |

### 2.2 A Lacuna

Ninguém oferece simultaneamente: gratuito + .NET nativo + pronto pra usar + Admin UI + documentação exemplar. O OpenGate preenche exatamente essa lacuna, combinando o motor do OpenIddict com tudo que falta para ser um produto completo.

> O OpenIddict é uma biblioteca fantástica, mas o próprio README diz: *"it is not a turnkey solution but a framework that requires writing custom code to be operational"*. O OpenGate é a solução turnkey que o ecossistema .NET precisa.

---

## 3. O Produto: OpenGate Identity

### 3.1 Experiência do Desenvolvedor

O setup completo do OpenGate será feito em 5 minutos:

**Passo 1: Criar projeto (30 segundos)**

```bash
dotnet new opengate-server -n MinhaEmpresa.Identity
```

**Passo 2: Configurar banco (30 segundos)**

Editar `appsettings.json` com a connection string. PostgreSQL, SQL Server ou SQLite.

**Passo 3: Rodar (10 segundos)**

```bash
dotnet run
```

**Resultado imediato:**

- Identity server rodando em `https://localhost:5001`
- Login page moderna e responsiva funcionando
- Admin dashboard em `https://localhost:5001/admin`
- Endpoints OAuth 2.1/OIDC configurados (`.well-known/openid-configuration`)
- PKCE obrigatório, refresh token rotation, key auto-rotation — tudo seguro por padrão
- Swagger da Admin API em `https://localhost:5001/swagger`

### 3.2 Componentes do Produto

| Componente | O que entrega |
|------------|---------------|
| **OpenGate.Server** | Pacote principal: configura o OpenIddict com defaults seguros, middleware de rate limiting, health checks, OpenTelemetry, CORS. Uma linha: `builder.AddOpenGate()` |
| **OpenGate.UI** | Templates de Login, Consent, Logout, Registro, Erro. Razor Pages com design moderno, dark mode, i18n (pt-BR, en, es). Customizável via CSS e Razor override. |
| **OpenGate.Admin** | Dashboard Blazor WASM: gerenciar clientes, escopos, usuários, tokens, sessões. Gráficos de métricas em tempo real. RBAC (Admin, Viewer). |
| **OpenGate.Admin.Api** | REST API completa para automação: CRUD de tudo, import/export JSON, webhooks, bulk operations. Documentada com Swagger. |
| **OpenGate.Data.EFCore** | Stores estendidos para EF Core: user profiles, audit log, sessions, login history. Migrations automáticas. PostgreSQL, SQL Server, SQLite. |
| **OpenGate.Data.MongoDB** | Mesmos stores estendidos para MongoDB. |
| **OpenGate.Migration** | dotnet tool CLI: importar clientes, escopos e configuração do Duende IS e IdentityServer4. `dotnet opengate migrate` |
| **OpenGate.Templates** | 4 templates dotnet new: server (completo), api-only, spa-bff (React/Angular/Blazor), docker-compose. |
| **OpenGate.Passkeys** | WebAuthn / Passkeys como método de autenticação nativo no fluxo de login. |
| **OpenGate.Saml** | SAML 2.0 bridge: atuar como IdP ou SP para integração com legados (AD FS, SAP, Salesforce). |

### 3.3 O Que Fica com o OpenIddict (Dependência)

Tudo que é protocolo vem pronto do OpenIddict via NuGet. Nós não tocamos nisso:

| Protocolo / Feature | Status no OpenIddict 7.x |
|----------------------|--------------------------|
| OAuth 2.0 / 2.1 completo | ✅ Authorization Code, Client Credentials, Device Auth, Refresh Tokens |
| OpenID Connect 1.0 | ✅ Discovery, UserInfo, End-Session, Registration |
| PKCE, DPoP, PAR, mTLS | ✅ Todos suportados nativamente |
| Token Exchange (RFC 8693) | ✅ Adicionado na v7.0 |
| Token Introspection / Revocation | ✅ Completo |
| Client Assertions (private_key_jwt) | ✅ Client, Server e Validation |
| EF Core + MongoDB stores | ✅ Nativo com stores customizáveis |
| Native AOT / Trimming | ✅ Compatível desde v7.0 |
| Event pipeline extensível | ✅ Handlers customizáveis para qualquer endpoint |

---

## 4. Roadmap

### 4.1 Fase 1 — MVP (Meses 1–3)

**Objetivo:** `dotnet new opengate-server` funcional, com login UI e documentação inicial.

- Criar OpenGate.Server: configuração opinativa do OpenIddict com 1 linha (`builder.AddOpenGate()`)
- Presets de segurança: Development, Production, HighSecurity — ativados por environment
- UI de Login / Consent / Logout / Registro: Razor Pages modernas, responsivas, com dark mode
- Integração com ASP.NET Core Identity para user management
- EF Core stores estendidos (users, audit log, sessions) com migrations para PostgreSQL, SQL Server, SQLite
- Template `dotnet new opengate-server` com configuração guiada
- Docker image oficial (Alpine, <100MB) + docker-compose (app + PostgreSQL + Redis)
- Documentação: 3 quickstarts, API reference, architecture overview
- 5 samples: SPA (React), API protegida, Blazor WASM BFF, Console M2M, Device Flow
- CI/CD (GitHub Actions), Codecov > 80%, CodeQL

**Entrega:** v0.1 Alpha — NuGet pré-release + Docker image

### 4.2 Fase 2 — Admin & Tools (Meses 4–7)

**Objetivo:** Admin UI, Migration CLI e ferramentas que transformam o projeto em produto real.

#### Resultado esperado (definição de Beta v0.5)

Ao final da Fase 2, um dev .NET deve conseguir:

- Subir o OpenGate via `docker-compose` (PostgreSQL + Redis + Prometheus + Grafana) e acessar o painel `/admin`
- Gerenciar **clientes, escopos, usuários, permissões** e **sessões/tokens (consulta/revogação)** com RBAC aplicado por padrão
- Exportar/importar configuração (JSON) e executar migrações via CLI
- Operar o serviço com **observabilidade** (traces/metrics/logs) e **proteções padrão** (rate limiting, health checks)

#### Workstreams / Epics

1) **Admin REST API (OpenGate.Admin.Api)**
   - CRUD de clientes, escopos, usuários, roles/permissões + sessões/tokens (consulta e revogação)
   - Paginação, ordenação e filtros consistentes (padrão único)
   - Import/export JSON + operações em lote (bulk) onde fizer sentido
   - Swagger/OpenAPI completo + exemplos (requests/responses)

2) **Admin Dashboard (OpenGate.Admin — Blazor WASM)**
   - Listagens com search/filter, formulários de create/edit, validação e estados de loading/erro
   - Viewer do audit log (filtros por período, ator, entidade, ação)
   - Dashboard com métricas operacionais (tokens emitidos, falhas de login, latência p95, etc.)

3) **Segurança e RBAC na Admin**
   - Perfis: Super Admin, Admin, Viewer (com permissões explícitas)
   - Seed inicial seguro (primeiro admin) + hardening (cookies/CSRF/CORS conforme arquitetura)
   - Trilhas mínimas de auditoria para ações administrativas sensíveis

4) **Migration CLI (OpenGate.Migration)**
   - `dotnet opengate migrate --source duende|is4 --connection-string ...`
   - Modo `--dry-run` + relatório (o que será criado/atualizado) + logs estruturados
   - Mapeamentos claros (clientes, secrets, redirect URIs, scopes) e limitações documentadas

5) **Autenticação do usuário final: Social + MFA**
   - Templates pré-configurados: Google, Microsoft, GitHub, Apple, Facebook
   - MFA com TOTP (Google Authenticator) + recovery codes + UX consistente

6) **Operação / Observabilidade / Proteções**
   - OpenTelemetry: traces, metrics, logs (defaults opinativos) + exporters comuns
   - Endpoint Prometheus + health checks prontos para Kubernetes
   - Rate limiting por `client_id`, IP e (quando aplicável) usuário

7) **Documentação e DX (Beta-ready)**
   - +15 tutoriais (Admin, RBAC, social login, MFA, observabilidade)
   - Guia de migração do Duende IS completo (incluindo “gotchas” e checklist)
   - Docker Compose “bater e rodar” com dashboards no Grafana

#### Fora de escopo (explícito) na Fase 2

- Multi-tenancy, SAML bridge, Passkeys/WebAuthn, SCIM, Helm Charts (Fase 3+)
- Conformance tests OpenID Foundation (Fase 4)

#### Critérios de aceite (gate do v0.5 Beta)

- **Admin API**
  - 100% das operações essenciais (clientes/escopos/usuários/roles + sessões/tokens para consulta/revogação) disponíveis via REST
  - Swagger atualizado e navegável; erros padronizados; paginação/filtros consistentes
- **Admin UI**
  - Fluxos completos de criar/editar/listar para clientes e escopos; gestão básica de usuários; consulta/revogação de sessões/tokens
  - RBAC aplicado (Viewer não altera; Admin altera; Super Admin gerencia permissões)
- **Migration CLI**
  - Importa ao menos 80% dos cenários comuns de Duende/IS4 (clientes + scopes + redirect URIs + secrets)
  - Suporta `--dry-run` e gera relatório legível
- **Operação**
  - Compose com Prometheus + Grafana e dashboards iniciais
  - OTel habilitado com configuração documentada; health checks prontos
- **Qualidade**
  - Testes de integração cobrindo endpoints críticos e autorização (RBAC)
  - CI verde (CodeQL + testes) e documentação mínima de release

#### Checklist (resumo do escopo)

- Admin REST API: CRUD de clientes, escopos, usuários, tokens. Import/export JSON. Swagger completo.
- Admin Dashboard (Blazor WASM): listagens com search/filter, forms, dashboard de métricas, audit log viewer
- RBAC na Admin: Super Admin, Admin, Viewer
- Migration CLI: `dotnet opengate migrate --source duende|is4 --connection-string ...`
- Social login templates pré-configurados: Google, Microsoft, GitHub, Apple, Facebook
- MFA com TOTP (Google Authenticator) integrado na UI
- OpenTelemetry: traces, metrics, logs. Prometheus endpoint. Health checks para Kubernetes
- Rate limiting inteligente por client_id, IP, usuário
- 15 tutoriais adicionais + guia de migração do Duende IS completo
- Docker Compose com tudo pré-configurado (app + PostgreSQL + Redis + Prometheus + Grafana)

#### Checklist de execução da Fase 2

**0. Preparação / kickoff**

- [ ] Confirmar que os entregáveis da Fase 1 necessários para Admin/API já estão estáveis
- [ ] Congelar o escopo da Fase 2 e registrar explicitamente o que ficou para a Fase 3+
- [ ] Definir owner por workstream (API, UI, CLI, Segurança, Docs, DevOps)
- [ ] Definir Definition of Done comum: código, testes, documentação, observabilidade e review
- [ ] Criar backlog por sprint com prioridade e dependências claras

**1. Admin REST API**

- [ ] Definir contratos da API (resources, rotas, paginação, filtros, erros padronizados)
- [ ] Implementar CRUD de clientes
- [ ] Implementar CRUD de escopos
- [ ] Implementar CRUD de usuários
- [ ] Implementar papéis/permissões administrativas
- [ ] Implementar consulta e revogação de sessões/tokens
- [ ] Implementar import/export JSON
- [ ] Adicionar Swagger/OpenAPI com exemplos e fluxos documentados
- [ ] Cobrir endpoints críticos com testes de integração

**2. Admin Dashboard (Blazor WASM)**

- [ ] Implementar autenticação/acesso ao painel `/admin`
- [ ] Criar listagens com busca, filtro, paginação e ordenação
- [ ] Criar formulários de criação/edição para clientes e escopos
- [ ] Criar fluxo básico de gestão de usuários
- [ ] Criar viewer de audit log com filtros úteis
- [ ] Criar dashboard com métricas operacionais principais
- [ ] Validar estados de loading, erro, empty state e feedback de sucesso

**3. Segurança e RBAC**

- [ ] Definir matriz de permissões para Super Admin, Admin e Viewer
- [ ] Implementar seed seguro do primeiro administrador
- [ ] Garantir proteção das rotas administrativas com authorization policies
- [ ] Auditar ações sensíveis (create/update/delete/revoke)
- [ ] Validar restrições por perfil com testes automatizados

**4. Migration CLI**

- [ ] Definir modelo de entrada/saída para migração de Duende e IdentityServer4
- [ ] Implementar parser/adapters por origem (`duende`, `is4`)
- [ ] Implementar `--dry-run` com relatório do que será criado/alterado
- [ ] Implementar execução real da migração com logs estruturados
- [ ] Documentar mapeamentos suportados e limitações conhecidas
- [ ] Validar a CLI com cenários reais ou fixtures representativas

**5. Social Login + MFA**

- [ ] Criar templates/configuração-base para Google, Microsoft, GitHub, Apple e Facebook
- [ ] Documentar secrets, callbacks e setup por provider
- [ ] Implementar enrolment de TOTP
- [ ] Implementar challenge de MFA no login
- [ ] Implementar recovery codes
- [ ] Implementar UX de ativação/desativação e mensagens de erro consistentes

**6. Operação / observabilidade / proteção**

- [ ] Habilitar OpenTelemetry para traces, metrics e logs
- [ ] Expor endpoint Prometheus
- [ ] Configurar health checks prontos para uso em Kubernetes
- [ ] Implementar políticas de rate limiting por `client_id`, IP e usuário
- [ ] Preparar dashboards iniciais no Grafana
- [ ] Montar `docker-compose` completo e funcional para ambiente demo/staging

**7. Documentação e DX**

- [ ] Produzir os 15 tutoriais planejados
- [ ] Escrever o guia completo de migração do Duende IS
- [ ] Documentar Admin API, RBAC, social login, MFA e observabilidade
- [ ] Atualizar samples e exemplos de configuração quando necessário
- [ ] Criar checklist de onboarding para novos usuários do Beta

**8. Gate de release do v0.5 Beta**

- [ ] Executar smoke test completo via `docker-compose`
- [ ] Validar fluxos principais: admin login, CRUD, import/export, migração, MFA e observabilidade
- [ ] Garantir CI verde (testes, CodeQL e demais checks obrigatórios)
- [ ] Revisar gaps conhecidos e registrar limitações do Beta
- [ ] Preparar release notes, changelog e instruções de upgrade/instalação
- [ ] Publicar artefatos: NuGet, imagens Docker e documentação do Beta

#### Plano de execução da Fase 2

**Sequência recomendada de implementação**

1. **Fundação da Admin API + RBAC**
   - Definir contratos, autorização, seed do primeiro admin e baseline de testes
2. **Entidades core da administração**
   - Clients, scopes, usuários, permissões e sessões/tokens
3. **Admin UI v1**
   - Painel funcional consumindo a API com listagens, formulários e audit log viewer
4. **Import/export + Migration CLI**
   - Primeiro JSON; depois migração Duende/IS4 com `--dry-run`
5. **Social login + MFA**
   - Entram depois da base administrativa estável
6. **Observabilidade + hardening operacional**
   - OTel, Prometheus, Grafana, health checks e rate limiting
7. **Documentação, empacotamento e gate de release**
   - Tutoriais, guide de migração, compose final, release notes

**Dependências críticas**

- A Admin UI depende da estabilidade mínima da Admin API e do modelo de autorização
- A Migration CLI depende dos contratos finais de clientes/escopos/usuários
- MFA e social login dependem da estabilidade do fluxo de autenticação/base de usuários
- Observabilidade e dashboards devem ser ligados cedo, mas finalizados no hardening
- Documentação deve começar junto das features; não apenas no fim da fase

**Responsáveis sugeridos por frente**

| Frente | Owner principal | Apoio |
|-------|------------------|-------|
| Admin API + RBAC | Tech Lead / Senior Backend | Security Engineer |
| Admin UI (Blazor) | Senior Full-Stack | Tech Lead |
| Migration CLI | Senior Backend | Tech Lead |
| Social Login + MFA | Senior Full-Stack | Security Engineer |
| Observabilidade / Compose | DevOps / SRE | Tech Lead |
| Documentação / Tutoriais | Technical Writer | Toda a equipe |

**Cadência operacional sugerida**

- Planejamento quinzenal por sprint com objetivos e critérios de saída
- Daily curta com foco em bloqueios e dependências cruzadas
- Review ao fim de cada sprint com demo do que está realmente utilizável
- Retrospectiva com ajuste de escopo, capacidade e riscos
- Freeze leve nas 2 últimas semanas para hardening, docs e correções

**Marcos de execução por mês**

- **Mês 4:** Admin API base, RBAC inicial, Swagger e primeiros testes de integração
- **Mês 5:** CRUD core completo + Admin UI v1 + audit log viewer
- **Mês 6:** Import/export JSON + Migration CLI + social login base
- **Mês 7:** MFA, observabilidade completa, compose final, docs, hardening e Beta release

**Riscos operacionais a monitorar durante a execução**

- Crescimento descontrolado do escopo da Admin UI
- Acoplamento excessivo entre API, UI e CLI
- Subestimação da complexidade da migração Duende/IS4
- Segurança tratada tarde demais no ciclo
- Documentação acumulada para o final da fase

**Sinais de que a fase está no caminho certo**

- Ao final de cada sprint existe pelo menos um fluxo demonstrável ponta a ponta
- Testes de integração acompanham a evolução da API
- O painel Admin é utilizável cedo, mesmo com escopo reduzido
- Compose local e ambiente demo permanecem funcionando durante toda a fase
- O backlog restante do Beta fica menor e mais claro sprint após sprint

#### Cronograma sugerido (8 sprints / 4 meses)

| Sprint | Foco | Entregável principal |
|--------|------|----------------------|
| 1 | Fundamentos Admin API | Skeleton Admin API + autenticação + RBAC básico + Swagger |
| 2 | Entidades core | CRUD de Clients/Scopes com paginação/filtros + testes de integração |
| 3 | Admin UI v1 | Listagens + forms para Clients/Scopes + login no painel |
| 4 | Usuários + auditoria | CRUD básico de usuários + audit log (API + viewer UI) |
| 5 | Import/Export | Export/import JSON + bulk ops + documentação |
| 6 | Migration CLI | Migração Duende/IS4 com `--dry-run` + relatórios |
| 7 | Social + MFA | Templates social login + TOTP + recovery codes + tutoriais |
| 8 | Ops + Beta hardening | Compose completo (Prom/Grafana) + OTel + rate limiting + release notes |

**Entrega:** v0.5 Beta — NuGet público + Admin UI + Docker Compose

### 4.3 Fase 3 — Enterprise (Meses 8–11)

**Objetivo:** Features enterprise, auditoria de segurança e preparação para certificação.

- Multi-tenancy: isolamento por banco, schema ou coluna discriminadora
- SAML 2.0 bridge: atuar como IdP e SP para integração com legados
- Passkeys / WebAuthn nativo no fluxo de login
- Account Management UI: perfil, sessões ativas, dispositivos, histórico de login
- Caching distribuído com Redis para tokens, configuração e sessões
- Audit log completo e pesquisável (quem, o quê, quando, de onde)
- Auditoria de segurança independente + pen testing
- Helm Charts oficiais para Kubernetes (HPA, PDB, resource limits, liveness/readiness)
- Performance benchmarks públicos: OpenGate vs Duende IS vs Keycloak

**Entrega:** v0.9 RC — production-ready para early adopters

### 4.4 Fase 4 — GA (Meses 12–14)

**Objetivo:** Release 1.0 com certificação e ecossistema completo.

- Executar conformance tests do OpenID Foundation contra o OpenGate
- SCIM 2.0 provisioning
- Programa de bug bounty
- 30+ tutoriais cobrindo todos os cenários
- 20+ samples no repositório
- Security hardening guide
- Lançamento do OpenGate Cloud (SaaS gerenciado) em beta privado

**Entrega:** OpenGate Identity v1.0 GA

---

## 5. Cronograma Visual

| Workstream | M1–M3 | M4–M7 | M8–M11 | M12–M14 | Entrega |
|------------|-------|-------|--------|---------|---------|
| Core Setup + Presets | ████ | █ | | | v0.1 Alpha |
| Login / Consent UI | ███ | ██ | █ | | v0.5 Beta |
| Admin UI + API | | ████ | ██ | █ | v0.5 Beta |
| Migration CLI | | ███ | █ | | v0.5 Beta |
| SAML + Passkeys | | | ████ | ██ | v0.9 RC |
| Multi-tenancy | | | ███ | █ | v0.9 RC |
| Segurança / Audit | █ | ██ | ████ | ██ | Auditado |
| Documentação + Samples | ██ | ███ | ███ | ████ | 30+ tutoriais |
| Comunidade | █ | ██ | ███ | ████ | 5K+ stars |

---

## 6. Equipe

| Papel | Qtd | Foco |
|-------|-----|------|
| Tech Lead / Arquiteto | 1 | Arquitetura; integração com OpenIddict; API design; DX; code review |
| Senior Full-Stack (.NET + Blazor) | 2 | Admin UI/API; Login/Consent UI; Account Management; templates dotnet new; MFA |
| Senior Backend (.NET) | 1 | Migration CLI; SAML bridge; Passkeys; multi-tenancy; stores estendidos; caching |
| Security Engineer | 1 | Segurança dos defaults; threat modeling; auditoria; FAPI profile; pen testing |
| DevOps / SRE (part-time) | 1 | CI/CD; Docker; Helm; benchmarks; OpenTelemetry; monitoring setup |
| Technical Writer (part-time) | 1 | Documentação; tutoriais; samples; blog; migration guides |

**Total:** 5 full-time + 2 part-time

---

## 7. Orçamento

### Investimento por Fase

| Fase | Período | Investimento | Principais Custos |
|------|---------|--------------|-------------------|
| MVP | M1–M3 | R$ 220K–320K | Equipe core; infra CI/CD |
| Admin & Tools | M4–M7 | R$ 300K–440K | Equipe + infra cloud |
| Enterprise | M8–M11 | R$ 320K–500K | Equipe + auditoria + pen test |
| GA | M12–M14 | R$ 200K–340K | Equipe + marketing + conferências |
| **TOTAL** | **14 meses** | **R$ 1.04M–1.6M** | |

### Custos Mensais Detalhados

| Categoria | Mensal | Anual |
|-----------|--------|-------|
| Salários (5 FT + 2 PT) | R$ 60K–85K | R$ 720K–1.02M |
| Infraestrutura Cloud (CI/CD, staging, registry) | R$ 3K–5K | R$ 36K–60K |
| Auditoria de Segurança (1x) | — | R$ 80K–150K |
| Marketing e Conferências | R$ 5K–8K | R$ 60K–96K |
| Ferramentas (GitHub, Figma, etc.) | R$ 2K–3K | R$ 24K–36K |

---

## 8. Posicionamento Competitivo

| Critério | Duende IS | Keycloak | OpenIddict | OpenGate | Vencedor |
|----------|-----------|----------|------------|----------|----------|
| **Custo** | US$1.5K–35K | Gratuito | Gratuito | **Gratuito** | — |
| **Nativo .NET** | ✅ | ❌ Java | ✅ | **✅** | — |
| **Turnkey / Pronto** | ⚠️ Médio | ✅ Sim | ❌ Não | **✅ Sim** | **OpenGate** |
| **Admin UI** | ❌ Pago | ✅ Sim | ❌ Não | **✅ Blazor** | **OpenGate** |
| **Setup < 5 min** | ⚠️ | ✅ Docker | ❌ Muito código | **✅ dotnet new** | **OpenGate** |
| **Migration Tool** | N/A | ❌ | ❌ | **✅ CLI** | **OpenGate** |
| **SAML 2.0** | ✅ Plugin£ | ✅ | ❌ | **✅ Incluso** | **OpenGate** |
| **Passkeys** | ❌ | ⚠️ | ❌ | **✅** | **OpenGate** |
| **Observabilidade** | ⚠️ | ✅ | ❌ | **✅ OTel** | **OpenGate** |
| **Multi-tenancy** | ❌ | ✅ | ❌ | **✅** | Empate |

---

## 9. Modelo de Monetização

O produto é 100% gratuito e open source (Apache 2.0). A sustentabilidade vem de serviços ao redor:

| Fonte de Receita | Descrição | Estimativa Anual |
|------------------|-----------|------------------|
| **Suporte Enterprise** | SLA garantido, hotfixes prioritários, consultoria dedicada, security advisories antecipados | R$ 500K–1.2M |
| **OpenGate Cloud (SaaS)** | Versão gerenciada: painel avançado, backups, updates automáticos, SLA 99.9% | R$ 300K–1M |
| **Treinamento** | Cursos oficiais, workshops, programa de certificação OpenGate Professional | R$ 200K–500K |
| **Consulting** | Migração assistida de Duende IS, IdentityServer4, Keycloak | R$ 200K–600K |

---

## 10. Riscos

| Risco | Prob. | Imp. | Mitigação |
|-------|-------|------|-----------|
| **OpenIddict descontinuado** | Baixa | Alto | Apache 2.0 é irrevogável; podemos internalizar a última versão e manter independentemente. Enquanto isso, é ativamente mantido com release anual. |
| **Breaking changes no OpenIddict** | Média | Médio | Pinning de versão major; testes de integração contra pré-releases; compatibility layer se necessário. Atualizar no nosso ritmo. |
| **Vulnerabilidade no OpenIddict** | Baixa | Crítico | Monitorar releases e CVEs; atualizar dependência em até 48h; security advisory próprio para usuários OpenGate. |
| **Percepção de "apenas wrapper"** | Alta | Médio | Comunicar o valor enorme do produto (Admin UI, CLI, docs, DX). Ninguém chama o Next.js de "apenas wrapper do React". O valor é real e tangível. |
| **Baixa adoção** | Média | Alto | DX excepcional; migration CLI para capturar base instalada de Duende IS e IS4; marketing ativo; presença em conferências. |
| **Duende fica gratuito** | Baixa | Alto | Diferenciar com Admin UI, DX superior, multi-tenancy, Passkeys, SAML bridge. Muitos features que o Duende não oferece. |
| **Scope creep** | Alta | Médio | Roadmap rígido; RFC process; dizer "não" é parte do processo. Foco no que importa: DX e produção-ready. |

---

## 11. Próximos Passos

1. Registrar domínio e criar GitHub org (opengate-identity)
2. Recrutar Tech Lead (perfil: experiência com OpenIddict, DX-minded, boa comunicação)
3. Criar repositório com estrutura de solução, CI/CD e pacotes iniciais
4. POC em 2 semanas: `builder.AddOpenGate()` + Login UI funcional + Docker
5. Validar o conceito com 5–10 devs .NET como beta testers internos
6. Iniciar Sprint 1: OpenGate.Server + OpenGate.UI + primeiro template
7. Publicar anúncio no blog, Reddit r/dotnet, Twitter/X, Hacker News

---

*Documento Final — Março 2026 — Produto Turnkey sobre OpenIddict*
