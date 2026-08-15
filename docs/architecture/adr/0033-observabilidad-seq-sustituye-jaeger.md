# ADR 0033 — Observabilidad con Seq: una sola UI para logs + trazas (sustituye a Jaeger)

**Supera a:** [ADR 0006](0006-observabilidad-opentelemetry-jaeger.md) (como backend de trazas)
**Resuelve el punto pendiente de:** [ADR 0032](0032-revertir-signoz-volver-a-jaeger.md) (búsqueda centralizada de logs)
**Relacionado (histórico):** [ADR 0031](0031-observabilidad-signoz-sustituye-jaeger.md) (SigNoz, revertido)

## Contexto

El ADR 0032 revirtió SigNoz y volvió a Jaeger + logs por consola, dejando un punto
explícitamente fuera de su alcance: "surgió la necesidad real de consultar logs de los 3
servicios en un solo sitio (para minería de logs), algo que ni Jaeger ni `docker compose logs -f`
cubren por sí solos... se trata como una decisión aparte, con su propio ADR".

Al evaluar ese hueco, surge un dato que cambia la comparación respecto a lo que pesaba en el
ADR 0031: **Seq ingesta trazas OpenTelemetry de forma nativa desde la versión 2024.1**, por el
mismo endpoint HTTP de su API (`/ingest/otlp/v1/traces`). Es decir, la correlación logs↔traces
en una sola UI ya no exige montar el stack completo de SigNoz (6 contenedores, ~1,25 GB, con la
dependencia frágil de Zookeeper documentada en el 0031): basta **un único contenedor** para
sustituir a la vez a Jaeger y a la búsqueda por consola.

## Opciones consideradas

1. **Mantener Jaeger + `grep` en consola** (estado del ADR 0032): funcional, pero mantiene el
   salto manual consola ↔ Jaeger que el ADR 0031 ya identificó como fricción, y no resuelve el
   hueco de minería de logs en un solo sitio que motivó este ADR.
2. **Volver a SigNoz**: ya descartado por peso y fragilidad (ADR 0031 → 0032). No compensa
   cuando Seq cubre el mismo caso de uso con un solo contenedor.
3. **Seq** (imagen `datalust/seq`): un único contenedor, ingesta nativa de trazas OTLP además
   de logs por HTTP (Serilog sink oficial), UI con consultas tipo SQL para logs y vista de
   trazas con spans y duraciones, correlacionando ambas por `TraceId`/`SpanId`.

## Decisión

1. **Sustituir Jaeger por Seq** en `infra/docker-compose.yml`:
   `datalust/seq:latest`, puerto `5341:80`, `ACCEPT_EULA=Y`, volumen `seq-data` persistente.
   Desaparece el contenedor `jaeger` (`jaegertracing/all-in-one`) y el puerto 4317 gRPC.
2. **Logs**: Serilog escribe a consola (como hasta ahora, con `TraceId`/`SpanId`) **y además** a
   Seq vía `Serilog.Sinks.Seq` 9.1.0, cuando la configuración `Observability:SeqServerUrl`
   esté presente (la definen `docker-compose.yml`, los `appsettings.json` locales y los
   manifiestos K8s). Sin esa clave, el comportamiento queda igual que el ADR 0032 (solo
   consola) — el sink nunca impide arrancar si Seq está caído.
3. **Trazas**: el exporter OTLP de `VenuePulse.Core` pasa a
   `OtlpExportProtocol.HttpProtobuf` (el recomendado por Seq en HTTP, sin TLS ni HTTP/2 en el
   camino Docker) apuntando a `http://<seq>/ingest/otlp/v1/traces`. La clave
   `Observability:OtlpEndpoint` cambia de formato (de base gRPC `:4317` a URL completa con
   path) en los 3 servicios — mismas claves de configuración, mismo código de instrumentación,
   solo cambia el valor, como ya prometía la sección "qué cambia y qué no" del guion de Azure.
4. **k8s**: los manifiestos base apuntan a `seq.observability:80` (mismo patrón externo que
   tenía `jaeger-collector.observability`). En Azure real seguiría aplicando la tabla del
   `infra/deploy/azure/README.md`: cambiar el endpoint OTLP a Azure Monitor/App Insights (o
   autoalojar Seq si se quisiera conservar la minería de logs con la misma UI).
5. **Sin API key** de Seq: demo local de un solo usuario, sin autenticación de ingesta
   (`X-Seq-ApiKey` quedaría documentada como opción en el ADR para un despliegue real).
6. **Documentación**: el ADR 0032 se conserva como histórico (su punto 3 anticipaba
   exactamente este ADR); 0006 queda marcado como superado (el backend de trazas pasa a ser
   Seq, no Jaeger); 0031 sigue siendo el registro de por qué se probó y se descartó SigNoz.

## Consecuencias / riesgo aceptado

- **Una sola UI** (`http://localhost:5341`) para logs de los 3 servicios + trazas
  correlacionadas por `TraceId` — se elimina el salto manual consola ↔ Jaeger y se cubre la
  minería de logs que ni Jaeger ni `docker compose logs -f` daban.
- **Coste de infraestructura mínimo**: un contenedor modesto (frente a los ~1,25 GB combinados
  de SigNoz) — coherente con el criterio anti sobre-ingeniería del proyecto.
- Se pierde la UI de Jaeger en `:16686`; la vista de trazas de Seq (spans, duraciones,
  waterfall por `TraceId`) la sustituye.
- Riesgo de proveedor: Seq pasa a ser pieza de infraestructura (como lo era Jaeger); si dejara
  de encajar, el exporter OTLP es estándar y solo cambiaría el endpoint de nuevo.
