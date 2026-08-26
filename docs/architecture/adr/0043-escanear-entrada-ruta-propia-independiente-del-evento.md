# ADR 0043 — "Escanear entrada" pasa a ruta propia en `venue-ops`, independiente del evento

## Contexto

`ScanPanel` vivía dentro de `OccupancyGrid`, el layout que `EventWorkspace` renderiza solo cuando
`event.status === 'SalesClosed'` — es decir, **no era posible escanear ninguna entrada mientras el
evento seguía `OnSale`**. Verificado contra el backend real (`CheckInCommand.cs`): el check-in no
depende en absoluto del estado del evento, solo del propio `Ticket` (`ticket.MarkCheckedIn()`,
localizado por su `Id`, un GUID único globalmente que ya lleva su `EventId`/`ZoneId`). La
restricción era puramente de frontend, sin ninguna justificación de negocio detrás — en un
recinto real, la gente empieza a entrar mientras las últimas entradas se siguen vendiendo.

Además, al vivir dentro de `EventWorkspace`, el escaneo exigía tener una pestaña de evento activa
seleccionada, cuando en realidad no hace falta: el ticket ID basta para resolver a qué evento y
zona pertenece.

## Decisión

Extraer el escaneo a su propia ruta de cliente, **`/venue-ops/scan`**, fuera del árbol de
`EventWorkspace` — no depende de qué evento esté seleccionado ni de su estado.

- **Sin librería de router**: mismo criterio que el `?event=` ya existente
  (`useEventCatalog.ts`) — se compara `window.location.pathname` a mano en `App.tsx` y se
  renderiza `ScanPage` en vez del resto de la app. Con un único caso de uso adicional, añadir
  `react-router` sería la misma sobreingeniería que el proyecto evita en el resto de decisiones
  (ver ADR 0006/0010).
- **Sin cambios en el gateway**: `nginx.conf` ya reenvía cualquier ruta bajo `/venue-ops/` al
  contenedor de `venue-ops` (`location /venue-ops/` con `rewrite`), y el `spa.conf` interno de esa
  app ya tiene `try_files $uri $uri/ /index.html` — la navegación directa o el refresco en
  `/venue-ops/scan` ya funcionaban sin tocar nada de infraestructura.
- `ScanPage` reutiliza el mismo `scanTicket` que ya vivía en `App.tsx`, sin duplicar lógica.

## Alternativas descartadas

- **Mantenerlo dentro de `OccupancyGrid` pero mostrarlo también en `SalesLayout`**: cerraría el
  bug del estado del evento, pero no el segundo problema (depender de tener un evento
  seleccionado) — un operador en la puerta no necesariamente sabe ni le importa qué evento está
  activo en la pestaña de otro compañero.
- **Micro frontend nuevo** (`/scan/`, hermano de `shell`/`venue-ops`/`ticket-checkout`): evaluado y
  descartado — coste real (app+build+Dockerfile+nginx+compose propios) desproporcionado frente al
  beneficio, y complica el guion de demo en vivo sin necesidad real demostrada. El escaneo sigue
  siendo una herramienta interna, el mismo criterio de audiencia que ya separa `venue-ops` de
  `shell` (ADR 0004) no aplica aquí — ambas rutas son "interno".

## Consecuencias

- El check-in ya es posible en cualquier momento del ciclo de vida del evento (`OnSale` o
  `SalesClosed`), coherente con el backend.
- `docs/demo-script.md` actualizado: el paso de escaneo apunta a `/venue-ops/scan` en vez de
  asumir que aparece dentro del panel de operaciones general.
- Riesgo aceptado: es un enrutado manual, no una librería — si `venue-ops` acumulara más de 2-3
  vistas de este tipo, revisar si compensa adoptar `react-router` en ese punto (mismo criterio de
  "roadmap si crece de verdad" que ya usa ADR 0004 para Module Federation).
