# Orchestrator

Modelo operativo para un agente orquestador que ejecuta trabajo desde tickets de Linear sin mezclar permisos ni ramas.

## Objetivo

- Tomar tickets listos desde Linear.
- Ser la unica autoridad para cambios de estado en Linear y operaciones globales de `git`.
- Aislar cada ticket en su propia branch y `git worktree`.
- Delegar implementacion, review o validacion a otros agentes con permisos minimos.

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

1. Leer ticket de Linear y verificar que tenga contexto suficiente.
2. Si falta especificacion, mover a `needs-info` o `needs-triage`.
3. Si esta listo, marcar `ready-for-agent` como tomado por el orquestador.
4. Elegir prefijo de branch:
   - `feature/` para capacidad nueva
   - `fix/` para correccion
   - `chore/` para trabajo tecnico sin cambio funcional
5. Crear branch por ticket con formato `<tipo>/<issue-id>-<slug>`.
6. Crear `git worktree` dedicado con formato `../wt-<issue-id>`.
7. Inyectar al worker:
   - ID del ticket
   - criterios de aceptacion
   - bounded context afectado
   - ADRs relevantes
   - restricciones de permisos
8. Recibir resultado, correr validacion y review del alcance.
9. Actualizar Linear con evidencia tecnica:
   - branch
   - pruebas ejecutadas
   - riesgos abiertos
   - decision siguiente
10. Esperar revision humana sobre la branch terminada antes de consolidar historial.
11. Si el humano aprueba, hacer `squash` a un solo commit final en la branch del ticket.
12. Abrir PR o fusionar hacia la rama de integracion objetivo solo despues de esa aprobacion explicita.
13. Tras merge o cierre, limpiar `worktree` y branch local.

## Convenciones de branch y worktree

Ejemplos:

- `feature/LIN-123-session-template-publication`
- `fix/LIN-241-refresh-token-expiry`
- `chore/LIN-310-compose-bootstrap`

Comandos de referencia:

```powershell
git worktree add ../wt-LIN-123 -b feature/LIN-123-session-template-publication main
git -C ../wt-LIN-123 status --short
git worktree remove ../wt-LIN-123
```

Usar un `worktree` por ticket evita contaminacion entre agentes y permite ejecutar varios tickets en paralelo.

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
