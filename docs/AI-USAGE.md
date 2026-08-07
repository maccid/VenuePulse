# Uso de IA en el desarrollo de VenuePulse

Este documento no es una declaración genérica de "usé IA". Es un registro concreto, fase por
fase, de qué generó la IA, qué corregí o validé yo con criterio senior, y por qué — porque la
oferta pide exactamente eso: usar la IA como acelerador, no como sustituto del juicio técnico.

## Cómo se trabajó

Todo el código de este repositorio se generó con **Claude Code**, en sesiones dirigidas por mí:
yo tomaba cada decisión de arquitectura (dominio, alcance, qué construir y qué documentar como
roadmap), y la IA ejecutaba, proponía alternativas cuando se le pedían, y — sobre todo — hacía
de par de depuración cuando algo no funcionaba a la primera. Ese último punto es el más
representativo del "acelerador exponencial": no solo generar código nuevo, sino diagnosticar
sistemáticamente por qué algo real (una integración con MongoDB, una librería de mensajería)
no se comportaba como se esperaba.

## Por fase

### Arquitectura y decisiones (ADRs)

Cada ADR en `docs/architecture/adr/` se redactó a partir de una decisión que yo ya había
tomado en la conversación (p. ej. "RabbitMQ en vez de Azure Service Bus", "CQRS solo en
CheckIn"). La IA estructuró el razonamiento y lo puso por escrito; yo revisé cada uno y ajusté
matices (por ejemplo, qué se acepta como trade-off consciente frente a qué es una limitación
real). Ejemplo concreto: la decisión de combinar el dominio "apuestas" original con el
feedback real de Sportian/Smart Venues fue mía tras recibir información del proceso de
selección — la IA rediseñó el dominio completo (Ticketing/AccessControl/CheckIn) sobre esa
corrección en minutos, no en horas.

### Scaffolding de las 3 capas por servicio

Clean Architecture (Domain/Application/Infrastructure/Api) por servicio, `VenuePulse.Core`
compartido, configuración de MassTransit/EF Core/MediatR: generado por la IA siguiendo el
patrón ya acordado. Validación manual: invariantes de dominio (p. ej. que `Zone.RecordCheckIn`
no pueda superar las entradas confirmadas), y que ningún servicio tuviera CQRS "porque sí"
(ver ADR 0003) — eso lo revisé explícitamente en cada Application Service generado.

### Tests

Generación asistida de tests unitarios (dominio + Saga con el harness in-memory de
MassTransit) y del test de integración con Testcontainers. Validación manual: cobertura de
casos borde (¿qué pasa si se libera una reserva que no existe? ¿y si se hace check-in dos
veces?) — añadidos explícitamente tras revisar qué faltaba, no generados de una sola pasada.

### Depuración real (el ejemplo más honesto de "validar código de IA")

Ver **ADR 0009** para el detalle técnico completo. En resumen: el camino feliz de la Saga
fallaba contra MongoDB real con una excepción de serialización de `Guid`. El proceso fue:

1. La IA propuso y ejecutó un test de aislamiento (RabbitMQ entre dos procesos) para descartar
   el broker como causa.
2. La IA inspeccionó las colas de error de RabbitMQ vía su API HTTP y encontró la excepción
   real (`GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified`).
3. Yo acepté la hipótesis (cambio de comportamiento de MongoDB.Driver 2.19+) y la IA aplicó el
   fix (`BsonSerializer.RegisterSerializer`).
4. Cuando el camino de **compensación** seguía fallando por una causa distinta, la IA probó
   tres formas distintas de bifurcar la Saga (`.IfElse`, `When` con predicado, `.ThenAsync` con
   `if`/`else` de C#) y un downgrade de versión de MassTransit — **yo decidí en qué punto
   parar** de perseguir un fallo específico del arnés de test (Testcontainers +
   `WebApplicationFactory` en la misma máquina bajo carga) y documentarlo honestamente en vez
   de seguir indefinidamente, dejando la verificación cruzada pendiente contra `docker-compose`
   real (ver `docs/demo-script.md`).

Este último punto es deliberadamente el ejemplo que más se cuenta en la entrevista: la IA es
rápida generando hipótesis y ejecutando diagnósticos, pero decidir **cuándo parar, qué aceptar
como limitación de entorno y qué exigir como bug real** es criterio humano, no algo que se
delega.

### Documentación

Este mismo documento, el README principal y el guion de demo se redactaron con IA a partir de
instrucciones mías sobre qué debía transmitir cada uno — y se revisaron para que no
sobre-prometan (ver la tabla "qué es real vs documentado" del README).

## Buenas prácticas aplicadas

- **Nada se aceptó sin ejecutar**: cada servicio se compiló y sus tests se corrieron realmente
  antes de darlo por completado — no hay código "generado y asumido correcto".
- **Las librerías con licencia comercial reciente** (MediatR, FluentAssertions) se evaluaron
  conscientemente, no se usaron por inercia (ver ADR 0008).
