# VenuePulse — Contexto para agente IA

## Referencias

Lee estos archivos antes de tocar cualquier código:

| Archivo | Contenido |
|---------|-----------|
| **`.ia/PRODUCT-VISION.md`** | **Qué es VenuePulse, para qué existe y glosario común — LEER PRIMERO** |
| `.ia/ARCHITECTURE.md` | Stack, estructura de carpetas, flujo de eventos, decisiones clave |
| `.ia/RULES.md` | Convenciones de código, anti-patrones, Definition of Done |
| `.ia/GIT-FLOW.md` | Metodología de ramas, commits, worktrees |
| `.ia/KNOWN-ISSUES.md` | Limitaciones conocidas y su causa raíz |
| `.ia/TODO.md` | Tareas pendientes (espejo del task-tracker de la sesión) |
| `docs/architecture/adr/` | Decisiones de arquitectura ya tomadas, con su motivo |

> `.ia/`, `CLAUDE.md` y este fichero son contexto de trabajo local para cualquier agente IA
> (Claude Code u otro) — no forman parte de lo que se muestra o entrega. `docs/` es la
> documentación humana visible en la entrevista (ADRs, `AI-USAGE.md`, `demo-script.md`).

## Modos de trabajo

Indica el modo al inicio de la tarea si no es obvio:

| Modo | Cuándo usarlo |
|------|---------------|
| `[DEV]` | Implementar features, corregir bugs. Escribe código y tests. |
| `[TEST]` | Escribir o mejorar tests. No toca código de producción. |
| `[REVIEW]` | Auditar código. Solo lectura, señala problemas. |
| `[ARCH]` | Decisiones de arquitectura. Evalúa sin implementar, redacta ADR si aplica. |

## Comandos esenciales

```bash
dotnet build services/VenuePulse.slnx
dotnet test services/VenuePulse.slnx
docker compose -f infra/docker-compose.yml up --build
```

Un servicio o app concreta:

```bash
dotnet build services/Ticketing/Api/VenuePulse.Ticketing.Api.csproj
npm run build --prefix ui/shell
```

## Flujo git

Ver `.ia/GIT-FLOW.md` para la metodología completa (GitFlow: `main`/`develop`/`feature`/`fix`,
merges `--no-ff`).

**Regla:** cada cambio no trivial va en su propia rama corta con nombre descriptivo
(`feature/<algo>` o `fix/<algo>`), nunca directo sobre `main`. Para trabajar en varias tareas a
la vez sin bloquear la copia principal, usa `git worktree`:

```bash
git checkout develop
git checkout -b fix/mi-cambio
git worktree add ../venuepulse-fix-mi-cambio fix/mi-cambio
cd ../venuepulse-fix-mi-cambio
# trabajar aquí sin tocar la copia principal
git checkout develop   # (en la copia principal)
git merge --no-ff fix/mi-cambio -m "merge: fix/mi-cambio -> develop"
git worktree remove ../venuepulse-fix-mi-cambio
git branch -d fix/mi-cambio
```

- No hacer commit a menos que se pida explícitamente.
- Varias tareas lógicas = varios commits, no todo en uno.
- Mensajes de commit en inglés.

## No hacer

- No usar Blazor — todo el frontend es React (ver decisión de alcance en el README).
- No añadir CQRS/MediatR fuera de `CheckIn` — es ceremonia sin beneficio en CRUD simple (ADR 0003).
- No añadir Redis, Prometheus/Grafana ni Seq sin una necesidad real y documentada — ver ADR 0006
  y el README ("qué NO se construyó"): la sobre-ingeniería es exactamente lo que se evita aquí.
- No usar pasarela de pago real — siempre mockeado.
- No hacer `COPY . .` en ningún Dockerfile.
