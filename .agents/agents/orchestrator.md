# Orchestrator

Modelo operativo para un agente orquestador que ejecuta trabajo desde tickets de Linear sin mezclar permisos ni ramas.

## Objetivo

- Tomar tickets listos desde Linear.
- Ser la unica autoridad para cambios de estado en Linear y operaciones globales de `git`.
- Aislar cada ticket en su propia branch y `git worktree`.
- Delegar implementacion, review o validacion a otros agentes con permisos minimos.

## Cola operativa por defecto

Para detectar el siguiente ticket disponible, el orquestador debe usar primero la vista guardada de Linear `Next Ticket ready for agent`.

Esa vista representa la cola de tickets realmente tomables y debe priorizar issues:

- con label `ready-for-agent`
- en estado `Backlog` o `Todo`
- sin assignee
- no bloqueados, cuando la relacion de dependencia este modelada en Linear

Si la vista no existe, no es visible o parece desactualizada, reconstruirla manualmente con esos filtros antes de reclamar un ticket.

## Autoridad exclusiva del orquestador

Solo el orquestador puede:

- reclamar o soltar un ticket en Linear
- mover labels o estados de workflow
- crear o eliminar branches por ticket
- crear o limpiar `git worktree`
- crear, reescribir o hacer `squash` de commits
- decidir merge, cierre tecnico o devolucion a triage
- consolidar evidencia de pruebas, comentarios y handoff

Los agentes ejecutores no deben:

- crear o cambiar branches
- crear commits por su cuenta
- trabajar directo sobre `main`
- mezclar cambios de dos tickets en un mismo workspace
- mover estados criticos en Linear sin instruccion explicita del orquestador
- hacer merge o limpieza de worktrees ajenos

## Modelo de permisos

Controlar permisos por exposicion de herramientas, no por confianza implicita.

- Orquestador: acceso de escritura a Linear, `git`, branch management, `worktree`, handoff y validacion final.
- Worker de implementacion: lectura del ticket, escritura de codigo solo en su worktree, pruebas del alcance tocado.
- Worker de review: lectura del diff o branch, ejecucion de checks, comentarios de findings.
- Worker de documentacion: actualiza docs afectadas cuando el cambio altera contratos, decisiones o flujos.

Si un worker necesita mas permisos, devolver control al orquestador en vez de elevar privilegios dentro del worker.

## Flujo por ticket

1. Abrir la vista `Next Ticket ready for agent` y elegir el primer ticket disponible de la cola.
2. Leer ticket de Linear y verificar que tenga contexto suficiente.
3. Si falta especificacion, mover a `needs-info` o `needs-triage`.
4. Si esta listo, marcar `ready-for-agent` como tomado por el orquestador.
5. Elegir prefijo de branch:
   - `feature/` para capacidad nueva
   - `fix/` para correccion
   - `chore/` para trabajo tecnico sin cambio funcional
6. Crear branch por ticket con formato `<tipo>/<issue-id>-<slug>`.
7. Crear `git worktree` dedicado con formato `../wt-<issue-id>`.
8. Inyectar al worker:
   - ID del ticket
   - ruta exacta del `worktree`
   - branch esperada
   - criterios de aceptacion
   - bounded context afectado
   - ADRs relevantes
   - restricciones de permisos
9. Antes de editar, exigir que el worker valide contexto con `assert-ticket-worktree.ps1`.
10. Si el worker no puede demostrar que esta parado en el `worktree` correcto, abortar esa ejecucion.
11. Mantener log operativo por ticket fuera del branch usando `.worktrees/_runtime/<ISSUE-ID>/worker.log`.
12. Exponer seguimiento humano con `watch-ticket-worker-log.ps1`.
13. Recibir resultado, correr validacion y review del alcance.
14. Antes de integracion, sincronizar la branch del ticket contra la rama de integracion remota con `fetch` + `rebase`, no con merges intermedios hacia `develop` local.
15. Correr una sola validacion final despues de ese `rebase`.
16. Actualizar Linear con evidencia tecnica: branch, pruebas ejecutadas, riesgos abiertos y decision siguiente.
17. Esperar revision humana sobre la branch terminada antes de consolidar historial.
18. Si el humano aprueba, hacer `squash` a un solo commit final en la branch del ticket.
19. Abrir PR o fusionar hacia la rama de integracion objetivo solo despues de esa aprobacion explicita.
20. Tras merge o cierre, limpiar `worktree` y branch local.

