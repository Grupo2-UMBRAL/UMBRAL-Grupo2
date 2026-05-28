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
- Mantener commits atomicos: un solo cambio coherente por commit.
- Usar mensajes `Conventional Commits` con referencia de tarea cuando exista: `feat[#Tarea]: mensaje`, `fix[#Tarea]: mensaje`.
- Preferir `rebase` para mantener historial lineal y reducir ruido de merges intermedios.
- Mantener pull requests pequenos. La referencia objetivo es un maximo de `300` lineas cambiadas de codigo productivo por PR. Si el cambio real necesita mas, dividirlo por slices verticales o justificar la excepcion.
- Incluir en cada PR:
  - descripcion tecnica
  - criterios de aceptacion
  - evidencia de pruebas ejecutadas

## Checklist minimo antes de cerrar un cambio

- El codigo compila o, si el repo aun no compila end-to-end, el cambio no introduce una nueva rotura conocida en el alcance tocado.
- Los tests del alcance tocado pasan, o se deja explicitado por que aun no existen o por que no pudieron ejecutarse.
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
- En reviews, priorizar findings sobre testabilidad, fronteras de IO, manejo de errores, coherencia con lenguaje ubicuo y tamano del cambio.

## Estado actual del repo

- `src/` contiene el mapa de contextos del proyecto y su lenguaje.
- `.agents/` contains agents, skills, and runtime utilities.
- `.agents/agents/` contains agentes esperados para la construccion del proyecto.
- El repo todavia no define la estructura final de implementacion del monorepo.
