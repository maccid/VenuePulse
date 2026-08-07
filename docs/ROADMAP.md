# Roadmap / decisiones pendientes de revisar

Notas abiertas, no compromisos — para retomar antes de la entrevista si hay tiempo.

## Estructura del repo

- Resuelto: un único monorepo con nombres explícitos por tipo — `services/` (backend .NET,
  incluye `Core/`), `ui/` (3 apps React), `infra/` (docker-compose, K8s, Jenkins, Nginx),
  `docs/`, `.ia/`. Sigue siendo un único monorepo (a propósito distinto de la estructura de dos
  repos `apps.olix`/`services.olix`), pero con nombres de carpeta más explícitos que el
  `src`/`frontend` genérico inicial.

## `.ia/`

- Hoy es un placeholder neutro (`README.md`) a propósito (ver ADR 0007): nunca debe llevar
  contenido propietario de la metodología real de `olix.workspace`.
- Pendiente de decidir: si se rellena con **estructura de carpetas** similar a la de Olix
  (`specs/`, `tasks/`, `prompts/`) pero con contenido 100% nuevo y trazable a este proyecto
  (decisiones reales tomadas en esta conversación, no copiado) — a confirmar con el candidato
  qué contenido concreto quiere ahí antes de tocarlo.

## `VenuePulse.Core`

- Es intencionalmente más pequeño que `olix.core`: código nuevo para esta demo, no una réplica.
  Ampliarlo solo si aporta una demostración real de patrón (no por aparentar más volumen).

## Aforo por evento (en curso)

- Bug real detectado en pruebas manuales: `Zone` no tenía `EventId`, así que el aforo se
  compartía entre eventos distintos en el mismo recinto de forma indefinida.
- Fix elegido: arquitectura event-driven — `Ticketing` publica `EventCreated` al sembrar/crear
  un evento; `AccessControl` lo consume y aprovisiona sus 3 zonas ya vinculadas a ese `EventId`.
  Ver el ADR correspondiente una vez implementado (pendiente de redactar).
