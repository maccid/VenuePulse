# ADR 0009 — Depuración real: MongoDB.Driver GuidRepresentation y downgrade de MassTransit

## Contexto

Durante el desarrollo, el test de integración del **camino feliz** de la Saga (contra
Postgres+RabbitMQ+MongoDB reales vía Testcontainers) fallaba de forma reproducible: el primer
mensaje se procesaba, pero la Saga nunca avanzaba. Esto queda documentado a propósito porque es
un ejemplo real de depuración de un problema de integración, no solo una decisión de diseño.

## Diagnóstico (resumen del proceso real)

1. Se confirmó con un test de aislamiento que RabbitMQ entregaba los mensajes correctamente
   entre procesos (el "doble" de AccessControl del test recibía `ReserveZoneSlot`).
2. Se inspeccionaron las colas de RabbitMQ vía su API de administración
   (`/api/queues/%2F/<cola>_error/get`) y apareció el mensaje de fallo real:

   ```
   MongoDB.Bson.BsonSerializationException: GuidSerializer cannot serialize a Guid when
   GuidRepresentation is Unspecified.
   ```

3. **Causa raíz**: MongoDB.Driver 2.19+ (y toda la serie 3.x) dejó de asumir una representación
   de `Guid` por defecto. Sin registrar una explícitamente, serializar **cualquier** `Guid` en
   un documento (aquí, `TicketPurchaseSagaState.ZoneId`) lanza esa excepción en el primer
   intento de guardar la Saga.

## Fix

Registro explícito e idempotente en `VenuePulse.Core.Messaging.MessagingExtensions`, ejecutado
antes de configurar el repositorio de Mongo de la Saga:

```csharp
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
```

## Decisión adicional: downgrade de MassTransit 9.2.0 a 8.5.10

Durante la misma investigación, el **camino de compensación** seguía fallando de forma
distinta (sin excepción visible) con MassTransit 9.2.0 — una versión muy reciente. Tras
descartar varias hipótesis de la propia lógica de la Saga (bifurcación con `.IfElse`, con dos
`When` predicados, y finalmente con `.ThenAsync` + `if`/`else` de C# puro — las tres formas
igualmente válidas), se bajó a **MassTransit 8.5.10** (última release de la serie 8, mucho más
madura y probada en producción). Esto restauró la fiabilidad del camino feliz de forma
consistente.

## Verificación cruzada posterior contra `docker-compose` real

Antes de la presentación se ejecutaron ambos caminos directamente contra el `docker-compose`
completo (sin Testcontainers ni `WebApplicationFactory` de por medio): el camino feliz confirma
en menos de 1 segundo y el camino de compensación libera el aforo reservado correctamente
(`confirmedSold` de la zona se queda en 0). Esto confirma la hipótesis: el problema de
fiabilidad del test era específico del arnés (recursos compartidos entre Testcontainers y el
`WebApplicationFactory` en proceso), no un defecto real de la Saga ni de MassTransit 8.5.10. Se
decide **no** volver a MassTransit 9.2.0 pese a esta confirmación: 8.5.10 es la última release
madura de la serie 8, no hay ninguna necesidad funcional de estar en una versión recién
publicada, y hacerlo ahora solo reintroduce un riesgo ya evitado sin ningún beneficio real.

## Por qué se documenta esto

Es exactamente el tipo de evidencia que la oferta pide sobre "IA como acelerador validado por
criterio senior": la IA generó hipótesis y diagnósticos rápidamente, pero decidir cuándo una
librería muy reciente es la causa más probable, cuándo aislar con un test mínimo, y cuándo
aceptar una limitación del entorno de test (ver `docs/AI-USAGE.md`) en vez de seguir
depurando indefinidamente, fueron decisiones de criterio humano.
