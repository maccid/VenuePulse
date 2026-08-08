# Git flow — VenuePulse

## Ramas

GitFlow simplificado: `main` (estable, tageada por versión) ← `develop` (integración) ←
`feature/*` / `fix/*` (una por cambio lógico).

- `main`: solo recibe merges `--no-ff` desde `develop`, con tag de versión (`vX.Y.Z`).
- `develop`: rama de integración. Todo el trabajo pasa por aquí antes de `main`.
- `feature/<algo>` / `fix/<algo>`: nombre corto y descriptivo del cambio, no del ticket.

## Flujo por tarea

```bash
git checkout develop
git checkout -b fix/mi-cambio
# trabajar, commitear
git checkout develop
git merge --no-ff fix/mi-cambio -m "merge: fix/mi-cambio -> develop"
```

Para trabajar varias tareas en paralelo sin bloquear la copia principal ni pisarte cambios entre
ramas, usar `git worktree` en vez de cambiar de rama en la misma carpeta. El repo vive en la
carpeta `venuepulse/` y los worktrees viven **fuera del repo**, como hermanos de esa carpeta
(patrón `venuepulse-<rama>`). En VSCode se ven todos a través del workspace multi-root
`venuepulse.code-workspace` de la carpeta contenedora — fichero **local, no versionado** — donde
cada root tiene su propia rama y Source Control:

```bash
git worktree add ../venuepulse-fix-mi-cambio fix/mi-cambio
cd ../venuepulse-fix-mi-cambio
# trabajar aquí; la copia principal en venuepulse sigue intacta en su rama
```

Al crear o eliminar un worktree, añadir o quitar su carpeta (`../venuepulse-<rama>`) en
`venuepulse.code-workspace`. Limpieza:

```bash
git worktree remove ../venuepulse-fix-mi-cambio
git branch -d fix/mi-cambio   # tras el merge
```

## Commits

- **Mensajes de commit en inglés.**
- Formato `tipo(ámbito): resumen corto` (`fix`, `feat`, `refactor`, `docs`, `ci`, `test`).
- Cuerpo del commit explicando el PORQUÉ, no solo el qué — especialmente en fixes no obvios.
- Varias tareas lógicas = varios commits, nunca todo aplastado en uno.
- No hacer commit a menos que se pida explícitamente.
- Nunca `git commit --amend` salvo petición explícita; nunca reescribir historial publicado.

## Antes de mergear a `develop`

- Tests en verde (`dotnet test services/VenuePulse.slnx`).
- Si el cambio toca `docker-compose.yml`/Dockerfiles: verificar que `docker compose up --build`
  sigue levantando los 11 contenedores.
