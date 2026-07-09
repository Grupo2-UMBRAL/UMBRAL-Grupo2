# ERS Normalizado

Documento operativo derivado de [ers-umbral.md](ers-umbral.md) para consumo de agentes.

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
- `Scoring and Monitoring`
- `Identity and Access`

## Reglas de negocio transversales

- Una mision debe estar activa para usarse en sesiones.
- Una sesion no inicia sin equipos registrados.
- No se aceptan evidencias en sesion pausada, finalizada o cancelada.
- Las etapas progresan de forma lineal por equipo.
- El ranking se ordena por puntaje y desempata por tiempo de resolucion.
- El tiempo de resolucion se usa solo para desempate y auditoria; no modifica el puntaje.
- El desempate por tiempo de resolucion usa precision de medio segundo.
- Las penalizaciones deben registrar motivo y momento.
- Al finalizar la sesion se revelan pistas y soluciones.

## Decisiones aclaradas del ERS

### Mission Design para primer release

- El diseno reusable de una `Mission` se modela, para primer release, como un arbol ordenado de `Mission Nodes`.
- Solo los nodos hoja son `Mission Stages` jugables y participan en el flujo operativo de sesion.
- Cada `Mission Stage` define su propia `Difficulty` y esa dificultad alimenta el puntaje base cuando la etapa se resuelve.
- Un `Mission Node` compuesto no define `Difficulty` para scoring y la `Mission` no impone una dificultad unica a todas sus hojas.
- `Substage` es una relacion padre-hijo recursiva entre `Mission Nodes` dentro de la misma `Mission`.
- Cada `Mission Stage` hoja define un `Prompt` visible para el `Participant`. En `Trivia` funciona como pregunta o enunciado; en `Treasure Hunt` funciona como instruccion, objetivo o contexto de busqueda.
- `Hint` pertenece directamente a un `Mission Stage` hoja, no a nodos compuestos.
- Una `Mission` puede mezclar varios `Game Types`, pero cada `Mission Stage` tiene exactamente uno.
- Cada `Mission Stage` hoja define exactamente una `Difficulty`: `Easy`, `Medium` o `Hard`.
- Cuando un `Mission Node` compuesto representa un bloque tematico, sus descendientes jugables deben compartir el mismo `Game Type`.
- El flujo operativo se deriva aplanando los `Mission Stages` hoja en recorrido depth-first de izquierda a derecha segun el orden definido entre hermanos.
- Los nodos compuestos pueden definir `Default Time Budget` heredable por descendientes. Un valor mas especifico reemplaza por completo al heredado.
- `Stage Template Reuse` entra en alcance para reutilizar un `Mission Stage` o un subarbol de `Mission Nodes` dentro de otra `Mission`.
- El reuse copia el subarbol completo con sus descendientes, `Hints`, tiempos y metadata como copia independiente.
- La copia conserva referencia de trazabilidad visible al origen y deja evento auditable de reuse, pero no mantiene sincronizacion viva con el origen.
- El reuse puede tomar como origen cualquier nodo de una `Mission` visible para el `Administrator`; no requiere que la mision origen este activa.
- La experiencia del `Participant` sigue siendo lineal sobre la hoja actual y puede mostrar el bloque padre como contexto, pero no expone el arbol completo de diseno.
- El `Participant Stage View` debe mostrar el `Prompt` de la hoja actual antes de pedir evidencia, junto con el bloque padre si ese contexto ayuda.
- En sesion, el `Operator` puede desactivar una hoja individual o un nodo compuesto; en este ultimo caso, la desactivacion afecta solo las hojas descendientes que sigan pendientes.

### Trivia

- La validacion de respuestas de `Trivia` ocurre automaticamente por defecto.
- El `Prompt` de una hoja `Trivia` es visible para el `Participant` y contiene la pregunta o enunciado que justifica la respuesta evaluada.
- El `Operator` puede corregir el resultado cuando detecte que la respuesta enviada corresponde a una alternativa valida.
- No debe modelarse `Trivia` como un flujo donde toda evidencia entra primero en estado `Pending` para revision humana obligatoria.

### Treasure Hunt

- El `Prompt` de una hoja `Treasure Hunt` es visible para el `Participant` y contiene la instruccion, objetivo o contexto que orienta la busqueda previa al escaneo del QR.
- El `Prompt` no reemplaza la validacion por `ExpectedQrHash`; solo explica que debe resolver el equipo antes de enviar evidencia.

### Scoring and penalties

- El puntaje base por evidencia validada depende de la `Difficulty` del `Mission Stage`.
- Para primer release, la tabla base de puntaje es `Easy = 100`, `Medium = 200`, `Hard = 300`.
- `Scoreboard` es la fuente de verdad del puntaje acumulado por `LiveSession`.
- Cada cambio efectivo de puntaje queda explicado por uno o mas `Score Entries`.
- La misma tabla base aplica para `Treasure Hunt` y `Trivia`.
- Un `Mission Stage` resuelto otorga `0` o el puntaje completo que le corresponda; no existe `partial credit` en el primer release.
- El instante oficial para medir `Resolution Time` es la recepcion del envio en backend.
- La latencia y los tiempos de conexion se registran para auditoria, pero no corrigen el desempate con una formula compensatoria.
- Si dos equipos empatan en puntaje y tambien en `Resolution Time` con precision de medio segundo, el `Ranking` mantiene el empate y no aplica un tercer criterio oculto.
- El `Operator` aplica penalizaciones eligiendo una severidad predefinida, no ingresando un descuento libre de puntos.
- Para primer release, las severidades de `Penalty` son `Minor = -50`, `Major = -100`, `Critical = -200`.
- Cuando un `Validation Override` confirma como valida una evidencia ambigua de `Trivia`, la etapa otorga el puntaje completo que corresponda segun la `Difficulty` del `Mission Stage`.
- Una `Evidence Submission` invalida no genera penalizacion automatica por si misma.
- El descuento de puntaje ocurre solo cuando el `Operator` aplica una `Penalty` explicita con severidad y motivo auditables.
- Un mismo `Session Team` puede recibir multiples `Penalty` dentro de una misma `LiveSession` cuando correspondan a hechos distintos.
- El sistema debe bloquear duplicados tecnicos del mismo comando de penalizacion para evitar doble aplicacion accidental por reenvio o doble accion del operador.
- El puntaje acumulado visible de un `Session Team` no puede quedar por debajo de `0`, aunque la penalizacion completa siga quedando registrada en auditoria y trazabilidad.
- Un `Session Team` solo puede obtener puntaje positivo una vez por cada `Mission Stage` resuelto dentro de una `LiveSession`.
- Los reintentos fallidos no generan puntaje por si mismos y una correccion manual del `Operator` no debe duplicar puntaje positivo sobre la misma hoja.
- Cada intento, correccion manual, penalizacion y cambio efectivo de puntaje debe quedar reflejado en auditoria.

## Uso esperado

Este archivo resume el ERS para trabajo diario. El documento academico completo sigue siendo la referencia funcional detallada.
