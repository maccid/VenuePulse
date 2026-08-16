# ADR 0041 — Precio dinámico por zona, no agregado a nivel de evento

**Revierte parcialmente:** [ADR 0016](0016-dynamic-pricing.md) (mantiene las heurísticas de escalón
y el disparador reactivo vía `ZoneOccupancyChanged`; cambia solo el nivel de agregación de la
ocupación).

## Contexto

El ADR 0016 calculaba el precio dinámico con la ocupación **agregada de todo el evento** (todas
las zonas sumadas), a propósito, para no modelar una relación real entre `Zone` (AccessControl) y
`TicketType` (Ticketing) — bounded contexts separados que hoy solo comparten `EventId`.

Probado en real: el usuario compró las 8 plazas de "Grada VIP Norte" (87%→100% de ocupación
**propia**) y el tipo de entrada siguió mostrando `"Demanda baja"`, porque el resto del recinto
(Grada Este/Oeste/VIP Sur, 95 plazas en total) apenas se había vendido — la ocupación agregada del
evento se quedaba en ~23%. Confirmado como comportamiento consciente del ADR 0016, no un bug — pero
al verlo en vivo, ambos coincidimos en que no es defendible: en sistemas reales de ticketing
(aerolíneas, Ticketmaster) el precio dinámico es por categoría/zona, precisamente porque cada una
tiene su propia curva de demanda independiente.

## Decisión

Calcular la ocupación **por zona**, no por evento, reutilizando el mismo criterio de fusión
zona↔tipo de entrada que ya usa el frontend por nombre (`ui/venue-core/src/domain/zoneTicketType.ts`,
`matchTicketType` — igualdad exacta, luego el nombre de tipo más largo contenido en el nombre de
zona, luego "General"/primero) para no introducir dos reglas de fusión divergentes:

1. **`ZoneOccupancyChanged` gana `Name`** (el nombre de la zona, ya expuesto en `ZoneOccupancyDto`
   desde `AccessControl` — cambio aditivo, sin tocar el resto del contrato).
2. **`ZoneDemandSnapshot` (Ticketing) gana `Name`**, poblado por `ZoneOccupancyChangedConsumer`
   junto con el resto de la proyección — migración aditiva `AddZoneDemandSnapshotName`.
3. **`ZoneTicketTypeMatcher`** nuevo en `Ticketing.Domain`: réplica en C# del mismo criterio de
   `matchTicketType` (mantenerlos sincronizados si el criterio cambia en el futuro es responsabilidad
   compartida de ambos lados, como ya ocurría implícitamente antes de este ADR para la fusión visual).
4. **`TicketingService.GetTicketTypesAsync`** deja de pedir un único `GetOccupancyPercentageAsync(eventId)`
   agregado — en su lugar, agrega capacidad/disponible **por `TicketType`** sumando solo las zonas que
   el matcher le asigna, y calcula el tier de cada tipo con su propia ocupación. Si dos zonas distintas
   emparejan con el mismo tipo (no ocurre con el catálogo actual, pero es soportado), se suman.

## Alternativas descartadas

- **Modelar una relación real `Zone`↔`TicketType`** (FK o tabla de mapeo explícita): más superficie
  de acoplamiento entre bounded contexts de la que este cambio necesita — la fusión por nombre ya es
  el criterio que el frontend usa y que el catálogo (`DefaultTicketTypeCatalog`, ADR 0018/0026) ya
  garantiza 1:1 a propósito (cada zona "especial" se llama igual que su tipo).
- **Mantener el agregado por evento y solo "avisar" en el frontend que es una simplificación**:
  descartado tras verlo en vivo — el propio usuario señaló que rompe la intuición al probarlo,
  suficiente motivo para cerrarlo en vez de documentarlo como limitación aceptada.

## Consecuencias / riesgo aceptado

- Si el catálogo de `TicketType`s de un evento no tuviera ningún nombre que empareje con una zona
  dada (no ocurre hoy, `DefaultTicketTypeCatalog` está alineado a propósito), esa zona no contribuye
  a ningún tier y su ocupación se ignora — mismo comportamiento silencioso que ya tenía el frontend
  (`matchTicketType` cae a `"General"`/primero, nunca lanza).
- Los tests existentes de `TicketingServiceTests`/`ZoneOccupancyChangedConsumerTests` que usaban una
  única zona sin nombre real siguen pasando sin cambios de expectativa — con un solo `TicketType` en
  el evento, el fallback ("General"/primero) sigue asignando esa zona a ese único tipo, igual que el
  agregado por evento hacía antes.
- Sin cambios en las heurísticas de escalón (`<40%/-10%`, `<70%/base`, `<90%/+15%`, `≥90%/+30%`) ni en
  el disparador (reactivo, ante cualquier cambio de `Zone`, ver `ZoneService.ExecuteWithRetryAsync`).
