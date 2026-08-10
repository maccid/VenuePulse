# ADR 0004 — Micro frontends: composición por Nginx + Web Component, no Module Federation

## Contexto

La oferta pide supervisión técnica de React con micro frontends. A esta escala (un shell +
un remoto) hay que elegir entre Module Federation (Webpack/Vite) o una composición más simple.

## Decisión

- **`shell`** (público) monta **`ticket-checkout`** (único remoto de la demo) como **Web
  Component**, en **light DOM** (sin Shadow DOM): el bundle de `ticket-checkout` se sirve en
  `/ticket-checkout/` y se inyecta en tiempo de ejecución (no declarado en el `index.html` de
  `shell`, para que Vite no intente resolverlo como dependencia local — es el build de OTRO
  despliegue independiente).
- El nombre de fichero de salida de `ticket-checkout` es **fijo, sin hash** (`ticket-checkout.js`)
  porque `shell` necesita una ruta estable; no puede conocer un hash generado en el build de un
  proyecto que se despliega por separado.
- **Nginx** (`infra/nginx/nginx.conf`) hace de reverse proxy: cada app (`shell`,
  `ticket-checkout`, `venue-ops`) es su propio contenedor con su propio Nginx sirviendo sus
  estáticos; el Nginx "gateway" solo compone rutas y reenvía a los 3 backends + los hubs de
  SignalR.
- **`venue-ops`** no es un micro frontend de `shell`: es una app interna completamente
  independiente, en su propia ruta (`/venue-ops/`), sin necesidad de composición.

## Por qué no Module Federation

Con un único remoto, la complejidad operativa de Module Federation (versión de React como
singleton compartido, resolución de `remoteEntry`, plugin específico de Webpack/Vite) tiene
retorno decreciente frente al riesgo de fricción antes de la demo. Queda documentada como
alternativa **evaluada y conscientemente pospuesta**, no desconocida — con un roadmap claro
si el número de remotos creciera.

## Por qué no "shell esqueleto + un remoto por feature"

Evaluado posteriormente, al revisar la decisión con el usuario: que `shell` quedara solo como
esqueleto (cabecera) y cada feature — `EventCatalog`, `ticket-checkout`, `venue-ops` — fuera un
micro frontend remoto compuesto por él. Descartado por el mismo criterio de retorno decreciente
con el que se descartó Module Federation:

- **`EventCatalog` no es una unidad desplegable con audiencia propia**: es el contenido mismo de la
  landing de fans, solo cobra sentido dentro de `shell`. Partirlo pagaría el coste del patrón
  (bundle sin hash, inyección runtime, escopado CSS a mano en light DOM) sin cambiar nada del
  despliegue real, e invertiría el acoplamiento: hoy shell↔catálogo es composición normal de React;
  como remoto sería un protocolo de runtime (`CustomEvent`) entre dos apps que se sustituyen en la
  misma página — el fallo clásico de modularizar por encima de la unidad de despliegue.
- **`venue-ops` ya tiene su forma correcta como app independiente**: su separación es de
  audiencia/aislamiento (app interna), no de composición. Componerla en el shell público mezclaría
  audiencias (fan vs operador).
- El único remoto que aporta valor sigue siendo `ticket-checkout` (flujo independiente con
  audiencia clara, el caso que esta decisión eligió para demostrar el patrón). Si el número de
  remotos creciera de verdad, la alternativa multi-remoto queda como roadmap pendiente de
  re-evaluar (punto de entrada: Module Federation o composición con N remotos).

## Trade-off aceptado: sin Shadow DOM

Al vivir en light DOM, `ticket-checkout` no tiene aislamiento de CSS automático. Se mitiga
escopando manualmente todo su CSS bajo la clase `.ticket-checkout` (nunca toca `:root`, `body`
ni selectores de etiqueta globales) — un único convenio de estilos compartido entre apps en
vez de la complejidad de Shadow DOM, razonable a esta escala.
