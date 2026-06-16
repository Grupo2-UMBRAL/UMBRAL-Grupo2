# Operating Model

Modelo operativo comun para agentes que trabajen en este repo.

## Fuentes de verdad

Leer en este orden:

1. [../product/ers.md](../product/ers.md)
2. [../../CONTEXT-MAP.md](../../CONTEXT-MAP.md)
3. [../architecture/repo-structure.md](../architecture/repo-structure.md)
4. `src/*/CONTEXT.md` segun el bounded context afectado
5. `docs/architecture/adr/` cuando exista una decision relevante

## Reglas

- Usar el vocabulario exacto de los `CONTEXT.md`.
- Si el ERS contradice un `CONTEXT.md`, reportar la contradiccion antes de proponer codigo.
- No asumir carpetas de implementacion que no existan todavia.
- Diferenciar documentacion de dominio, documentacion operativa y skills.
- Si falta una decision de arquitectura dificil de revertir, proponer un ADR.

## Flujo de contribucion

- Tratar `main` como rama protegida.
- Hacer desarrollo solo en ramas `feature/*`, `fix/*` o equivalentes por cambio aislado.
- Cuando haya multiples agentes trabajando desde tickets, asignar un unico orquestador como autoridad de Linear y `git`.
- Cada ticket activo debe vivir en su propia branch y su propio `git worktree`.
- Incluir siempre el identificador de Linear en la branch: `<tipo>/<issue-id>-<slug>`.
- Mantener commits atomicos: un solo cambio coherente por commit.
- Usar mensajes `Conventional Commits` con referencia de Linear en el encabezado cuando aplique: `feat(LIN-123): mensaje`, `fix(LIN-123): mensaje`.
- Si el cambio va a abrir pull request, enlazar el commit con un magic word no cerrador en el footer: `Refs LIN-123`.
- Si el cambio se integrara sin PR por una excepcion explicita, usar un magic word cerrador en el commit final: `Fixes LIN-123`.
- Preferir `rebase` para mantener historial lineal y reducir ruido de merges intermedios.
- Antes de integrar una branch de ticket, hacer `fetch` de la rama de integracion remota y `rebase` de la branch del ticket sobre esa referencia; evitar mezclar primero cambios en `develop` local salvo necesidad explicita.
- Mantener pull requests pequenos. La referencia objetivo es un maximo de `300` lineas cambiadas de codigo productivo por PR. Si el cambio real necesita mas, dividirlo por slices verticales o justificar la excepcion.
- Incluir en cada PR:
  - issue ID de Linear en el titulo
  - magic word + issue ID en el cuerpo para el ticket principal, por defecto `Fixes LIN-123`
  - `Refs <issue-id>` para tickets secundarios cuando un PR toca mas de un issue
  - descripcion tecnica
  - criterios de aceptacion
  - evidencia de pruebas ejecutadas

## Checklist minimo antes de cerrar un cambio

- El codigo compila o, si el repo aun no compila end-to-end, el cambio no introduce una nueva rotura conocida en el alcance tocado.
- Los tests del alcance tocado pasan, o se deja explicitado por que aun no existen o por que no pudieron ejecutarse.
- El codigo backend supero la prueba de mutacion antes de ser considerado "done". (El frontend se valida via Playwright).
- No hay credenciales, secretos ni datos sensibles en codigo, commits ni artefactos de prueba.
- Los nombres reflejan el lenguaje ubicuo del bounded context afectado.
- La documentacion afectada queda actualizada cuando cambia una decision, contrato o flujo operativo.

## Estandares de codigo

- Nombrar tipos, funciones, variables y eventos con lenguaje de dominio. Los nombres deben revelar intencion y evitar abreviaturas ambiguas.
- Mantener funciones pequenas, con responsabilidad unica y un solo nivel principal de abstraccion por bloque.
- Preferir guard clauses y early returns para evitar indentacion piramidal y reducir complejidad cognitiva.
- Evitar metodos dios, parametros booleanos sin contexto y retornos `null` silenciosos.
- Separar logica de decision de efectos secundarios como IO, red, filesystem, hora del sistema o base de datos.
- Introducir dependencias mediante inyeccion de dependencias o puertos explicitos cuando el componente necesite colaboraciones externas.
- No leer tiempo, archivos, red ni estado global directamente desde la logica de negocio si esa decision puede abstraerse con una interfaz o puerto.
- Validar precondiciones al inicio, preservar invariantes durante la operacion y comprobar postcondiciones cuando aplique.
- Distinguir errores de dominio de errores tecnicos. Los mensajes deben ser claros, accionables y tipados cuando el lenguaje lo permita.
- Disenar codigo robusto frente a colecciones vacias, overflow, entradas invalidas y estados imposibles.

## Como deben operar los agentes

- Antes de proponer o escribir codigo, leer los ADRs aplicables del area tocada.
- Si un cambio viola estas reglas por necesidad tecnica, explicitar el trade-off y dejarlo documentado en el cambio o en un ADR.
- Cuando una regla sea repetible como procedimiento, apoyarse en skills; cuando sea una decision dificil de revertir, apoyarse en ADRs.
- Cuando haya que elegir o ejecutar validacion local, usar `.agents/skills/local-validation/` para preferir los scripts reproducibles del repo sobre comandos armados ad hoc.
- Durante el loop de implementacion, correr la validacion mas angosta que pruebe el area tocada; reservar la validacion completa para un solo gate final antes de merge o cierre tecnico.
- Para el desarrollo backend, operar con TDD estricto (Red-Green-Refactor) un test a la vez. No escribir codigo de produccion sin un test en rojo. Validar la calidad final con pruebas de mutacion.
- Los agentes ejecutores no deben crear ramas, mezclar tickets en un mismo workspace ni mover estados criticos del tracker sin pasar por el orquestador.
- En reviews, priorizar findings sobre testabilidad, fronteras de IO, manejo de errores, coherencia con lenguaje ubicuo y tamano del cambio.
- Si el trabajo entra en loops de `docker compose`, preferir `ps`, `config`, logs acotados por servicio y archivos en `.worktrees/_runtime/` sobre streams largos en chat; para eso usar `.agents/skills/docker-compose-context-hygiene/`.

## Estado actual del repo

- `src/` contiene el mapa de contextos del proyecto y su lenguaje.
- `.agents/` contains agents, skills, and runtime utilities.
- `.agents/agents/` contains agentes esperados para la construccion del proyecto.
- El repo todavia no define la estructura final de implementacion del monorepo.
