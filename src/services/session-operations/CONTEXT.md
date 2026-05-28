# Session Operations

Contexto responsable de la ejecución en vivo de una misión para equipos concretos. Aquí viven el estado operativo de la sesión, el flujo efectivo de etapas y la interacción en tiempo real con operadores y participantes.

## Language

**LiveSession**:
Ejecución en vivo de una **Mission** para equipos concretos. Una **LiveSession** es el agregado principal de este contexto y contiene las invariantes operativas sobre estado, participantes y progreso.
_Avoid_: Mission, plantilla

**Session Join Code**:
Código entregado por el operador para entrar a una **LiveSession**. El **Session Join Code** identifica la sesión de destino, pero no define por sí mismo el equipo dentro de esa sesión.
_Avoid_: access token, team code

**Session Enrollment**:
Capacidad interna de **Session Operations** que gobierna entrada a una **LiveSession**, creación o unión a un **Session Team** y reglas del **Team Assignment Window**. Sus cambios entran por el agregado **LiveSession**.
_Avoid_: live progression, scoring, authentication internals

**Session Lifecycle**:
Capacidad interna de **Session Operations** que gobierna los estados globales de una **LiveSession** y sus transiciones válidas.
_Avoid_: per-team progression, scoring update

**Session Team**:
Grupo creado o elegido por participantes dentro de una **LiveSession**. Un **Session Team** existe solo dentro de esa sesión y se modela como parte de la consistencia del agregado **LiveSession**.
_Avoid_: team global, user

**Session Stage Flow**:
Secuencia efectiva de etapas que una **LiveSession** ejecuta. Se deriva de las **Mission Stages** seleccionadas y puede cambiar por desactivaciones propias de la sesión.
_Avoid_: Mission Stage, flujo base

**Session Progression**:
Capacidad interna de **Session Operations** que gobierna la etapa actual de cada **Session Team**, la aceptación operativa de evidencias y el avance por el **Session Stage Flow**.
_Avoid_: global session state, ranking projection

**Per-Team Progression**:
Regla por la que cada **Session Team** avanza por su propia etapa actual sin bloquear ni desbloquear el avance de otros equipos. El progreso de un equipo no reclama una etapa para el resto.
_Avoid_: exclusive claim, first-team-wins stage lock

**Team Participation**:
Vinculación entre un participante autenticado y un **Session Team** dentro de una **LiveSession**. Expresa en qué grupo juega ese participante durante esa ejecución.
_Avoid_: membership global, user account only

**Team Assignment Window**:
Período previo al inicio efectivo del juego en el que un participante puede crear o cambiar su **Session Team** dentro de una **LiveSession**. Después de ese punto, cualquier corrección de asignación es una intervención excepcional del operador.
_Avoid_: open-ended switching, runtime free reassignment

**Evidence Submission**:
Intento de un **Session Team** por resolver su etapa actual dentro de una **LiveSession**. Session Operations decide si el envío es aceptable dentro del flujo activo antes de que otras capacidades lo usen.
_Avoid_: ScoreEntry, audit event

**Validation Outcome**:
Resultado de evaluar una **Evidence Submission** dentro de una **LiveSession**. Puede resolverse automáticamente por regla o requerir intervención manual del operador en casos ambiguos.
_Avoid_: score entry, audit-only status

**Session State**:
Estado operativo de una **LiveSession**. Controla si la sesión admite avances, evidencias y acciones del operador.
_Avoid_: mission status, connection status

**Hint Release**:
Capacidad operativa que controla qué **Hints** quedan visibles para cada **Session Team** durante una **LiveSession**. Incluye liberación manual, liberación por regla y revelación final de soluciones.
_Avoid_: mission design hint authoring, generic notification

## Flagged Ambiguities

**Participant**:
Si hablas de identidad autenticada, usa **User** en Identity and Access. Si hablas del grupo con el que compite dentro del juego, usa **Session Team**.

**Stage Claim**:
Queda descartado por ahora como concepto del dominio central. El lenguaje actual de UMBRAL favorece **Per-Team Progression**, no exclusividad de etapa para el primer equipo que la resuelve.

## Example Dialogue

Dev: "¿El grupo con el que juego existe en todo el sistema?"
Experto de dominio: "No. El Session Team nace dentro de una LiveSession y vale solo para esa ejecución."

Dev: "¿Cuál es el agregado principal de Session Operations?"
Experto de dominio: "LiveSession. Ahí viven las invariantes que coordinan estado, equipos y progreso."

Dev: "¿Y Session Enrollment modifica otro agregado?"
Experto de dominio: "No. Entra por LiveSession porque la asignación de equipos es parte de esa consistencia."

Dev: "¿Puedo cambiarme de grupo en cualquier momento?"
Experto de dominio: "Solo dentro del Team Assignment Window. Una vez iniciado el juego, ya no es un cambio libre del participante."

Dev: "¿Entrar a una sesión y formar grupo es parte del mismo bloque que avanzar etapas?"
Experto de dominio: "No necesariamente. Session Enrollment tiene reglas propias y conviene separarlo del progreso en vivo."

Dev: "¿Pausar una sesión y avanzar un equipo son la misma clase de decisión?"
Experto de dominio: "No. Session Lifecycle gobierna el estado global; Session Progression gobierna el avance por equipo."

Dev: "Si un equipo resuelve la etapa 1, ¿los demás ya no pueden hacerlo?"
Experto de dominio: "No. Cada Session Team progresa de forma independiente por su propio flujo."

Dev: "En Trivia, ¿toda respuesta queda esperando al operador?"
Experto de dominio: "No. Primero intentamos resolver el Validation Outcome automáticamente; el operador entra solo si el caso es ambiguo."

Dev: "¿Las pistas son solo un atributo más de la sesión?"
Experto de dominio: "No. Hint Release tiene reglas propias dentro de Session Operations, aunque siga perteneciendo al mismo bounded context."

Dev: "¿Desactivar una etapa cambia la Mission Stage?"
Experto de dominio: "No. Cambia el Session Stage Flow de esta LiveSession."

Dev: "¿Y Evidence Submission existe aunque luego no otorgue puntos?"
Experto de dominio: "Sí. Primero es un hecho operativo de la sesión; el puntaje se decide aparte."
