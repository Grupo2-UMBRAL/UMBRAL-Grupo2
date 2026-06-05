# Identity and Access

Contexto de soporte responsable de autenticación, autorización y administración de identidades operativas. No contiene las reglas centrales de juego, misión o puntaje.

## Language

**User**:
Identidad autenticable del sistema. Un **User** puede operar la plataforma web o acceder a la experiencia móvil según su rol.
_Avoid_: Team, participant session state

**Role**:
Capacidad autorizativa asignada a un **User**. En UMBRAL los roles actuales son Administrator, Operator y Participant.
_Avoid_: permission set ad hoc, team type

**Access Token**:
Credencial emitida tras autenticación que porta la identidad y los roles del usuario. Los demás contextos la consumen para autorizar acciones, pero no definen su semántica interna.
_Avoid_: session code, join code

## Flagged Ambiguities

**Participant**:
Cuando se hable del usuario autenticado en la app móvil, usar **User** con rol `Participant`. Cuando se hable del grupo compitiendo dentro del juego, usar **Session Team** en Session Operations.

## Example Dialogue

Dev: "El participante que inicia sesión, ¿es el mismo concepto que el equipo que compite?"
Experto de dominio: "No. El User autenticado pertenece a Identity and Access; el equipo compitiendo vive en la sesión."

Dev: "Entonces el token autoriza, pero no define progreso ni puntaje."
Experto de dominio: "Correcto. Eso pertenece a otros contextos."
