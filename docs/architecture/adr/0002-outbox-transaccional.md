# ADR 0002 — Outbox transaccional nativo de MassTransit sobre EF Core

## Contexto

Publicar un evento de integración justo después de escribir en la base de datos tiene el
clásico problema del "dual write": si la escritura en BD tiene éxito pero publicar en el bus
falla (o al revés), el sistema queda inconsistente sin ninguna transacción que lo cubra.

## Decisión

Usar el Outbox transaccional **nativo de MassTransit** (`AddEntityFrameworkOutbox<TDbContext>`)
en `Ticketing`... no, en `CheckIn` (el único servicio que publica eventos como parte de
comandos de escritura), en vez de construir un Outbox a mano.

Mecánica: el handler hace `repository.AddAsync(entidad)` → `publishEndpoint.Publish(evento)`
→ `repository.SaveChangesAsync()`, en ese orden. El `Publish` no toca la red todavía: queda
en memoria hasta que `SaveChangesAsync` escribe, **en la misma transacción de base de datos**,
tanto la fila de negocio como el mensaje en la tabla `OutboxMessage`. Un proceso de MassTransit
(`UseBusOutbox`) entrega después esos mensajes al broker, con deduplicación de consumo (patrón
Inbox, tabla `InboxState`) para una semántica exactly-once efectiva sobre un transporte que solo
garantiza at-least-once.

## Por qué la librería y no una tabla + polling casero

Es más defendible en una entrevista decir "conozco el framework lo bastante bien para no
reinventar su Outbox con sus propios bugs de concurrencia" que enseñar un Outbox casero. El
candidato debe poder explicar igualmente el mecanismo (interceptor de `SaveChanges`, entrega
asíncrona vía hosted service) sin haberlo escrito él mismo.

## Alcance

Solo `CheckIn` tiene Outbox propio. `AccessControl` consume comandos y publica eventos de
respuesta, pero no necesita atomicidad "cambio de agregado + evento" en el mismo grado (sus
mutaciones son más simples — ver ADR 0003), así que usa MassTransit sin Outbox configurado.
