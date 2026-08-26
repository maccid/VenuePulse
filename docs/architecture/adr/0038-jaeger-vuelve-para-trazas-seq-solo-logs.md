# ADR 0038 — Jaeger vuelve para trazas, Seq se queda solo con los logs

**Revierte parcialmente:** [ADR 0033](0033-observabilidad-seq-sustituye-jaeger.md) (solo el punto
3, trazas — el punto de logs centralizados en Seq se mantiene).
**Relacionado:** [ADR 0006](0006-observabilidad-opentelemetry-jaeger.md) (Jaeger original),
[ADR 0032](0032-revertir-signoz-volver-a-jaeger.md) (mismo patrón de vuelta a Jaeger, otro motivo).

## Contexto

El ADR 0033 sustituyó Jaeger por Seq para tener logs y trazas en una sola UI, con un único
contenedor en vez de dos. Funcionalmente correcto (Seq ingesta OTLP nativamente y correlaciona por
`TraceId`), pero al usarlo en real durante una sesión de depuración de la `PurchaseOrderSaga`
(ver `docs/AI-USAGE.md`) surgió una limitación de uso no evidente al decidir el ADR 0033: Seq
representa las trazas como una lista más de eventos dentro del mismo stream cronológico que los
logs — hay que expandir cada evento para ver el árbol de spans, y no hay una vista dedicada de
waterfall (barras apiladas por duración) como la de Jaeger. Para el caso de uso real de este
proyecto — explicar a alguien que no conoce el código cómo la Saga orquesta la compra cruzando
`CheckIn` → `AccessControl` vía RabbitMQ — la vista de Jaeger comunica el flujo pub/sub entre
microservicios de forma mucho más directa que la de Seq.

La minería de logs centralizada que motivó la mitad del ADR 0033 (buscar en los 3 servicios desde
un solo sitio) sigue siendo necesaria y Seq la resuelve bien — no hay motivo para revertir esa
parte.

## Decisión

Separar las dos responsabilidades en dos herramientas, cada una donde se ve mejor:

1. **Jaeger vuelve** para trazas: se reintroduce el contenedor `jaeger` (`jaegertracing/all-in-one:1.60`,
   `COLLECTOR_OTLP_ENABLED=true`, puertos `16686` UI y `4317` OTLP/gRPC) en
   `infra/docker-compose.yml`, igual que estaba antes del ADR 0033. `Observability__OtlpEndpoint`
   de los 3 servicios vuelve a `http://jaeger:4317` (`http://jaeger-collector.observability:4317`
   en k8s), y `ObservabilityExtensions.AddTracing` deja de forzar `OtlpExportProtocol.HttpProtobuf`
   (ese valor solo era necesario para el endpoint HTTP de Seq) — vuelve al protocolo por defecto
   del exporter (gRPC), correcto para Jaeger.
2. **Seq se queda, pero solo para logs**: `Observability__SeqServerUrl` no cambia — Serilog sigue
   escribiendo a Seq vía HTTP (`Serilog.Sinks.Seq`). Deja de recibir el export OTLP de trazas.
3. **Correlación entre ambos**: se mantiene, sin cambio de código — `Enrich.WithSpan()` ya pone
   `TraceId`/`SpanId` en cada línea de log, así que cruzar "vi algo raro en un log de Seq, quiero
   ver su traza completa" sigue siendo un simple copiar-pegar del `TraceId` en el buscador de
   Jaeger. Solo se pierde el salto de un único clic que daba tener ambas cosas en la misma UI.

## Alternativas descartadas

- **Mantener solo Seq** (statu quo del ADR 0033): descartada — el motivo de este ADR es
  explícitamente que la vista de Seq no comunica bien el flujo pub/sub de la Saga para el caso de
  uso real (explicarlo a un interlocutor que no conoce el código), no un problema de correlación
  técnica.
- **Volver también a "logs solo por consola"** (deshacer el ADR 0033 por completo, como hizo el
  ADR 0032 con SigNoz): descartada — la minería de logs centralizada de Seq sigue aportando valor
  real y no tiene nada que ver con el motivo de este cambio (la vista de trazas).
- **SigNoz de nuevo**: ya se probó y descartó por peso/fragilidad (ADR 0031 → 0032); no hay ningún
  dato nuevo aquí que cambie esa conclusión.

## Consecuencias / riesgo aceptado

- Vuelve a haber **dos contenedores** de observabilidad (Jaeger + Seq) en vez de uno — coste de
  infraestructura ligeramente mayor, aceptado porque cada uno cubre un caso de uso real distinto
  (explicar la Saga visualmente vs. buscar en logs).
- Se pierde el clic único "log → su traza" que daba Seq; se sustituye por copiar el `TraceId` de
  un lado a otro — mismo trade-off que ya aceptaba el ADR 0006 originalmente antes de que existiera
  Seq.
- k8s (`infra/deploy/k8s/base/api-*.yaml`) y la guía de Azure
  (`infra/deploy/azure/README.md`) se actualizan en la misma rama para no dejarlos inconsistentes
  con `docker-compose.yml`, aunque no se despliegan realmente (ver tabla de "qué es real" en
  `README.md`).
