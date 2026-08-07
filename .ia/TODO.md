# TODO — VenuePulse

Espejo del task-tracker de la sesión. Actualizar al cerrar o abrir tareas.

## En curso

- [ ] **Provisión de zonas por evento vía `EventCreated`** (event-driven): nuevo integration
  event publicado por `Ticketing` (necesita wiring de MassTransit, hoy no publica nada), consumer
  nuevo en `AccessControl` que aprovisiona las 3 zonas del evento, `EventId` añadido a `Zone` +
  migración, `ZonesController` con filtro `?eventId=`, frontend (`shell`/`ticket-checkout`/
  `venue-ops`) actualizado para pasar/mostrar el evento correcto. Ver `.ia/KNOWN-ISSUES.md`.
- [ ] Verificar manualmente desde el navegador (no solo API) el camino feliz y el de
  compensación, con el timeline de la Saga por SignalR — único ítem sin marcar en el checklist
  de `docs/demo-script.md`.

## Pendiente de decidir (ver `docs/ROADMAP.md`)

- [ ] Rellenar `.ia/` con más detalle si aparecen huecos durante el desarrollo en vivo de la
  entrevista.
- [ ] Definir mecanismo de exportación "código sin `.git/` ni `.ia/`" para la entrega real al
  entrevistador (distinto de repartir solo el Docker, que era el plan inicial).

## Hecho

- [x] Scaffolding, 3 microservicios, Saga con Outbox/Inbox, tests (unit + arquitectura +
  integración), frontend (shell + ticket-checkout + venue-ops), CI/CD, documentación completa.
- [x] Estructura de carpetas renombrada a `services/`/`ui/` (desde `src/`/`frontend/`).
- [x] Fix CRLF en `init-multiple-dbs.sh`, nomenclatura de contenedores Docker, limpieza de
  imágenes huérfanas.
- [x] Verificación manual contra `docker-compose` real de ambos caminos de la Saga.
