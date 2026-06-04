# Session Operations

Contexto responsable de la ejecucion en vivo de una mision para equipos concretos. Aqui viven el estado operativo de la sesion, el flujo efectivo de etapas y la interaccion en tiempo real con operadores y participantes.

## Language

**LiveSession**:
Ejecucion en vivo de una **Mission** para equipos concretos. Una **LiveSession** es el agregado principal de este contexto y contiene las invariantes operativas sobre estado, participantes y progreso.
_Avoid_: Mission, plantilla

**Session Join Code**:
Codigo entregado por el operador para entrar a una **LiveSession**. El **Session Join Code** identifica la sesion de destino, pero no define por si mismo el equipo dentro de esa sesion.
_Avoid_: access token, team code

**Session Enrollment**:
Capacidad interna de **Session Operations** que gobierna entrada a una **LiveSession**, creacion o union a un **Session Team** y reglas del **Team Assignment Window**. Sus cambios entran por el agregado **LiveSession**.
_Avoid_: live progression, scoring, authentication internals

**Session Lifecycle**:
Capacidad interna de **Session Operations** que gobierna los estados globales de una **LiveSession** y sus transiciones validas.
_Avoid_: per-team progression, scoring update

**Session Team**:
Grupo creado o elegido por participantes dentro de una **LiveSession**. Un **Session Team** existe solo dentro de esa sesion y se modela como parte de la consistencia del agregado **LiveSession**.
_Avoid_: team global, user

**Session Stage Flow**:
Secuencia efectiva de etapas que una **LiveSession** ejecuta. Se deriva aplanando los **Mission Stages** hoja del arbol de **Mission Nodes** en recorrido depth-first de izquierda a derecha y puede cambiar por desactivaciones propias de la sesion. Conserva los datos operativos necesarios de cada hoja, incluido su **Prompt** y su **Difficulty**, para que la experiencia del participante y el scoring de esa ejecucion no dependan de ediciones posteriores de la **Mission** reusable.
_Avoid_: Mission Stage, flujo base

**Session Progression**:
Capacidad interna de **Session Operations** que gobierna la etapa actual de cada **Session Team**, la aceptacion operativa de evidencias y el avance por el **Session Stage Flow**.
_Avoid_: global session state, ranking projection

**Per-Team Progression**:
Regla por la que cada **Session Team** avanza por su propia etapa actual sin bloquear ni desbloquear el avance de otros equipos. El progreso de un equipo no reclama una etapa para el resto.
_Avoid_: exclusive claim, first-team-wins stage lock

**Team Participation**:
Vinculacion entre un participante autenticado y un **Session Team** dentro de una **LiveSession**. Expresa en que grupo juega ese participante durante esa ejecucion.
_Avoid_: membership global, user account only

**Team Assignment Window**:
Periodo previo al inicio efectivo del juego en el que un participante puede crear o cambiar su **Session Team** dentro de una **LiveSession**. Despues de ese punto, cualquier correccion de asignacion es una intervencion excepcional del operador.
_Avoid_: open-ended switching, runtime free reassignment

**Evidence Submission**:
Intento de un **Session Team** por resolver su etapa actual dentro de una **LiveSession**. Session Operations decide si el envio es aceptable dentro del flujo activo antes de que otras capacidades lo usen.
_Avoid_: ScoreEntry, audit event

**Validation Override**:
Intervencion manual del **Operator** para corregir el resultado de una **Evidence Submission** ambigua dentro de una **LiveSession**. La correccion cambia el resultado operativo final, debe quedar auditada y, si confirma una respuesta valida, habilita el puntaje completo de la hoja sin duplicar credito positivo para la misma etapa.
_Avoid_: nuevo intento disfrazado, doble premio por la misma etapa, cambio silencioso sin trazabilidad

**Validation Outcome**:
Resultado de evaluar una **Evidence Submission** dentro de una **LiveSession**. Puede resolverse automaticamente por regla o requerir intervencion manual del operador en casos ambiguos.
_Avoid_: score entry, audit-only status

**Validation Override**:
Correccion manual del **Operator** sobre un **Validation Outcome** de Trivia cuando una respuesta ambigua o inicialmente rechazada corresponde a una alternativa valida. Puede habilitar el credito completo de la hoja una sola vez, pero no crea credito positivo duplicado.
_Avoid_: revision humana obligatoria para toda respuesta Trivia, puntaje parcial, segundo credito por la misma hoja

**Resolution Time**:
Tiempo oficial de resolucion usado para desempatar **Ranking** y para auditoria. Se mide desde la recepcion del envio en backend con precision oficial de 500 ms. No modifica puntaje ni aplica compensacion por latencia.
_Avoid_: bonus por velocidad, compensacion de conexion, tercer criterio oculto

**Session State**:
Estado operativo de una **LiveSession**. Controla si la sesion admite avances, evidencias y acciones del operador.
_Avoid_: mission status, connection status

**Hint Release**:
Capacidad operativa que controla que **Hints** quedan visibles para cada **Session Team** durante una **LiveSession**. Incluye liberacion manual, liberacion por regla y revelacion final de soluciones.
_Avoid_: mission design hint authoring, generic notification

**Penalty Application**:
Accion operativa por la que el **Operator** sanciona a un **Session Team** dentro de una **LiveSession** eligiendo una severidad predefinida y registrando el motivo. Esta accion origina una **Penalty** para `Scoring and Audit`, pero no decide por si misma el recalculo interno del **Scoreboard**.
_Avoid_: descuento libre de puntos como decision operativa, recalculo de ranking dentro de Session Operations

