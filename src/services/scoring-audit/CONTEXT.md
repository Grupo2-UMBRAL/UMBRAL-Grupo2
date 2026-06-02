# Scoring and Audit

Contexto responsable de transformar hechos operativos de una sesión en puntaje, ranking y trazabilidad. Aquí viven las reglas de cálculo, penalización y registro auditable.

## Language

**Score Entry**:
Registro atómico que explica una variación de puntaje. Un **Score Entry** pertenece a una **LiveSession** y a un equipo dentro de esa sesión. Conserva el valor completo de una **Penalty** incluso cuando el puntaje visible del equipo satura en cero.
_Avoid_: evidence submission, generic score

**Scoring**:
Capacidad interna de **Scoring and Audit** que transforma hechos operativos en puntaje, penalizaciones y ranking.
_Avoid_: session control, hint release

**Scoreboard**:
Agregado principal de **Scoring and Audit** para una **LiveSession**. El **Scoreboard** mantiene la consistencia del puntaje acumulado por equipo, aplica penalizaciones, impide credito positivo duplicado por **Mission Stage**, resuelve criterios de desempate y sirve como fuente de verdad para derivar el **Ranking**. El puntaje visible de un equipo tiene piso en cero.
_Avoid_: pure read model, session controller

**Penalty**:
Descuento de puntaje aplicado por un **Operator** con motivo explícito, momento registrado y **Penalty Severity** fija. Una **Penalty** se origina en **Session Operations** y puede originar uno o más **Score Entries** dentro del **Scoreboard**. Varias penalizaciones reales pueden afectar al mismo equipo en una **LiveSession**, pero el mismo comando técnico no puede aplicarse dos veces.
_Avoid_: correction, warning, monto libre, penalizacion automatica por intento invalido

**Penalty Severity**:
Clasificacion fija que determina el descuento de una **Penalty**. Los valores del primer release son Minor = -50, Major = -100 y Critical = -200.
_Avoid_: monto libre, porcentaje dinamico, descuento implicito

**Ranking**:
Ordenamiento vigente de los equipos de una **LiveSession** según puntaje y **Resolution Time**. El **Ranking** se deriva del **Scoreboard**, no es la fuente original de verdad. Cuando dos equipos coinciden en puntaje y **Resolution Time** a precision de 500 ms, conserva el empate sin tercer criterio oculto.
_Avoid_: leaderboard snapshot as source of truth

**Resolution Time**:
Tiempo oficial de resolucion usado solo para desempatar **Ranking** y para auditoria. Se mide desde la recepcion del envio en backend con precision de 500 ms. No modifica el puntaje ni compensa latencia.
_Avoid_: bonus por velocidad, ajuste por conexion, tercer criterio oculto

**Stage Credit**:
Credito positivo completo otorgado una sola vez a un equipo por cada **Mission Stage** resuelto dentro de una **LiveSession**. Depende de la **Difficulty** de la hoja: Easy = 100, Medium = 200 y Hard = 300. La misma tabla aplica para Trivia y Treasure Hunt.
_Avoid_: partial credit, credito duplicado por override, puntaje distinto por game type

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
