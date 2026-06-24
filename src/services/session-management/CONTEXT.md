# Session Management

Contexto responsable de la ejecucion en vivo de una mision para equipos concretos. Aqui viven el estado operativo de la sesion, el **Session Flow** efectivo (lista lineal de **Plays**) y la interaccion en tiempo real con operadores y participantes.

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

**Session Flow**:
Secuencia efectiva de **Plays** que una **LiveSession** ejecuta. La **Mission** ya entrega sus **Plays** en orden lineal global (1-based, depth-first ya resuelto), y el **Session Flow** los conserva como una lista plana que puede cambiar por desactivaciones propias de la sesion. Conserva los datos operativos necesarios de cada **Play**, incluido su **Prompt**, su **Difficulty**, su `gameType`, su `timeLimitMinutes` ya resuelto y, en Trivia, sus alternativas seleccionables, para que la experiencia del participante y el scoring de esa ejecucion no dependan de ediciones posteriores de la **Mission** reusable.
_Avoid_: Play base, flujo base, arbol de nodos

**Play**:
Unidad jugable individual dentro del **Session Flow** de una **LiveSession**. Cada **Play** trae ya resuelto su `gameType` (`Trivia` o `Treasure Hunt`), su **Difficulty** y su `timeLimitMinutes`, ademas de su **Prompt** y los datos propios del juego: en `Trivia`, las alternativas seleccionables y, solo del lado del servidor, cual es la correcta; en `Treasure Hunt`, el hash de QR esperado y sus **Hints**.
_Avoid_: Mission Stage, Mission Node, etapa del arbol

**Session Progression**:
Capacidad interna de **Session Operations** que gobierna el **Play** actual de cada **Session Team**, la aceptacion operativa de evidencias y el avance por el **Session Flow**.
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
Intento de un **Session Team** por resolver su **Play** actual dentro de una **LiveSession**. En `Trivia` el intento lleva el `choiceId` seleccionado; en `Treasure Hunt` el hash del QR escaneado. Session Operations decide si el envio es aceptable dentro del **Session Flow** activo antes de que otras capacidades lo usen.
_Avoid_: ScoreEntry, audit event, texto libre de respuesta Trivia

**Validation Override**:
Intervencion manual del **Operator** para corregir el resultado de una **Evidence Submission** ambigua dentro de una **LiveSession**. La correccion cambia el resultado operativo final, debe quedar auditada y, si confirma una respuesta valida, habilita el puntaje completo del **Play** sin duplicar credito positivo para el mismo **Play**.
_Avoid_: nuevo intento disfrazado, doble premio por el mismo Play, cambio silencioso sin trazabilidad

**Validation Outcome**:
Resultado de evaluar una **Evidence Submission** dentro de una **LiveSession**. Puede resolverse automaticamente por regla o requerir intervencion manual del operador en casos ambiguos.
_Avoid_: score entry, audit-only status

**Validation Override**:
Correccion manual del **Operator** sobre un **Validation Outcome** de Trivia. La validacion automatica de Trivia compara el `choiceId` seleccionado contra la alternativa correcta del **Play**, siempre del lado del servidor; el override existe solo para casos excepcionales. Puede habilitar el credito completo del **Play** una sola vez, pero no crea credito positivo duplicado.
_Avoid_: revision humana obligatoria para toda respuesta Trivia, comparar texto libre, puntaje parcial, segundo credito por el mismo Play

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
Accion operativa por la que el **Operator** sanciona a un **Session Team** dentro de una **LiveSession** eligiendo una severidad predefinida y registrando el motivo. Esta accion origina una **Penalty** para `Scoring and Monitoring`, pero no decide por si misma el recalculo interno del **Scoreboard**.
_Avoid_: descuento libre de puntos como decision operativa, recalculo de ranking dentro de Session Operations

**Penalty Command Idempotency**:
Proteccion tecnica que evita aplicar dos veces la misma **Penalty Application** cuando el mismo comando se reenvia o el operador dispara accidentalmente la misma accion duplicada. No fusiona penalizaciones de negocio realmente distintas.
_Avoid_: doble descuento accidental, deduplicar hechos distintos, esconder sanciones reales