**Penalty Command Idempotency**:
Proteccion tecnica que evita aplicar dos veces la misma **Penalty Application** cuando el mismo comando se reenvia o el operador dispara accidentalmente la misma accion duplicada. No fusiona penalizaciones de negocio realmente distintas.
_Avoid_: doble descuento accidental, deduplicar hechos distintos, esconder sanciones reales

**Invalid Attempt**:
Resultado operativo de una **Evidence Submission** rechazada dentro de una **LiveSession**. Queda auditado como intento invalido, pero no descuenta puntaje por si mismo sin una **Penalty Application** explicita del **Operator**.
_Avoid_: penalizacion automatica implicita, confundir rechazo de evidencia con sancion, descuento silencioso

**Session Flow Deactivation**:
Capacidad operativa de desactivar etapas pendientes dentro del **Session Stage Flow** para una **LiveSession** concreta. Puede apuntar a un **Mission Stage** hoja individual o a un **Mission Node** compuesto, caso en el que la desactivacion aplica a todas sus hojas descendientes que sigan pendientes. Las hojas ya completadas conservan su historial y no se reescriben.
_Avoid_: editar la Mission base, borrar historial ya ejecutado, desactivar solo el nodo visual sin efecto en hojas

**Participant Stage View**:
Vista operativa que recibe un **Session Team** participante durante la **LiveSession**. Muestra la hoja jugable actual, su **Prompt** visible y puede incluir el nombre del bloque padre como contexto, pero no expone el arbol completo de **Mission Nodes**.
_Avoid_: mostrar toda la jerarquia administrativa al jugador, convertir estructura de diseno en carga cognitiva de runtime

**Prompt**:
Texto principal visible de la hoja actual dentro de una **LiveSession**. Se copia desde el **Mission Stage** al crear el snapshot del **Session Stage Flow**. En `Trivia` se presenta como pregunta o enunciado; en `Treasure Hunt` como instruccion, objetivo o contexto previo al escaneo.
_Avoid_: respuesta correcta, hint liberada, nota privada de operador

## Flagged Ambiguities

**Participant**:
Si hablas de identidad autenticada, usa **User** en Identity and Access. Si hablas del grupo con el que compite dentro del juego, usa **Session Team**.

**Stage Claim**:
Queda descartado por ahora como concepto del dominio central. El lenguaje actual de UMBRAL favorece **Per-Team Progression**, no exclusividad de etapa para el primer equipo que la resuelve.

## Example Dialogue

Dev: "¿El grupo con el que juego existe en todo el sistema?"
Experto de dominio: "No. El Session Team nace dentro de una LiveSession y vale solo para esa ejecución."

Dev: "Cual es el agregado principal de Session Operations?"
Experto de dominio: "LiveSession. Ahi viven las invariantes que coordinan estado, equipos y progreso."

Dev: "Y Session Enrollment modifica otro agregado?"
Experto de dominio: "No. Entra por LiveSession porque la asignacion de equipos es parte de esa consistencia."

Dev: "Puedo cambiarme de grupo en cualquier momento?"
Experto de dominio: "Solo dentro del Team Assignment Window. Una vez iniciado el juego, ya no es un cambio libre del participante."

Dev: "Entrar a una sesion y formar grupo es parte del mismo bloque que avanzar etapas?"
Experto de dominio: "No necesariamente. Session Enrollment tiene reglas propias y conviene separarlo del progreso en vivo."

Dev: "Pausar una sesion y avanzar un equipo son la misma clase de decision?"
Experto de dominio: "No. Session Lifecycle gobierna el estado global; Session Progression gobierna el avance por equipo."

Dev: "Si un equipo resuelve la etapa 1, los demas ya no pueden hacerlo?"
Experto de dominio: "No. Cada Session Team progresa de forma independiente por su propio flujo."

Dev: "En Trivia, toda respuesta queda esperando al operador?"
Experto de dominio: "No. Primero intentamos resolver el Validation Outcome automaticamente; el operador entra solo si el caso es ambiguo."

Dev: "Las pistas son solo un atributo mas de la sesion?"
Experto de dominio: "No. Hint Release tiene reglas propias dentro de Session Operations, aunque siga perteneciendo al mismo bounded context."

Dev: "Cuando penalizo a un equipo, escribo cualquier numero?"
Experto de dominio: "No. En la operacion eliges una severidad predefinida y dejas el motivo; el descuento lo resuelve Scoring and Audit."

Dev: "Desactivar una etapa cambia la Mission Stage?"
Experto de dominio: "No. Cambia el Session Stage Flow de esta LiveSession."

Dev: "Puedo desactivar un bloque completo del arbol durante la sesion?"
Experto de dominio: "Si. La sesion puede desactivar una hoja o un nodo compuesto, y en este ultimo caso se desactivan sus hojas pendientes descendientes."

Dev: "Y si parte de ese bloque ya se jugo?"
Experto de dominio: "Se puede desactivar lo pendiente. Lo ya completado queda intacto en el historial."

Dev: "El participante ve todo el arbol de la mision?"
Experto de dominio: "No. Ve su hoja actual y, si ayuda, el nombre del bloque padre como contexto."

Dev: "Entonces una respuesta Trivia puede validarse sin pregunta?"
Experto de dominio: "No deberia. El participante necesita ver el Prompt de la hoja actual antes de enviar evidencia."

Dev: "Y Evidence Submission existe aunque luego no otorgue puntos?"
Experto de dominio: "Si. Primero es un hecho operativo de la sesion; el puntaje se decide aparte."
