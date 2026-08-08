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
ramas, usar `git worktree` en vez de cambiar de rama en la misma carpeta. Los worktrees se crean
**dentro del repo** en `worktrees/<rama>` (carpeta gitignoreada) para que VSCode los muestre:

```bash
git worktree add worktrees/venuepulse-fix-mi-cambio fix/mi-cambio
cd worktrees/venuepulse-fix-mi-cambio
# trabajar aquí; la copia principal en sportian.workspace sigue intacta en su rama
```

Cada worktree aparece como raíz propia en `sportian.workspace.code-workspace` (multi-root), así
VSCode muestra su rama y Source Control independientes. Al crear o eliminar un worktree, añadir o
quitar su carpeta en ese fichero. Limpieza:

```bash
git worktree remove worktrees/venuepulse-fix-mi-cambio
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