**Invalid Attempt**:
Resultado operativo de una **Evidence Submission** rechazada dentro de una **LiveSession**. Queda auditado como intento invalido, pero no descuenta puntaje por si mismo sin una **Penalty Application** explicita del **Operator**.
_Avoid_: penalizacion automatica implicita, confundir rechazo de evidencia con sancion, descuento silencioso

**Session Flow Deactivation**:
Capacidad operativa de desactivar **Plays** pendientes dentro del **Session Flow** para una **LiveSession** concreta. Apunta a un **Play** individual que siga pendiente. Los **Plays** ya completados conservan su historial y no se reescriben.
_Avoid_: editar la Mission base, borrar historial ya ejecutado, desactivar un Play ya resuelto

**Participant Stage View**:
Vista operativa que recibe un **Session Team** participante durante la **LiveSession**. Muestra el **Play** actual y su **Prompt** visible; en `Trivia` incluye las alternativas seleccionables (id + texto) pero **nunca** cual es la correcta. No expone la respuesta correcta ni ninguna marca de correccion.
_Avoid_: revelar la alternativa correcta, exponer `correctChoiceId` o flag isCorrect al jugador, convertir estructura de diseno en carga cognitiva de runtime

**Prompt**:
Texto principal visible del **Play** actual dentro de una **LiveSession**. Se copia desde el **Play** de la **Mission** al crear el snapshot del **Session Flow**. En `Trivia` se presenta como pregunta o enunciado acompanado de sus alternativas; en `Treasure Hunt` como instruccion, objetivo o contexto previo al escaneo.
_Avoid_: respuesta correcta, hint liberada, nota privada de operador

## Flagged Ambiguities

**Participant**:
Si hablas de identidad autenticada, usa **User** en Identity and Access. Si hablas del grupo con el que compite dentro del juego, usa **Session Team**.

**Stage Claim**:
Queda descartado por ahora como concepto del dominio central. El lenguaje actual de UMBRAL favorece **Per-Team Progression**, no exclusividad de etapa para el primer equipo que la resuelve.

## Example Dialogue

Dev: "Â¿El grupo con el que juego existe en todo el sistema?"
Experto de dominio: "No. El Session Team nace dentro de una LiveSession y vale solo para esa ejecuciÃ³n."

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
Experto de dominio: "No. La validacion compara el choiceId seleccionado contra la alternativa correcta del Play, en el servidor; el operador entra solo si el caso es ambiguo."

Dev: "Y el jugador ve cual alternativa es la correcta?"
Experto de dominio: "Nunca. El Play expone las alternativas con id y texto, pero cual es la correcta vive solo en el dominio y en la comparacion server-side."

Dev: "Las pistas son solo un atributo mas de la sesion?"
Experto de dominio: "No. Hint Release tiene reglas propias dentro de Session Operations, aunque siga perteneciendo al mismo bounded context."

Dev: "Cuando penalizo a un equipo, escribo cualquier numero?"
Experto de dominio: "No. En la operacion eliges una severidad predefinida y dejas el motivo; el descuento lo resuelve Scoring and Monitoring."

Dev: "Desactivar un Play cambia la Mission?"
Experto de dominio: "No. Cambia el Session Flow de esta LiveSession."

Dev: "Y si ese Play ya se jugo?"
Experto de dominio: "Solo se desactiva lo pendiente. Lo ya completado queda intacto en el historial."

Dev: "El participante ve todos los Plays de la mision?"
Experto de dominio: "No. Ve su Play actual; el Session Flow es una lista lineal y avanza Play a Play."

Dev: "Entonces una respuesta Trivia puede validarse sin pregunta?"
Experto de dominio: "No deberia. El participante necesita ver el Prompt y las alternativas del Play actual antes de enviar evidencia."

Dev: "Y Evidence Submission existe aunque luego no otorgue puntos?"
Experto de dominio: "Si. Primero es un hecho operativo de la sesion; el puntaje se decide aparte."