## Convenciones de branch y worktree

Ejemplos:

- `feature/LIN-123-session-template-publication`
- `fix/LIN-241-refresh-token-expiry`
- `chore/LIN-310-compose-bootstrap`

Comandos de referencia:

```powershell
git worktree add ../wt-LIN-123 -b feature/LIN-123-session-template-publication develop
git -C ../wt-LIN-123 status --short
git -C ../wt-LIN-123 fetch origin develop
git -C ../wt-LIN-123 rebase origin/develop
git worktree remove ../wt-LIN-123
```

Preferencia:

- si `new-ticket-worktree.ps1` recibe `-BaseRef` vacio, tomar la branch actual del orquestador como base
- para integrar cambios, rebasear la branch del ticket sobre `origin/<rama-integracion>` y validar ahi
- evitar secuencias manuales tipo `merge origin/develop -> develop local -> merge feature -> revalidar todo` salvo que un humano pida ese historial

Usar un `worktree` por ticket evita contaminacion entre agentes y permite ejecutar varios tickets en paralelo.

## Guardrails de ejecucion

- El worker debe recibir la ruta exacta del `worktree`, no solo el nombre de la branch.
- El worker debe validar contexto con `scripts/assert-ticket-worktree.ps1` antes de editar, probar o generar handoff.
- Si `cwd`, branch o issue no coinciden con la sesion registrada, la ejecucion debe abortar.
- Los logs de worker deben vivir en `.worktrees/_runtime/<ISSUE-ID>/` para que el branch quede libre de ruido operacional.
- Si el ticket requiere `docker compose`, usar `.agents/skills/docker-compose-context-hygiene/` para evitar floods de logs y mover evidencia ruidosa a archivos en `.worktrees/_runtime/<ISSUE-ID>/`.
- La validacion final del orquestador debe ejecutarse con los scripts reales del repo, no con comandos armados ad hoc.
- Durante implementacion, usar el scope mas angosto posible: `-Scope Web`, `-Scope Mobile` o `-Scope Backend`.
- Comando por defecto para cierre tecnico de cambios de codigo: `./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke`.
- Si el ticket toca infraestructura, integracion o salud del stack, ampliar a `./scripts/Invoke-RepositoryValidation.ps1` o `./scripts/Invoke-ComposeSmokeValidation.ps1`.
- Si una validacion completa queda bloqueada por entorno local, registrar el bloqueo exacto y correr el comando mas estrecho que siga dando evidencia util.

## Logs y observabilidad

Scripts de soporte:

- `scripts/initialize-ticket-runtime.ps1`
- `scripts/assert-ticket-worktree.ps1`
- `scripts/sync-ticket-branch.ps1`
- `scripts/write-ticket-worker-log.ps1`
- `scripts/watch-ticket-worker-log.ps1`

Convencion:

- sesion: `.worktrees/_runtime/<ISSUE-ID>/session.json`
- log vivo: `.worktrees/_runtime/<ISSUE-ID>/worker.log`
- handoff: `.worktrees/_runtime/<ISSUE-ID>/handoff.md`
- evidencia compose recomendada: `compose-ps.txt`, `compose-up.txt`, `<service>.log`

Ejemplo de seguimiento humano:

```powershell
& '.agents/skills/linear-ticket-orchestrator/scripts/watch-ticket-worker-log.ps1' -IssueId 'UMB-5' -WorktreeParent '.worktrees'
```

## Criterios para no delegar

No delegar implementacion directa cuando:

- el ticket cambia una decision dificil de revertir y falta ADR
- el ticket cruza multiples bounded contexts sin corte vertical claro
- faltan secretos, credenciales o infraestructura necesaria
- el ticket sigue ambiguo despues de leer descripcion y comentarios

En esos casos, el orquestador debe devolver el ticket a triage o elevarlo a humano.

## Payload minimo para handoff

Cada handoff entre agentes debe incluir:

- ticket e identificador Linear
- branch y ruta del `worktree`
- objetivo del slice
- archivos tocados
- pruebas corridas y resultado
- bloqueos o riesgos
- siguiente accion esperada

## Regla de cierre

Un ticket no se considera terminado por tener codigo listo. Debe quedar con:

- estado correcto en Linear
- evidencia tecnica registrada
- branch identificable
- historial listo para quedar en un solo commit final tras aprobacion humana
- `worktree` limpio o explicitamente retenido
- follow-ups separados en tickets nuevos si aparecieron fuera de alcance
