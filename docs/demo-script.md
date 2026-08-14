# Guion de demo

Objetivo: en ~15 minutos, mostrar el sistema funcionando de verdad y en ~10 minutos adicionales
pedir a Claude Code que amplíe algo en vivo, narrando el momento de revisión del código
generado (ver `docs/AI-USAGE.md`).

## 0. Arrancar todo (1 comando)

```bash
docker compose -f infra/docker-compose.yml up --build
```

Abrir:
- `http://localhost:8080` → `shell` (catálogo + aforo en vivo)
- `http://localhost:8080/venue-ops/` → panel de operaciones
- `http://localhost:16686` → Jaeger (trazas)
- `http://localhost:15672` (guest/guest o credenciales del compose) → RabbitMQ management

## 1. Camino feliz (2 minutos)

1. En `shell`, elegir un evento → "Comprar entrada".
2. En el panel de `ticket-checkout`, elegir tipo de entrada y zona, pulsar "Comprar entrada".
3. Señalar el **timeline de la Saga** actualizándose en vivo vía SignalR: Reservando aforo →
   Procesando pago → Confirmando → Confirmada.
4. Copiar el ID de la entrada mostrado.
5. En `venue-ops`, pegar el ID y pulsar "Escanear" → el aforo de la zona sube en vivo, en
   `venue-ops` **y** en el widget de `shell` (dos pestañas distintas, misma actualización).

## 2. Camino de compensación — el momento central (2 minutos)

1. Volver a `shell`, comprar otra entrada.
2. Marcar el checkbox **"Simular fallo de pago"** antes de confirmar.
3. Señalar que el timeline llega hasta "Procesando pago" y se marca en rojo — no es un mensaje
   de error genérico, es la Saga ejecutando una **compensación real**: libera el aforo que ya
   había reservado unos milisegundos antes.
4. Abrir `venue-ops` y mostrar que el aforo de esa zona **no subió** — la reserva se liberó de
   verdad, no fue una simulación de interfaz.

## 3. Trazas distribuidas (1 minuto)

1. Abrir Jaeger, buscar el servicio `VenuePulse.AccessControl` (es donde más spans se agregan)
   y localizar el trace largo (~25-30 spans) de la última compra.
2. Mostrar los spans cruzando `CheckIn` (HTTP + Outbox + Saga) y `AccessControl` (reserva y
   confirmación de zona) con sus duraciones — "esto es lo primero que miraría si algo fuera
   lento en producción". Señalar que `Ticketing` **no** aparece en este trace a propósito: solo
   interviene en la fase de catálogo (el fan ya trae el `eventId`/`ticketTypeId` elegidos), sin
   llamada síncrona en el momento de comprar — ver ADR 0006. Es un buen ejemplo de no forzar
   acoplamiento solo para que la traza "se vea mejor".

## 4. Arquitectura y pruebas (2 minutos)

```bash
dotnet test services/VenuePulse.slnx
```

Señalar: tests de dominio, tests de la Saga con el harness in-memory de MassTransit (camino
feliz y compensación, sin broker real), tests de arquitectura con ArchUnitNET que **fallan el
build** si el dominio depende de Infrastructure, y el test de integración con Testcontainers
que reproduce el flujo completo contra Postgres+RabbitMQ+MongoDB reales.

**CQRS solo donde aporta, no por checklist**: `CheckIn` es el único servicio con CQRS/MediatR
(`Ticketing`/`AccessControl` son Application Service simple) — no por descuido, sino porque es
el único con asimetría real lectura/escritura (la Saga de compra frente a una consulta directa),
ver ADR 0003. `Architecture.Tests` lo hace cumplir: si MediatR apareciera en `Ticketing`/
`AccessControl`, el build fallaría. Es el ejemplo más claro de criterio senior del proyecto:
saber cuándo NO añadir un patrón es tan defendible como saber aplicarlo.

**Cómo escalaría a un cuarto servicio**: un microservicio nuevo referenciaría `Core` (kit de
dominio genérico, sin conocimiento de negocio) y `Contracts` (para publicar/suscribirse a
eventos de integración) — cero cambios en los 3 servicios existentes. `Architecture.Tests`
verificaría automáticamente que no acabe acoplado a ninguno de ellos por accidente (ver ADR
0030).

## 5. Tarea en vivo con Claude Code (10 minutos)

Dos opciones, según el tiempo disponible — pedir explícitamente que se narre el proceso, no
solo el resultado:

**Opción A (baja complejidad, ~5 min)**: "Añade un endpoint de solo lectura
`GET /api/access-control/api/zones/{id}` que además devuelva el porcentaje de ocupación, con
su test unitario." — Mostrar cómo se revisa el diff antes de aceptarlo.

**Opción B (mayor impacto, ~10 min)**: "Añade una nueva rama de compensación a la Saga: si el
tipo de entrada se agota entre la reserva de aforo y la confirmación, se debe liberar el aforo
y rechazar con motivo `TicketTypeSoldOut`, con su test de integración." — Este es el ejemplo
más honesto de "validación de código generado por IA": el motivo de rechazo, el test de
integración con Testcontainers y la comprobación de que no rompe el camino feliz existente son
el momento de revisar el diff con criterio, no de aceptarlo a ciegas.

## Nota sobre el test de integración de la Saga

El test de integración del camino de compensación tuvo un problema de fiabilidad específico del
arnés de test (Testcontainers + `WebApplicationFactory` compitiendo por recursos en una máquina
cargada), documentado en el ADR 0009: confirmado que no era un bug real, sino ruido del entorno
de test. El camino de compensación está verificado manualmente contra `docker-compose` real (ver
checklist más abajo).

## Checklist de verificación manual (verificado contra docker-compose real)

- [x] `docker compose -f infra/docker-compose.yml up --build` levanta los 11 contenedores sin
      errores (`api-ticketing`, `api-accesscontrol`, `api-checkin`, `web-shell`,
      `web-ticket-checkout`, `web-venue-ops`, `gateway`, `postgres`, `mongo`, `rabbitmq`,
      `jaeger`).
- [x] Camino feliz completo (compra → confirmación en <1s → check-in → aforo sube) verificado
      contra la API real: `confirmedSold`/`checkedIn` de la zona pasan de 0 a 1.
- [x] Camino de compensación (simular fallo → rechazo `PaymentFailed` → aforo NO sube) verificado
      contra la API real: la zona se queda en `confirmedSold: 0`.
- [x] Jaeger muestra el trace conectado de una compra completa (~28 spans, CheckIn +
      AccessControl vía Outbox/Saga/RabbitMQ) y de la compensación.
- [ ] Repetir el camino feliz y el de compensación una vez más **desde el navegador** (no solo
      vía API) para confirmar el timeline visual de la Saga por SignalR antes de la demo.
