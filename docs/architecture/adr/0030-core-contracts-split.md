# ADR 0030 — Separar `VenuePulse.Core` en `Core` + `Contracts`, eliminar la fuga de MediatR

## Contexto

Una revisión comparando la estructura de carpetas de VenuePulse con la de otro proyecto .NET del
mismo autor (`services.olix`) encontró que `VenuePulse.Core` era un único ensamblado que mezclaba
tres cosas de naturaleza distinta:

1. Kit de dominio genérico y sin dependencias (`Entity`, `Guard`, `Result`, `ValueObject`,
   `PagedResult`, `IAuditable`).
2. Wiring de infraestructura compartido (`MessagingExtensions` de MassTransit, interceptor de
   auditoría de EF Core, extensiones de observabilidad y de Api).
3. Contratos de eventos de integración (`IntegrationEvent` + los mensajes de
   `Messaging/Contracts/*.cs`).

Ese único ensamblado además referenciaba el paquete **MediatR**, por dos motivos:
`Cqrs/LoggingBehavior.cs` (un `IPipelineBehavior` cuyo único consumidor real es `CheckIn`) e
`IDomainEvent : INotification` (`Core.Domain`). Consecuencia real, verificada con `dotnet build`:
`Ticketing.Domain` y `AccessControl.Domain` recibían el paquete MediatR de forma **transitiva**
vía `Core`, pese a que ningún tipo de MediatR se usaba en su código — exactamente lo que ADR 0003
y `.ia/RULES.md` (*"Solo CheckIn.Application puede referenciar MediatR"*) querían evitar. Ninguna
regla de `Architecture.Tests` lo detectaba: la regla existente (`Solo_CheckIn_aplica_CQRS_con_MediatR`)
solo analiza el namespace `Application` de cada servicio, nunca `Core.Domain`.

Investigando el porqué de `IDomainEvent`, se confirmó que es maquinaria **muerta**: `Entity.Raise()`
no se llama en ningún punto del código, y no existe un solo `INotificationHandler<T>` en toda la
solución. El mecanismo de domain events nunca llegó a despacharse.

## Decisión

### 1. Dos proyectos, no tres

Se separó en `services/Core` (kit de dominio + todo el wiring de infraestructura) y
`services/Contracts` (contratos de eventos de integración, agrupados por servicio de origen:
`Contracts/Ticketing/`, `Contracts/AccessControl/`, `Contracts/CheckIn/`).

Se evaluó y descartó una separación en tres proyectos (`Kernel`/`Bootstrap`/`Contracts`, inspirada
literalmente en la estructura de `services.olix`): el problema real (la fuga de MediatR) no
necesitaba tres proyectos para resolverse, y separar el kit genérico del wiring de infraestructura
no evita ningún acoplamiento dañino **en este proyecto concreto** — los 3 microservicios ya
necesitan MassTransit y EF Core directamente para su propio trabajo, así que tenerlos disponibles
transitivamente vía `Core` no perjudica a nadie que no los necesitara igualmente (a diferencia de
MediatR, que Ticketing/AccessControl no necesitan para nada). Mantener esa separación habría sido
la sobre-ingeniería que el propio proyecto evita a propósito (ADR 0006).

`Contracts` sí se mantiene como proyecto separado, por una razón distinta y que sí aplica aquí: es
el protocolo de cable acordado entre quien publica y quien consume un mensaje por el bus — más
parecido a un esquema/IDL compartido que a "código interno reutilizable". Mismo criterio (más
simple) que `Olix.Events.Contracts` en el proyecto de referencia.

### 2. Eliminar el mecanismo muerto de domain events

`IDomainEvent.cs` eliminado. `Entity<TId>` pierde `DomainEvents`, `Raise()` y `ClearDomainEvents()`
— nunca se usaban. Los 3 `DbContext` pierden sus llamadas a `builder.Ignore(x => x.DomainEvents)`,
ya innecesarias.

### 3. `MediatR` fuera de `Core`

