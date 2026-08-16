# ADR 0040 — Lock en memoria por zona en `ZoneService`: cierra la ventana de carrera que el reintento optimista no cubría

**Relacionado:** [ADR 0035](0035-zone-concurrency-token-xmin-postgres.md) (xmin + 15 reintentos ante
`DbUpdateConcurrencyException`), [ADR 0039](0039-saga-outbox-evita-publish-duplicado-en-reintento.md)
(cierra el `Publish()` duplicado de la Saga — causa distinta del mismo síntoma).

## Contexto

Tras el fix de ADR 0039 (evita que un reintento de la Saga duplique `Publish()`), el usuario siguió
reproduciendo pedidos colgados contra el stack real — confirmado que **no** eran duplicados: un
pedido de 4 entradas (2+2 en dos zonas) tuvo una línea huérfana en `AwaitingPayment` con solo **un**
`ReleaseZoneSlot` legítimo publicado para ella (verificado con la traza completa de Seq, sin ningún
`Publish()` repetido esta vez). El fallo real:

```
R-FAULT ReleaseZoneSlot ... System.InvalidOperationException: No hay ninguna reserva pendiente que liberar.
```

**Causa raíz**: `ZoneService.ExecuteWithRetryAsync` solo reintenta ante `DbUpdateConcurrencyException`
(conflicto de escritura, ADR 0035). El guard de invariante de negocio de `Zone.ConfirmSlot()`/
`ReleaseSlot()` (`Reserved <= 0` → excepción) es una categoría de fallo **distinta** que nunca se
reintenta. Bajo contención real — varias líneas de un mismo pedido confirmando o liberando su aforo
casi a la vez contra la **misma** `Zone` — el patrón "leer, mutar en memoria, guardar con reintento
solo ante conflicto de escritura" deja una ventana: dos operaciones pueden leer el mismo
`Reserved` reciente, cada una decide que hay margen, y aunque el conflicto de *escritura* se detecta
y reintenta correctamente, el número de veces que el *dominio* decide "sí hay margen" bajo alta
frecuencia de eventos casi simultáneos no está protegido — el guard revienta sin red de seguridad y
el mensaje muere en la cola de error (`AccessControl` no tiene `UseMessageRetry` de transporte, ver
`.ia/KNOWN-ISSUES.md`).

## Decisión

`ZoneService.ExecuteWithRetryAsync` adquiere un `SemaphoreSlim(1,1)` **por `ZoneId`** (diccionario
estático `ConcurrentDictionary<Guid, SemaphoreSlim>`) antes de leer/mutar/guardar, y lo libera al
terminar (éxito o excepción). Esto serializa **por completo** todas las mutaciones concurrentes sobre
la misma zona dentro del proceso — ya no hay dos operaciones leyendo el mismo estado "fresco" a la
vez, así que el guard de invariante solo puede dispararse ante un bug real de sobreventa, nunca por
una carrera de lectura.

**Por qué un lock en memoria y no algo distribuido**: `AccessControl` corre como una única instancia,
sin réplicas ni load balancer — el mismo criterio que ya fijó ADR 0029 para descartar un backplane de
Redis ("condición explícita bajo la que sí se justificaría: N instancias de una misma API detrás de
un load balancer, escenario de roadmap"). Un lock en memoria del proceso resuelve el 100% de la
contención real de esta demo sin infraestructura nueva.

## Alternativas descartadas

- **Bloqueo pesimista a nivel de base de datos** (`SELECT ... FOR UPDATE` + transacción explícita):
  cierra el mismo caso, pero exige SQL crudo (EF Core no tiene un `.ForUpdate()` de primera clase) y
  complica los tests unitarios existentes, que construyen `ZoneService` contra el proveedor InMemory
  de EF Core (sin soporte de transacciones/SQL crudo) — habría que reescribir varios tests solo para
  acomodar el mecanismo de lock, sin ganar nada frente al lock en memoria mientras siga siendo una
  única instancia.
- **Subir aún más `maxAttempts`**: no cierra nada — el guard de invariante nunca se reintenta,
  subir el límite de reintentos de *concurrencia* no le afecta.
- **Hacer que el guard de invariante tolere "confirmar/liberar de más" silenciosamente**: descartado
  en ADR 0039 por la misma razón — enmascararía una sobreventa real si algún día ocurriera por un
  motivo distinto a esta carrera.
- **Inyectar el lock como dependencia (`IZoneConcurrencyGate` registrado `Singleton`)** en vez de un
  campo `static`: más "limpio" en apariencia, pero cambia la firma del constructor de `ZoneService`
  y rompe los tests unitarios que lo construyen directamente (`new ZoneService(repo, snapshotRepo,
  notifier)`) sin aportar ningún beneficio real — el campo `static` ya es compartido correctamente
  entre las instancias `Scoped` del mismo proceso, que es exactamente lo que hace falta aquí.

## Consecuencias / riesgo aceptado

- El bucle de reintento ante `DbUpdateConcurrencyException` (ADR 0035) se mantiene como red de
  seguridad, aunque con el lock en su sitio ya no debería dispararse en el camino normal — solo
  protegería ante un escenario que hoy no existe (otra instancia u otro proceso tocando la misma fila).
- Si el proyecto alguna vez escalara a varias réplicas de `AccessControl` detrás de un load balancer
  (el mismo escenario que ADR 0029 documenta como condición para reconsiderar Redis), este lock en
  memoria dejaría de ser suficiente — habría que revisar esta decisión junto con esa, no antes.
- No repara los contadores de zona ya corrompidos por incidentes anteriores a este fix (ver nota en
  `.ia/KNOWN-ISSUES.md`) — solo evita que se generen nuevos.
