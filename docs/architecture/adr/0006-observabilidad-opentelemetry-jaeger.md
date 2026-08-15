# ADR 0006 — Observabilidad real con OpenTelemetry + Jaeger (no solo logging)

> Brevemente superado por [ADR 0031](0031-observabilidad-signoz-sustituye-jaeger.md) (SigNoz) y
> revertido de vuelta a esta decisión por [ADR 0032](0032-revertir-signoz-volver-a-jaeger.md) —
> vigente de nuevo.

## Contexto

La observabilidad no era un requisito explícito, pero detectar cuellos de botella en un
sistema distribuido con una Saga cruzando 3 servicios es imposible solo con logs sueltos por
servicio. Se decidió tratarla como pieza real del MVP, no como un extra.

## Decisión

- **Serilog** (logging estructurado) en los 3 servicios, con `TraceId`/`SpanId` de
  OpenTelemetry como campo de correlación en cada línea de log (`Serilog.Enrichers.Span`).
- **OpenTelemetry** instrumentando ASP.NET Core, `HttpClient` y EF Core, exportado vía OTLP a
  **Jaeger** (imagen `all-in-one`, un único contenedor en `docker-compose`).
- MassTransit ya emite sus propios spans de forma nativa (publish/consume) sin código
  adicional — capturados automáticamente por el mismo pipeline.

## Resultado demostrable

Abrir Jaeger (`http://localhost:16686`) tras una compra y ver **un único trace conectado** que
cruza HTTP → `PurchaseTicketCommand` → Saga → mensaje RabbitMQ → `AccessControl` → respuesta,
con la duración de cada paso. Es la evidencia visual más directa de trazabilidad end-to-end
en un sistema de microservicios.

## Fuera de alcance (roadmap)

Métricas agregadas con Prometheus/Grafana (dashboards de series temporales) — el siguiente
paso natural una vez hay trazas, pero no se construye aquí para no diluir el foco del MVP. En
Azure, este mismo código de instrumentación exportaría a Azure Monitor/Application Insights
cambiando solo el endpoint OTLP (ver `infra/deploy/azure/README.md`).

**Agregador de logs (tipo Seq)**: descartado deliberadamente para este MVP, con el mismo
criterio que Redis y Prometheus/Grafana — para 3 servicios, `docker compose logs -f` más las
trazas de Jaeger (que ya correlacionan la actividad entre servicios para una compra) cubren el
caso de uso real sin añadir una pieza de infraestructura más. Serilog escribe a consola con
`TraceId`/`SpanId` como campos, suficiente para cruzar un log puntual con su trace en Jaeger si
hiciera falta. Si el volumen de logs creciera más allá de lo que un `grep` razonable puede
cubrir, sería el siguiente paso natural (documentado, no construido).
