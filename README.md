# VenuePulse

Demo técnica de digitalización de estadios (smart venues): experiencia del fan (ticketing) y
operaciones del recinto (control de accesos y aforo en tiempo real).

No tiene relación con ninguna marca real: los nombres del dominio (`VenuePulse`, `Ticketing`,
`AccessControl`, `CheckIn`) son inventados para esta demo.

## Qué demuestra

- **DDD + microservicios**: 3 bounded contexts, cada uno con Clean Architecture propia
  (Domain/Application/Infrastructure/Api) y su propia base de datos.
- **CQRS solo donde aporta** (ver [ADR 0003](docs/architecture/adr/0003-cqrs-solo-en-checkin.md)):
  `CheckIn` lo usa porque tiene asimetría real lectura/escritura; `Ticketing` y `AccessControl`
  son Application Services simples a propósito.
- **Event-driven con Saga real**: `TicketPurchaseSaga` (MassTransit) orquesta
  reservar aforo → "pago" mockeado → confirmar/**compensar**, con una compensación real (no
  simulada) cuando el pago falla tras la reserva — ver
  [ADR 0001](docs/architecture/adr/0001-saga-masstransit-rabbitmq.md).
- **Outbox transaccional** nativo de MassTransit sobre EF Core
  ([ADR 0002](docs/architecture/adr/0002-outbox-transaccional.md)).
- **Persistencia poliglota con motivo**: Postgres (agregados de negocio), MongoDB (estado de
  la Saga — [ADR 0005](docs/architecture/adr/0005-persistencia-poliglota-saga-mongo.md)),
  RabbitMQ (mensajería).
- **React con micro frontends real** (`shell` + `ticket-checkout` como Web Component) +
  panel de operaciones — ver [ADR 0004](docs/architecture/adr/0004-microfrontends-nginx-webcomponents.md).
- **Trazas distribuidas** con OpenTelemetry + Jaeger, no solo logs —
  [ADR 0006](docs/architecture/adr/0006-observabilidad-opentelemetry-jaeger.md).
- **CI/CD y gobernanza real** en GitHub Actions, con manifiestos K8s/Jenkinsfile/Azure
  documentados honestamente como capacidad, no como algo desplegado (ver tabla más abajo).
- **IA como acelerador de desarrollo**, con evidencia concreta incluyendo una sesión de
  depuración real — ver [`docs/AI-USAGE.md`](docs/AI-USAGE.md).

## Estructura del repositorio

```
services/   Backend .NET — 3 microservicios + librería compartida Core (VenuePulse.Core)
            Incluye Architecture.Tests/ (ArchUnitNET): tooling transversal que valida las reglas
            de capas sobre los ensamblados reales, no es un servicio más
ui/         3 apps React independientes (shell, ticket-checkout, venue-ops)
infra/      docker-compose, manifiestos K8s, Jenkinsfile, guía de Azure, Nginx de composición
docs/       ADRs, AI-USAGE.md, demo-script.md
```

Separación deliberada: `infra/` no depende de `services/` ni `ui/` a nivel de código, solo los
referencia como contexto de build — así queda claro qué es aplicación y qué es despliegue.

## Arrancar la demo

```bash
docker compose -f infra/docker-compose.yml up --build
```

- Rebuild selectivo por capas: `docker compose up --build` solo reconstruye las imágenes cuyo
  código cambió, el resto reutiliza la cache.
- `http://localhost:8080` — app pública (catálogo + aforo en vivo + comprar entrada)
- `http://localhost:8080/venue-ops/` — panel de operaciones (escanear entradas, aforo por zona)
- `http://localhost:16686` — Jaeger (trazas distribuidas)

Guion completo de demo, incluyendo el camino de compensación y una tarea en vivo con Claude
Code: [`docs/demo-script.md`](docs/demo-script.md).

## Qué es real y qué es documentado

No prometer de más es parte del ejercicio. Tabla honesta:

| Capacidad | Estado |
|---|---|
| 3 microservicios con `docker-compose up`, migraciones reales, RabbitMQ, Saga con compensación real | **REAL**, corre local |
| Trazas distribuidas end-to-end (OpenTelemetry → Jaeger) | **REAL**, visible en Jaeger UI |
| GitHub Actions: build+test, lint, build/push de imágenes | **REAL**, corre en cada push |
| Validación sintáctica de manifiestos K8s en CI | **REAL** (solo validación) |
| Manifiestos K8s/Kustomize (`infra/deploy/k8s`) | Escritos y válidos, **no desplegados** a AKS real |
| `Jenkinsfile` (`infra/deploy/jenkins`) | Escrito, **no ejecutado** |
| Topología Azure (`infra/deploy/azure/README.md`) | **Documentada**, no aprovisionada |
| Test de integración de la Saga contra Testcontainers reales | Camino feliz **verificado**; camino de compensación con un problema de fiabilidad específico del arnés de test, documentado en [ADR 0009](docs/architecture/adr/0009-masstransit-mongo-debugging.md) — **verificado por separado contra `docker-compose` real**: ambos caminos funcionan correctamente, confirmando que no era un defecto de la Saga |

## Tests

```bash
dotnet test services/VenuePulse.slnx
```

Pirámide deliberada: tests de dominio (invariantes), tests de la Saga con el harness in-memory
oficial de MassTransit (camino feliz y compensación, sin infraestructura real), tests de
arquitectura con ArchUnitNET (fallan el build si el dominio depende de Infrastructure), y tests
de integración con Testcontainers (Postgres+RabbitMQ+MongoDB reales).

## Alcance: qué no se construyó (y por qué)

Fuera de alcance, documentado como roadmap con motivo: pasarela de pago real (el "pago" es un
paso mockeado dentro de la Saga, por diseño), IdentityServer/Azure AD B2C, videovigilancia y
analítica de datos avanzada (pilares del smart venue no cubiertos por decisión consciente de
alcance), métricas agregadas con Prometheus/Grafana, Module Federation, autoscaling validado
con carga real. El objetivo fue profundidad y capacidad de defensa sobre amplitud de checklist.

**Autenticación entre servicios (decisión de alcance):** los servicios no se llaman por HTTP
entre sí — se comunican por el bus de mensajes (RabbitMQ/MassTransit), cuya autenticación es la
del broker (credenciales + vhost aislado + TLS), no OAuth client-credentials entre procesos.
El único HTTP es usuario → gateway → backend, y ahí no hay identidad de usuario (JWT/OIDC en el
gateway quedaría fuera de alcance, como IdentityServer/Azure AD B2C arriba). Si algún día se
desplegara público: secretos del broker, TLS en nginx e identidad de usuario en el gateway.
