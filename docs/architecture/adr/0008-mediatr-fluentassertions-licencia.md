# ADR 0008 — Uso de MediatR y FluentAssertions pese a su licencia comercial (2025)

## Contexto

Jimmy Bogard (MediatR) y Dennis Doomen (FluentAssertions) movieron ambas librerías a un modelo
de licencia comercial de pago para uso en producción a partir de sus versiones más recientes
(2025). Es un dato de actualidad que conviene conocer y saber razonar, no ignorar.

## Decisión

Se usan igualmente en esta demo:

- **MediatR** en `CheckIn` (único servicio con CQRS real, ver ADR 0003).
- **FluentAssertions** en los tests de los 3 servicios.

## Justificación

Este es un proyecto de demo/portfolio sin fines comerciales ni generación de ingresos — entra
dentro del uso permitido de ambas licencias. Además, siguen siendo el estándar que muchas
empresas (potencialmente el propio Sportian) usan en producción bajo licencia pagada: mostrar
que se conocen y se saben aplicar correctamente es mejor argumento de entrevista que evitarlas
por evitar. El propio hecho de documentar este ADR — conocer el cambio de licencia de 2025 y
haber evaluado conscientemente si aplica a este contexto — es en sí mismo la señal de criterio
senior que se busca transmitir, no la elección de librería en sí.

## Nota

Si este código se llevara a un contexto comercial real, habría que verificar los términos de
licencia vigentes en ese momento y, si no se ajustan, sustituir MediatR por un dispatcher CQRS
mínimo propio (patrón trivial: interfaces `ICommandHandler`/`IQueryHandler` + resolución por
DI) y FluentAssertions por una alternativa MIT como Shouldly.
