# ADR 0032 — Revertir SigNoz, volver a Jaeger + logs por consola

**Revierte a:** [ADR 0006](0006-observabilidad-opentelemetry-jaeger.md)
**Revierte:** [ADR 0031](0031-observabilidad-signoz-sustituye-jaeger.md)

> **Superado en el punto 3 por [ADR 0033](0033-observabilidad-seq-sustituye-jaeger.md):** la
> "búsqueda centralizada de logs" que este documento deja fuera de su alcance se resuelve con
> Seq, que además sustituye a Jaeger como backend de trazas (una sola UI para ambas cosas).

## Contexto

El ADR 0031 sustituyó Jaeger por SigNoz para unificar trazas y logs en una sola UI, aceptando
explícitamente el coste: "SigNoz añade ~6 contenedores y ClickHouse pide 2-4 GB de RAM". Al
probarlo en real sobre el stack completo (`docker compose up --build`, 16 contenedores en total),
ese coste no compensa frente al beneficio para esta demo:

- **Peso real de la pila**: 6 contenedores nuevos (`clickhouse`, `zookeeper-1`, `signoz`,
  `signoz-otel-collector`, `init-clickhouse`, `signoz-telemetrystore-migrator`) sobre un total de
  16 — más de un tercio del stack dedicado a observabilidad, para un proyecto de 3 servicios.
  Medido en real tras el arranque: `clickhouse` ~424 MiB, `zookeeper` ~757 MiB (más que
  ClickHouse), `signoz` ~42 MiB, `collector` ~26 MiB — bastante por debajo de los 2-4 GB citados
  para el nodo de ClickHouse en solitario (esa cifra es la recomendación oficial de ClickHouse para
  un nodo con carga real de producción, no lo que consume esta demo con un volumen mínimo de
  telemetría), pero sigue siendo ~1,25 GB combinados solo para observabilidad, con un primer
  arranque de ~1 minuto por la inicialización de ClickHouse + migraciones de esquema.
- **Riesgo de plataforma ya documentado en el propio ADR 0031**: el stack de SigNoz usa
  `zookeeper` en vez de ClickHouse Keeper porque Keeper "segfallea bajo Docker Desktop en
  Windows" — una dependencia de compatibilidad frágil para un componente que solo existe para
  coordinar metadata de réplicas que esta demo no necesita (un único nodo de ClickHouse).
- **El caso de uso de la demo no lo pedía con la urgencia que se pensó**: correlacionar trazas y
  logs saltando a mano por `traceId` (el "problema" que motivó el ADR 0031) es un paso que se da
  pocas veces en una demo de portfolio, no un flujo de trabajo diario de un equipo de guardia real.

## Decisión

1. **Se revierte el commit de merge `0929c2f` (`feature/signoz-observabilidad -> develop`)**:
   vuelven `infra/docker-compose.yml` con el contenedor `jaeger` (`jaegertracing/all-in-one`),
   desaparece `infra/signoz/` (config de ClickHouse/collector), los manifiestos de
   `infra/deploy/k8s/base/` vuelven a apuntar a Jaeger, y `services/Core/Observability/ObservabilityExtensions.cs`
   deja de exportar logs por OTLP (Serilog vuelve a escribir solo a consola, tal como fijó el ADR 0006).
2. **Ni el ADR 0006 ni el 0031 se borran** — 0006 queda marcado como vigente de nuevo (brevemente
   superado por 0031, revertido por este documento), y 0031 queda como histórico de por qué se
   probó SigNoz y qué llevó a deshacerlo. Ninguna decisión desaparece sin dejar rastro.
3. **Búsqueda centralizada de logs**: surgió la necesidad real de consultar logs de los 3 servicios
   en un solo sitio (para minería de logs), algo que ni Jaeger ni `docker compose logs -f` cubren
   por sí solos. Queda fuera del alcance de este documento — se trata como una decisión aparte, con
   su propio ADR, en vez de mezclarla con la reversión de SigNoz.

## Consecuencias / riesgo aceptado

- Se pierde la correlación automática logs↔trace de un solo click que daba SigNoz — vuelve el paso
  manual de cruzar `traceId` entre consola y la UI de Jaeger, exactamente el trade-off que ya
  aceptaba el ADR 0006 originalmente.
- El demo script (`docs/demo-script.md`) y la documentación de agente (`.ia/ARCHITECTURE.md`,
  `AGENTS.md`, `README.md`) vuelven a su redacción anterior a SigNoz junto con el revert del merge.
- Si en el futuro se necesita observabilidad unificada de verdad (no solo para una demo), SigNoz
  sigue siendo una opción válida y ya documentada (ADR 0031) — este documento no descarta la idea
  en general, solo concluye que no compensa para el alcance actual del proyecto.
