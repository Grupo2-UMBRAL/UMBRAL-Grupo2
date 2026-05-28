# Scoring and Audit

Contexto responsable de transformar hechos operativos de una sesión en puntaje, ranking y trazabilidad. Aquí viven las reglas de cálculo, penalización y registro auditable.

## Language

**Score Entry**:
Registro atómico que explica una variación de puntaje. Un **Score Entry** pertenece a una **LiveSession** y a un equipo dentro de esa sesión.
_Avoid_: evidence submission, generic score

**Scoring**:
Capacidad interna de **Scoring and Audit** que transforma hechos operativos en puntaje, penalizaciones y ranking.
_Avoid_: session control, hint release

**Scoreboard**:
Agregado principal de **Scoring and Audit** para una **LiveSession**. El **Scoreboard** mantiene la consistencia del puntaje acumulado por equipo, aplica penalizaciones, resuelve criterios de desempate y sirve como fuente de verdad para derivar el **Ranking**.
_Avoid_: pure read model, session controller

**Penalty**:
Descuento de puntaje aplicado con motivo explícito y momento registrado. Una **Penalty** se origina en **Session Operations** y puede originar uno o más **Score Entries** dentro del **Scoreboard**.
_Avoid_: correction, warning

**Ranking**:
Ordenamiento vigente de los equipos de una **LiveSession** según puntaje y criterio de desempate. El **Ranking** se deriva del **Scoreboard**, no es la fuente original de verdad.
_Avoid_: leaderboard snapshot as source of truth

**Audit Log**:
Capacidad interna de **Scoring and Audit** que registra el historial auditable de hechos relevantes de una **LiveSession**.
_Avoid_: debug log, websocket stream

**Session Event Log**:
Historial auditable de hechos relevantes asociados a una **LiveSession**. Sirve para trazabilidad y supervisión posterior.
_Avoid_: websocket notification, debug log

## Example Dialogue

Dev: "El equipo envió una evidencia, ¿eso ya es un Score Entry?"
Experto de dominio: "No. El envío ocurre en la operación; el Score Entry aparece cuando una regla de puntaje decide una variación."

Dev: "Entonces, ¿qué guarda la consistencia principal de puntaje?"
Experto de dominio: "El Scoreboard. Los Score Entries explican los cambios, pero la consistencia acumulada vive allí."

Dev: "¿El desempate también vive allí?"
Experto de dominio: "Sí. Si afecta la consistencia del ranking de la sesión, el Scoreboard lo gobierna."

Dev: "¿Scoring y Audit Log son el mismo módulo porque miran los mismos hechos?"
Experto de dominio: "No. Pueden consumir hechos parecidos, pero cambian por razones distintas."

Dev: "Entonces el Ranking no decide nada."
Experto de dominio: "Exacto. El Ranking resume resultados; no gobierna la sesión."
