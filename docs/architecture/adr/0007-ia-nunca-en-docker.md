# ADR 0007 — Garantía de que `.ia/` nunca entra en una imagen Docker

## Contexto

`.ia/` es la metodología personal de trabajo con IA del autor — vive en su copia local del
repositorio (que no se entrega a Sportian: solo se enseña en pantalla durante la entrevista),
pero el **artefacto Docker** sí se entrega o se ejecuta ante terceros. Hace falta una garantía
técnica, no solo una promesa, de que esa carpeta nunca puede acabar en una imagen.

## Decisión — defensa en tres capas independientes

1. **`.dockerignore`** en la raíz excluye explícitamente `.ia/`, `.claude/`, `.git/`, `docs/`,
   `infra/deploy/`, `**/node_modules/`, etc.
2. **Ningún Dockerfile hace `COPY . .`** desde la raíz del repo. Cada uno copia solo rutas
   explícitas (`services/Core/**`, `services/<Servicio>/**`, `ui/<app>/**`) — así,
   aunque el `.dockerignore` tuviera un fallo, `.ia` no puede entrar porque nunca se referencia
   por ruta en ningún `COPY`.
3. **Guard automatizado en CI** (`docker-build.yml`): tras construir cada una de las 6
   imágenes, `docker save <imagen> | tar -tv | grep -i "\.ia"` falla el pipeline si aparece
   algo — convierte la garantía en una aserción verificada en cada push, no en una promesa de
   diseño.

## Por qué tres capas y no una

Cada capa cubre el fallo de la anterior: si alguien añadiera un `COPY . .` por error, la capa 1
(`.dockerignore`) seguiría protegiendo; si el `.dockerignore` se editara mal, la disciplina de
`COPY` explícito (capa 2) haría el fallo imposible salvo error humano directo en el Dockerfile;
y el guard de CI (capa 3) detectaría ese último escenario antes de que la imagen se publique.
