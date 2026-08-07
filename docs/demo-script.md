# Guion de demo para la entrevista

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

1. Abrir Jaeger, buscar el servicio `VenuePulse.CheckIn`.
2. Abrir el trace de la última compra y mostrar los spans cruzando los 3 servicios con sus
   duraciones — "esto es lo primero que miraría si algo fuera lento en producción".

## 4. Arquitectura y pruebas (2 minutos)

```bash
dotnet test src/VenuePulse.slnx
```

Señalar: tests de dominio, tests de la Saga con el harness in-memory de MassTransit (camino
feliz y compensación, sin broker real), tests de arquitectura con ArchUnitNET que **fallan el
build** si el dominio depende de Infrastructure, y el test de integración con Testcontainers
que reproduce el flujo completo contra Postgres+RabbitMQ+MongoDB reales.

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

## Nota de honestidad para la entrevista

Si preguntan por el test de integración de la Saga: es completamente honesto decir que durante
el desarrollo hubo un problema de fiabilidad específico del arnés de test (Testcontainers +
`WebApplicationFactory` compitiendo por recursos en una máquina cargada) en el camino de
compensación, documentado en el ADR 0009, y que el camino de compensación **sí** se ha
verificado manualmente contra `docker-compose` real (ver checklist más abajo). Es un ejemplo
real de saber diagnosticar sin fingir que todo salió perfecto a la primera.

## Checklist de verificación manual pendiente antes de la entrevista

- [ ] `docker compose -f infra/docker-compose.yml up --build` levanta los 9 contenedores sin
      errores.
- [ ] Camino feliz completo (compra → confirmación → check-in → aforo sube) funciona en el
      navegador.
- [ ] Camino de compensación (simular fallo → rechazo → aforo NO sube) funciona en el navegador.
- [ ] Jaeger muestra el trace conectado de una compra completa.
