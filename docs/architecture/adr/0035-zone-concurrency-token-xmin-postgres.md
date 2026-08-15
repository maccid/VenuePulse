# ADR 0035 — El token de concurrencia de Zone no funcionaba en Postgres; se sustituye por xmin

## Contexto

ADR 0027 identificó un riesgo secundario (lost-update entre `Reserve`/`Release`/`Confirm`
concurrentes sobre la misma `Zone`) y lo descartó como fuera de alcance en ese momento. Más
tarde se implementó un intento de cierre (migración `AddZoneRowVersion`, comentario en
`ZoneService.cs` que lo daba por "resuelto"): una shadow property `byte[] RowVersion` marcada
con `IsRowVersion()`, más un bucle de reintento (`ExecuteWithRetryAsync`, hasta 3 intentos) que
captura `DbUpdateConcurrencyException` y relee la zona con datos frescos.

Ese intento no funciona en Postgres. `IsRowVersion()` sobre una columna `byte[]`/`bytea` normal
es el patrón correcto en SQL Server, donde `rowversion` es un tipo nativo que la base de datos
actualiza sola en cada escritura de la fila. En Postgres no existe ese tipo — una columna `bytea`
creada a mano nunca cambia de valor por sí misma salvo que algo (trigger, aplicación) la
reescriba explícitamente, cosa que ni EF Core ni el código del proyecto hacían. Consecuencia: la
comparación optimista en el `UPDATE ... WHERE "RowVersion" = @original` siempre coincide (el
valor nunca varía), así que ningún conflicto se detecta jamás — el lost-update seguía intacto a
pesar del mecanismo de reintento, que nunca llega a activarse porque `DbUpdateConcurrencyException`
nunca se lanza.

Verificado en real contra el stack (`docker compose up`, Postgres real, no Testcontainers): un
pedido pidiendo 8 entradas de "Grada VIP Norte" (capacidad 8, 7 libres en el momento de la
prueba) hizo que las 8 solicitudes de reserva, procesadas casi simultáneamente por
`ReserveZoneSlotConsumer`, reservaran con éxito sin ningún log de "Conflicto de concurrencia" —
la zona terminó con más aforo reservado del que tenía disponible. El propio test unitario
`ZoneServiceTests` que debía cubrir este caso lo enmascaraba: el proveedor InMemory de EF Core
tampoco genera un nuevo valor de `RowVersion` por sí solo, así que el test fijaba ese valor a
mano para forzar el conflicto — pasaba, pero por una razón que no reproducía lo que de verdad
pasa contra Postgres.

## Decisión

Sustituir la columna `RowVersion` por la columna de sistema `xmin`, que Postgres sí actualiza
automáticamente en cada `UPDATE` de la fila, sin necesitar trigger ni columna propia:

```csharp
builder.Property<uint>("xmin")
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

La migración generada por `dotnet ef migrations add` no se pudo usar tal cual: el scaffolder
generó un `AddColumn<uint>(name: "xmin", ...)`, pero `xmin` es un nombre de columna reservado por
Postgres (`ERROR: column name "xmin" conflicts with a system column name` — verificado a mano
contra el Postgres real antes de aplicar nada). La migración se editó para dejar solo el
`DropColumn("RowVersion")`: `xmin` ya existe implícitamente en toda tabla Postgres, no hace falta
ningún DDL para ella.

## Alternativas descartadas

- **Generar el valor de `RowVersion` manualmente en la aplicación** (p. ej. un nuevo GUID en cada
  mutación de `Zone`): funcionaría, pero reimplementa a mano justo lo que `xmin` ya da gratis a
  nivel de motor — más código y más superficie de error por mantener.
- **Serializable/nivel de aislamiento más estricto en la transacción**: cerraría el lost-update
  sin optimistic concurrency, pero cambia el comportamiento de todo `AccessControlDbContext`, no
  solo de `Zone`, y tiene coste de rendimiento bajo alta contención — desproporcionado para este
  caso concreto.
- **Bloqueo pesimista (`SELECT ... FOR UPDATE`)**: cierra el caso, pero requiere abrir una
  transacción explícita alrededor de cada mutación y serializa el acceso a la zona incluso
  cuando no hay conflicto real — el patrón optimista ya existente encaja mejor con la frecuencia
  real de contención esperada en esta demo.

## Consecuencias

- El lost-update queda cerrado de verdad contra Postgres, no solo en apariencia.
- El test `Guardar_una_zona_con_xmin_obsoleto_lanza_conflicto_de_concurrencia` sigue fijando el
  valor a mano (limitación del proveedor InMemory, no de Postgres) — sigue siendo una simulación
  del comportamiento real, no una prueba end-to-end contra Postgres.
- Candidato a revisar junto con `.ia/KNOWN-ISSUES.md` ("aforo fantasma tras timeout de reserva"):
  este bug es un candidato plausible a explicar (parte de) esa intermitencia bajo contención real,
  no confirmado como causa única.

## Actualización — el límite de reintentos era insuficiente

Con el conflicto de concurrencia ya detectado de verdad, `ExecuteWithRetryAsync` (`maxAttempts = 3`)
resultó insuficiente bajo contención real: verificado contra el stack, un pedido de 6 entradas para
una zona con 6 plazas libres generó 6 solicitudes de reserva casi simultáneas, de las cuales varias
agotaron sus 3 intentos y la excepción se propagó sin capturar — el consumer completo
(`ReserveZoneSlotConsumer`) falló, MassTransit lo reintentó con su propia política, y tras agotar
también esos reintentos, 5 mensajes terminaron en la cola de error `ReserveZoneSlot_error` sin que
esos tickets recibieran nunca `ZoneSlotReserved` ni `ZoneSlotReservationFailed` — se quedan en
`Pending` hasta que el watchdog externo los rescata a los 60s.

`maxAttempts` sube de 3 a 15: `PlaceOrderCommandHandler.MaxTicketsPerOrder` limita a 10 entradas por
pedido, así que 10 solicitudes concurrentes contra la misma zona es el peor caso real posible — 15
da margen sin ser un número arbitrario.
