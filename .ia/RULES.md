# Reglas de código — VenuePulse

## Capas (Clean Architecture por servicio)

`Domain` → `Application` → `Infrastructure` → `Api`. Nunca al revés. Las reglas están
verificadas automáticamente por `services/ArchitectureTests` (ArchUnitNET) — si rompes una capa,
el build falla, no es solo una convención de estilo.

- `Domain` no referencia `Infrastructure` ni `Application` de ningún servicio.
- Solo `CheckIn.Application` puede referenciar `MediatR`. `Ticketing` y `AccessControl` no.
- `VenuePulse.Core` es la única dependencia compartida entre los 3 servicios.

## Convenciones

- Namespaces: `VenuePulse.{Servicio}.{Capa}`, salvo lo compartido en `VenuePulse.Core`.
- Comentarios: solo cuando el PORQUÉ no es obvio (una decisión, un workaround, una invariante no
  evidente). Nunca explicar el QUÉ — el código ya lo dice.
- Un microservicio, una base de datos — nunca acceso cruzado a la base de datos de otro servicio.
- Eventos de integración van en `VenuePulse.Core.Messaging.Contracts`, nunca definidos dentro de
  un servicio consumidor.

## Anti-patrones a evitar

- CQRS/MediatR fuera de `CheckIn` (ceremonia sin beneficio, ver ADR 0003).
- Repositorios genéricos — cada servicio tiene su propio repositorio con métodos explícitos.
- `IConfiguration` inyectado directo — usar opciones tipadas cuando la configuración crezca.
- Añadir infraestructura (Redis, Prometheus, Seq...) sin una necesidad real y documentada.
- `COPY . .` en cualquier Dockerfile — siempre rutas explícitas.

## Definition of Done

- [ ] Compila sin warnings nuevos.
- [ ] `dotnet test services/VenuePulse.slnx` en verde (unit + arquitectura + integración).
- [ ] Si se toca la Saga: test de integración del camino feliz **y** de compensación.
- [ ] Si se añade un evento/comando nuevo: vive en `VenuePulse.Core.Messaging.Contracts`.
- [ ] Si se añade una decisión de arquitectura no trivial: ADR nuevo en `docs/architecture/adr/`.
- [ ] `docker compose -f infra/docker-compose.yml up --build` sigue levantando los 11 contenedores.
- [ ] Ningún Dockerfile nuevo hace `COPY . .`.