`Cqrs/LoggingBehavior.cs` se mueve a `CheckIn/Application/LoggingBehavior.cs`, su único consumidor
real. `Core.csproj` deja de declarar `<PackageReference Include="MediatR" />`. Con esto y con el
punto 2, ningún tipo de `Core` depende ya de MediatR.

### 4. Regla nueva en `Architecture.Tests`

`Un_servicio_no_referencia_directamente_a_otro`: ningún servicio (`Domain`/`Application`/
`Infrastructure`) puede depender de los tipos de otro servicio — solo de `Core`/`Contracts`. Cierra
el hueco que permitió la fuga de MediatR sin que ningún test lo detectara. Verificada: 10/10 tests
de `Architecture.Tests` en verde (7 reglas previas + 3 casos nuevos, uno por servicio).

### 5. Limpieza de organización (sin impacto arquitectónico)

- Interfaces de puertos agrupadas en `Application/Abstractions/` en los 3 servicios (antes sueltas
  en la raíz de `Application/`).
- `CheckIn/Application/Dtos.cs` partido por concepto (`Dtos/TicketDtos.cs`, `Dtos/OrderDtos.cs`,
  `Dtos/RevenueDtos.cs`).
- DTOs de request de los Controllers de `CheckIn` movidos a `Api/Contracts/` (antes definidos
  inline en el propio fichero del Controller).

### 6. `TimeProvider` en el interceptor de auditoría

`AuditingSaveChangesInterceptor` (ver ADR 0011) pasa de `DateTimeOffset.UtcNow` estático a
`TimeProvider` inyectado — mejora la testabilidad (permite un reloj falso en tests) sin cambiar el
punto de uso: mantiene un constructor sin parámetros que usa `TimeProvider.System` por defecto.

### 7. Red de seguridad para excepciones no controladas

`GlobalExceptionHandler` (patrón `IExceptionHandler` de .NET 8+, registrado en los 3 `Program.cs`
vía `AddVenuePulseExceptionHandling()` + `app.UseExceptionHandler()`) captura cualquier excepción
no controlada y devuelve un `ProblemDetails` JSON en vez de la página de error por defecto de
ASP.NET Core. **No sustituye ni se mezcla con `Result`** (fallos de negocio esperables, ver ADR
0011): es solo la red de seguridad para lo que hoy no tenía ninguna, un bug o un fallo de
infraestructura sin capturar.

## Namespaces sin cambiar

Decisión deliberada: los namespaces de C# (`VenuePulse.Core.Domain`, `VenuePulse.Core.Data`,
`VenuePulse.Core.Messaging`, `VenuePulse.Core.Messaging.Contracts`, `VenuePulse.Core.Observability`,
`VenuePulse.Core.Api`) no cambiaron aunque los ficheros se movieron de proyecto y de carpeta. Un
namespace no tiene por qué coincidir con el nombre del ensamblado o de la carpeta que lo contiene
en .NET — mantenerlos igual evitó tocar varias decenas de directivas `using` en el resto del código
sin ningún beneficio funcional real.

## Consecuencias

- `Ticketing.Application` y `AccessControl.Application` ganaron referencias explícitas
  (`Ticketing.Application` → `Contracts`; `AccessControl.Application` sigue recibiendo `EventType`
  transitivamente vía `Domain` → `Core`, sin cambio) allí donde antes las recibían solo de forma
  transitiva a través del `Core` mezclado — ahora es explícito qué necesita cada capa.
- Verificado en cada paso: `dotnet build services/VenuePulse.slnx` en verde (0 errores) y
  `dotnet test` (excluidos los tests de integración con Testcontainers, sin relación con este
  cambio) en 133/133 — `Ticketing.Tests` 33/33, `CheckIn.Tests` 43/43, `AccessControl.Tests` 47/47,
  `Architecture.Tests` 10/10.
- Riesgo aceptado, no resuelto aquí: la regla nueva de `Architecture.Tests` cubre `Domain`/
  `Application`/`Infrastructure` pero no `Api` — los ensamblados `Api` de los 3 servicios no se
  cargan en `CleanArchitectureTests` (limitación ya preexistente a este ADR, no introducida por él).
