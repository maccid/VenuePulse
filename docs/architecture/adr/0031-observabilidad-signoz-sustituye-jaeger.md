# ADR 0031 — Observabilidad unificada con SigNoz (trazas + logs en una sola UI)

**Supera a:** [ADR 0006](0006-observabilidad-opentelemetry-jaeger.md)

## Contexto

El ADR 0006 fijó trazas distribuidas con OpenTelemetry → Jaeger y descartó un agregador de logs
("para 3 servicios, `docker compose logs -f` más las trazas de Jaeger cubren el caso de uso").
La experiencia real de demo mostró el límite de esa decisión: para responder "¿qué pasó y por qué
fue lento?" hay que **saltar manualmente** entre la consola (`grep` de logs con `traceId`) y la UI
de Jaeger (búsqueda del trace por ese mismo `traceId`). La correlación existe, pero es un paso
humano. Se buscaba ver trazas **y** logs en una sola pantalla, como un APM (estilo New Relic), sin
prometer métricas agregadas ni piezas que la demo no necesita.

## Decisión

1. **Sustituir Jaeger por SigNoz** self-hosted en `docker-compose` (UI en `http://localhost:3301`),
   con su stack mínimo: `clickhouse` (almacenamiento), `zookeeper-1` (metadata de réplicas),
   `signoz` (UI + API, binario unificado), `signoz-otel-collector` (ingesta OTLP en 4317 gRPC /
   4318 HTTP) y dos jobs one-shot (`init-clickhouse` descarga el UDF `histogramQuantile`,
   `signoz-telemetrystore-migrator` crea/actualiza el esquema). Config en `infra/signoz/`.
2. **Los 3 servicios exportan ahora trazas Y logs por OTLP**: las trazas siguen saliendo del
   pipeline de OpenTelemetry (`AddVenuePulseTracing`) y los logs salen por el **sink de Serilog
   `Serilog.Sinks.OpenTelemetry`** (`WriteTo.OpenTelemetry(...)` en
   `services/Core/Observability/ObservabilityExtensions.cs`, mismo endpoint que las trazas).
   El sink mapea `Activity.Current` a `trace_id`/`span_id` en cada `LogRecord`, así que en SigNoz
   cada log queda **colgado de su trace automáticamente**, sin código adicional.
3. **Serilog se mantiene** escribiendo a consola (`docker compose logs`): el agregador no lo
   sustituye, lo complementa. Siguen siendo dos salidas del mismo `ILogger<T>`.
4. **Por qué zookeeper y no clickhouse-keeper**: el stack actual de SigNoz usa ClickHouse Keeper,
   que **segfallea bajo Docker Desktop en Windows** (documentado por SigNoz). El último compose
   oficial con `deploy/docker/` (v0.119.0) usa zookeeper y es estable en Windows; se fijan esas
   imágenes (`signoz/signoz:v0.119.0`, `signoz/signoz-otel-collector:v0.144.2`,
   `clickhouse/clickhouse-server:25.5.6`, `signoz/zookeeper:3.7.1`).
5. **k8s**: el endpoint OTLP de los manifiestos apunta a `signoz-otel-collector.observability:4317`
   (la misma stack desplegable en el clúster vía Helm — documentada, no provisionada, mismo
   criterio que el resto de `infra/deploy/k8s/`).

## Resultado demostrable

Comprar una entrada y abrir `http://localhost:3301`: un único trace conectado que cruza
HTTP → `PurchaseTicketCommand` → Saga → RabbitMQ → `AccessControl` → respuesta, con la duración de
cada span, y **los logs del flujo** (`LogInformation`/`LogWarning`/`LogError` de los consumers,
`LoggingBehavior`, `GlobalExceptionHandler`) accesibles desde el propio trace. Además, búsqueda de
logs por texto (p. ej. `"sin aforo"`) que salta directo al trace correspondiente — el paso que
antes requería cruzar consola ↔ Jaeger a mano.

## Fuera de alcance (roadmap)

- **Métricas de aplicación** (contadores/RED propios): SigNoz ya deriva métricas APM de los spans
  (`signozspanmetrics/delta` en el config del collector) sin que la app emita nada; emitir métricas
  de dominio por OTLP quedaría como siguiente paso natural si se necesitara.
- **Azure Application Insights**: sigue siendo la vía documentada en producción — mismo pipeline
  OTLP, se cambia el endpoint (ver `infra/deploy/azure/README.md`).

## Alternativas descartadas

- **Mantener Jaeger + `grep` en consola**: funcional, pero mantiene el salto manual
  consola ↔ Jaeger que este ADR elimina.
- **Seq**: agregador de logs excelente, pero **solo logs** — no unifica trazas, seguiría habiendo
  dos herramientas (Seq + Jaeger).
- **Grafana LGTM (Loki + Tempo + Grafana)**: unifica, pero son 3+ piezas nuevas — sobre-carga para
  una demo que SigNoz cubre con una sola UI.
- **SigNoz moderno con ClickHouse Keeper** (Foundry / instalación oficial actual): bloqueado por el
  segfault de Keeper bajo Docker Desktop en Windows — la máquina de desarrollo de la demo es
  Windows.
- **Azure Application Insights como backend de la demo local**: requiere cuenta de Azure, y la demo
  debe correr con `docker compose up` sin ninguna cuenta.

## Consecuencias / riesgo aceptado

- SigNoz añade ~6 contenedores y **ClickHouse pide 2–4 GB de RAM** — es el coste real de un APM
  unificado y se acepta para la demo. Primer arranque ~1 minuto (inicialización de ClickHouse +
  descarga del UDF + migraciones del esquema).
- **Dependencia soft de las APIs hacia SigNoz**: no hay `depends_on` de `signoz-otel-collector` en
  los backends — los exporters OTLP encolan y reintentan, así que si el collector aún no está listo
  la telemetría llega cuando lo esté, sin retrasar el arranque de los servicios.
- Si SigNoz/ClickHouse se cae, los 3 servicios siguen funcionando (la telemetría se bufferiza en el
  proceso y se reenvía al recuperar).
- Se elimina el contenedor `jaeger` (imagen `jaegertracing/all-in-one:1.60`) del compose — no queda
  coexistencia; SigNoz es el único punto de telemetría de la demo.
