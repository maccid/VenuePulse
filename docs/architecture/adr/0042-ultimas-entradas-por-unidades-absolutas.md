# ADR 0042 — "Últimas entradas" también por unidades absolutas, no solo por porcentaje

**Relacionado:** [ADR 0041](0041-dynamic-pricing-por-zona.md) (precio dinámico por zona — este ADR
no cambia la agregación por zona, solo añade un criterio adicional al cálculo del tier).

## Contexto

Tras el ADR 0041 (precio por zona), probado en real: al comprar la penúltima/última entrada de
"Grada VIP Sur" (capacidad 7), el precio mostrado seguía en `"Demanda alta"` en vez de
`"Últimas entradas"`. No era un bug — es una consecuencia matemática de la heurística por
porcentaje (ADR 0016): para que un comprador **vea** el tier `≥90%` con inventario aún disponible
hace falta que exista un entero `N` de confirmadas con `N/capacidad ≥ 0,90` y `N < capacidad`
(para que quede al menos 1 unidad). Despejando, eso solo es posible si `capacidad ≥ 10`.

Con capacidad 7 u 8 (las zonas VIP de este evento), el salto es siempre directo de `"Demanda
alta"` (≤89%) a zona agotada — nadie ve nunca `"Últimas entradas"` ahí. Confirmado con el usuario
que esto no es defendible: en sistemas reales de ticketing, "solo quedan N entradas" es una señal
independiente del porcentaje, no derivada de él.

## Decisión

`DynamicPricing.Apply` gana un criterio adicional, evaluado **antes** del switch por porcentaje y
sin modificar ninguno de sus tramos existentes: si `availableToSell <= DynamicPricing.LastUnitsThreshold`
(fijado en 2), el tier es `TierUltimas` sin importar el porcentaje de ocupación. `TicketingService`
pasa el `availableToSell` agregado por `TicketType` (ya calculado para el porcentaje, ADR 0041) al
mismo método, sin una segunda consulta.

## Alternativas descartadas

- **Umbral relativo al aforo** (p. ej. "últimas entradas" si quedan `≤10%` de la capacidad, con
  mínimo 1): resuelve lo mismo para zonas pequeñas, pero para zonas grandes cambiaría cuándo se
  activa el tier respecto al comportamiento actual (ADR 0016) sin necesidad — el criterio absoluto
  fijo no interfiere en absoluto con zonas donde el porcentaje ya cruza el 90% con inventario
  disponible (≥10 plazas), que siguen exactamente igual que antes.
- **Bajar el umbral de porcentaje global** (p. ej. de 90% a 80%): cambiaría el comportamiento para
  **todas** las zonas, incluidas las grandes, solo para arreglar el caso de las pequeñas — efecto
  colateral innecesario sobre una heurística que ya funcionaba bien donde el aforo lo permite.

## Consecuencias

- Sin cambios en los 4 tramos de porcentaje existentes (40/70/90%) ni en sus multiplicadores — solo
  se añade una condición previa. Verificado con los tests existentes de `DynamicPricingTests`
  (0/1/39/40/69/70/89/90/100%) sin ninguna modificación de expectativa.
- Zonas con capacidad ≤ `2 × LastUnitsThreshold` (≤4 plazas) podrían mostrar `"Últimas entradas"`
  desde una ocupación baja en términos de porcentaje (p. ej. una zona de 3 plazas con 1 vendida ya
  tendría 2 disponibles, en el límite) — aceptado: para una zona tan pequeña, "quedan 2" es una
  señal de escasez real igual de válida que el porcentaje.
