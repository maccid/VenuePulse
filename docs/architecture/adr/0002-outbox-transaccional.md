# ADR 0002 — Outbox transaccional nativo de MassTransit sobre EF Core

## Contexto

Publicar un evento de integración justo después de escribir en la base de datos tiene el
clásico problema del "dual write": si la escritura en BD tiene éxito pero publicar en el bus
falla (o al revés), el sistema queda inconsistente sin ninguna transacción que lo cubra.

## Decisión

Usar el Outbox transaccional **nativo de MassTransit** (`AddEntityFrameworkOutbox<TDbContext>`)
en `CheckIn` y `Ticketing` (los servicios que publican eventos como parte de comandos de
escritura), en vez de construir un Outbox a mano.

Mecánica: el handler hace `repository.AddAsync(entidad)` → `publishEndpoint.Publish(evento)`
→ `repository.SaveChangesAsync()`, en ese orden. El `Publish` no toca la red todavía: queda
en memoria hasta que `SaveChangesAsync` escribe, **en la misma transacción de base de datos**,
tanto la fila de negocio como el mensaje en la tabla `OutboxMessage`. Un proceso de MassTransit
(`UseBusOutbox`) entrega después esos mensajes al broker, con deduplicación de consumo (patrón
Inbox, tabla `InboxState`) para una semántica exactly-once efectiva sobre un transporte que solo
garantiza at-least-once.

## Por qué la librería y no una tabla + polling casero

El Outbox de MassTransit es un mecanismo probado en producción; un Outbox casero tendría que
reinventar la gestión de concurrencia sobre la misma tabla sin beneficio real. El mecanismo
(interceptor de `SaveChanges`, entrega asíncrona vía hosted service) se documenta aquí y se
explica igual sin haberlo escrito desde cero.

## Alcance

`CheckIn` y `Ticketing` tienen Outbox propio — ambos escriben una entidad y publican un evento
en el mismo `SaveChangesAsync` (`TicketPurchased` en CheckIn, `EventCreated` en el seed de
Ticketing). `AccessControl` consume comandos y publica eventos de respuesta, pero no necesita
atomicidad "cambio de agregado + evento" en el mismo grado (sus mutaciones son más simples — ver
ADR 0003), así que usa MassTransit sin Outbox configurado.
