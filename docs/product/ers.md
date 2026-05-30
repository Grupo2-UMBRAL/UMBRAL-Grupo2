# ERS Normalizado

Documento operativo derivado de [../../README/ERS UMBRAL .md](../../README/ERS%20UMBRAL%20.md) para consumo de agentes.

## Proposito

UMBRAL es una plataforma para operar experiencias narrativas inmersivas en tiempo real con dos tipos de juego:

- `Treasure Hunt`
- `Trivia`

Existen dos superficies principales:

- web para `Administrator` y `Operator`
- movil para `Participant`

## Decisiones estables del enunciado

- Arquitectura limpia o hexagonal.
- Separacion CQRS implementada con `MediatR`.
- `CQRS` se aplica a nivel logico de comandos y consultas, sin separar bases de datos de escritura y lectura.
- Persistencia relacional.
- Comunicacion en tiempo real implementada con `SignalR`.
- Mensajeria asincrona.
- Roles `Administrator`, `Operator` y `Participant`.
- Identidad y acceso gestionados con `Keycloak`.
- Autenticacion basada en tokens `JWT` emitidos por `Keycloak`.
- Autorizacion basada en roles y permisos definidos en `Keycloak` y consumidos desde los claims del token.
- La experiencia del participante ocurre en movil.
- El diseno reusable de la mision esta separado de la operacion en vivo.

## Bounded contexts esperados

- `Mission Design`
- `Session Operations`
- `Scoring and Audit`
- `Identity and Access`

## Reglas de negocio transversales

- Una mision debe estar activa para usarse en sesiones.
- Una sesion no inicia sin equipos registrados.
- No se aceptan evidencias en sesion pausada, finalizada o cancelada.
- Las etapas progresan de forma lineal por equipo.
- El ranking se ordena por puntaje y desempata por tiempo de resolucion.
- Las penalizaciones deben registrar motivo y momento.
- Al finalizar la sesion se revelan pistas y soluciones.

## Decisiones aclaradas del ERS

### Mission Design para primer release

- El diseno reusable de una `Mission` se modela, para primer release, como un arbol ordenado de `Mission Nodes`.
- Solo los nodos hoja son `Mission Stages` jugables y participan en el flujo operativo de sesion.
- `Substage` es una relacion padre-hijo recursiva entre `Mission Nodes` dentro de la misma `Mission`.
- `Hint` pertenece directamente a un `Mission Stage` hoja, no a nodos compuestos.
- Una `Mission` puede mezclar varios `Game Types`, pero cada `Mission Stage` tiene exactamente uno.
- Cuando un `Mission Node` compuesto representa un bloque tematico, sus descendientes jugables deben compartir el mismo `Game Type`.
- El flujo operativo se deriva aplanando los `Mission Stages` hoja en recorrido depth-first de izquierda a derecha segun el orden definido entre hermanos.
- Los nodos compuestos pueden definir `Default Time Budget` heredable por descendientes. Un valor mas especifico reemplaza por completo al heredado.
- `Stage Template Reuse` entra en alcance para reutilizar un `Mission Stage` o un subarbol de `Mission Nodes` dentro de otra `Mission`.
- El reuse copia el subarbol completo con sus descendientes, `Hints`, tiempos y metadata como copia independiente.
- La copia conserva referencia de trazabilidad visible al origen y deja evento auditable de reuse, pero no mantiene sincronizacion viva con el origen.
- El reuse puede tomar como origen cualquier nodo de una `Mission` visible para el `Administrator`; no requiere que la mision origen este activa.
- La experiencia del `Participant` sigue siendo lineal sobre la hoja actual y puede mostrar el bloque padre como contexto, pero no expone el arbol completo de diseno.
- En sesion, el `Operator` puede desactivar una hoja individual o un nodo compuesto; en este ultimo caso, la desactivacion afecta solo las hojas descendientes que sigan pendientes.

### Trivia

- La validacion de respuestas de `Trivia` ocurre automaticamente por defecto.
- El `Operator` puede corregir el resultado cuando detecte que la respuesta enviada corresponde a una alternativa valida.
- No debe modelarse `Trivia` como un flujo donde toda evidencia entra primero en estado `Pending` para revision humana obligatoria.

## Uso esperado

Este archivo resume el ERS para trabajo diario. El documento academico completo sigue siendo la referencia funcional detallada.
