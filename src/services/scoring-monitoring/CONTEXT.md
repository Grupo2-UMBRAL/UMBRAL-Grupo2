# Scoring and Monitoring

Contexto responsable de transformar hechos operativos de una sesiÃ³n en puntaje, ranking y trazabilidad. AquÃ­ viven las reglas de cÃ¡lculo, penalizaciÃ³n y registro auditable.

## Language

**Score Entry**:
Registro atÃ³mico que explica una variaciÃ³n de puntaje. Un **Score Entry** pertenece a una **LiveSession** y a un equipo dentro de esa sesiÃ³n. Conserva el valor completo de una **Penalty** incluso cuando el puntaje visible del equipo satura en cero.
_Avoid_: evidence submission, generic score

**Single-Play Credit**:
Regla por la que un **Session Team** solo puede recibir puntaje positivo una vez por cada **Play** resuelto dentro de una **LiveSession**. Reintentos y correcciones posteriores pueden cambiar el resultado auditable de validacion, pero no duplican credito positivo sobre la misma hoja.
_Avoid_: doble conteo por reenvio, sumar puntos otra vez por override manual, premio acumulado por la misma hoja

**Full Credit Override**:
Regla por la que una correccion manual valida de `Trivia` otorga el mismo puntaje base que habria otorgado la validacion automatica de esa hoja. La intervencion humana cambia la decision de validacion, no la tabla de scoring.
_Avoid_: descuento por revision manual, multiplicador especial por override, scoring distinto segun canal de validacion

**No Partial Credit**:
Regla por la que un **Play** solo puede otorgar `0` o su puntaje base completo dentro del **Scoreboard**. El primer release no permite puntajes fraccionados ni creditos parciales por respuestas incompletas o casi correctas.
_Avoid_: porcentaje manual de puntos, premio intermedio discrecional, score fraccionado por interpretacion del operador

**Scoring**:
Capacidad interna de **Scoring and Monitoring** que transforma hechos operativos en puntaje, penalizaciones y ranking.
_Avoid_: session control, hint release

**Scoreboard**:
Agregado principal de **Scoring and Monitoring** para una **LiveSession**. El **Scoreboard** mantiene la consistencia del puntaje acumulado por equipo, aplica penalizaciones, impide credito positivo duplicado por **Play**, resuelve criterios de desempate y sirve como fuente de verdad para derivar el **Ranking**. El puntaje visible de un equipo tiene piso en cero.
_Avoid_: pure read model, session controller

**Score Floor**:
Regla por la que el puntaje acumulado visible de un equipo en el **Scoreboard** no puede bajar de cero. La penalizacion completa sigue quedando explicada en auditoria y en los **Score Entries**, aunque el acumulado visible quede saturado en `0`.
_Avoid_: score negativo visible, borrar rastro de penalizacion por saturar en cero, delegar esta regla solo a UI

**Penalty**:
Descuento de puntaje aplicado por un **Operator** con motivo explÃ­cito, momento registrado y **Penalty Severity** fija. Una **Penalty** se origina en **Session Operations** y puede originar uno o mÃ¡s **Score Entries** dentro del **Scoreboard**. Varias penalizaciones reales pueden afectar al mismo equipo en una **LiveSession**, pero el mismo comando tÃ©cnico no puede aplicarse dos veces.
_Avoid_: correction, warning, monto libre, penalizacion automatica por intento invalido

**Penalty Severity**:
Clasificacion fija que determina el descuento de una **Penalty**. Los valores del primer release son Minor = -50, Major = -100 y Critical = -200.
_Avoid_: monto libre, porcentaje dinamico, descuento implicito

**Explicit Penalty Only**:
Regla por la que un intento invalido o una evidencia rechazada no descuentan puntaje por si mismos. El **Scoreboard** solo reduce puntaje cuando recibe una **Penalty** explicita originada por el **Operator**.
_Avoid_: castigo automatico por error, descuento implicito por rechazo, mezclar validacion con sancion

**Distinct Penalty Events**:
Regla por la que varias **Penalty** pueden afectar al mismo equipo dentro de una **LiveSession** si corresponden a hechos distintos. La deduplicacion solo aplica a reenvios tecnicos del mismo comando, no a sanciones de negocio separadas.
_Avoid_: colapsar penalizaciones reales en una sola, sumar dos veces el mismo comando tecnico, perder trazabilidad entre eventos

**Ranking**:
Ordenamiento vigente de los equipos de una **LiveSession** segÃºn puntaje y **Resolution Time**. El **Ranking** se deriva del **Scoreboard**, no es la fuente original de verdad. Cuando dos equipos coinciden en puntaje y **Resolution Time** a precision de 500 ms, conserva el empate sin tercer criterio oculto.
_Avoid_: leaderboard snapshot as source of truth

**Resolution Time**:
Tiempo auditable usado para desempatar el **Ranking** entre equipos con el mismo puntaje dentro de una **LiveSession**. No altera el puntaje, se mide con precision de medio segundo y usa como instante oficial la recepcion del envio en backend.
_Avoid_: multiplicador de score, latencia cruda de red como verdad oficial, tiempo informal de cliente

**Shared Rank Tie**:
Resultado por el que dos o mas equipos conservan empate en el **Ranking** cuando coinciden tanto en puntaje como en **Resolution Time** a precision de medio segundo. No se rompe el empate con criterios internos no visibles al negocio.
_Avoid_: desempate oculto por timestamp exacto, orden interno tecnico, criterio arbitrario no auditado

**Play Credit**:
Credito positivo completo otorgado una sola vez a un equipo por cada **Play** resuelto dentro de una **LiveSession**. Depende de la **Difficulty** de la hoja: Easy = 100, Medium = 200 y Hard = 300. La misma tabla aplica para Trivia y Treasure Hunt.
_Avoid_: partial credit, credito duplicado por override, puntaje distinto por game type

**Audit Log**:
Capacidad interna de **Scoring and Monitoring** que registra el historial auditable de hechos relevantes de una **LiveSession**.
_Avoid_: debug log, websocket stream

**Session Event Log**:
Historial auditable de hechos relevantes asociados a una **LiveSession**. Sirve para trazabilidad y supervisiÃ³n posterior.
_Avoid_: websocket notification, debug log

## Example Dialogue

Dev: "El equipo enviÃ³ una evidencia, Â¿eso ya es un Score Entry?"
Experto de dominio: "No. El envÃ­o ocurre en la operaciÃ³n; el Score Entry aparece cuando una regla de puntaje decide una variaciÃ³n."

Dev: "Entonces, Â¿quÃ© guarda la consistencia principal de puntaje?"
Experto de dominio: "El Scoreboard. Los Score Entries explican los cambios, pero la consistencia acumulada vive allÃ­."

Dev: "Â¿El desempate tambiÃ©n vive allÃ­?"
Experto de dominio: "SÃ­. Si afecta la consistencia del ranking de la sesiÃ³n, el Scoreboard lo gobierna."

Dev: "Â¿Scoring y Audit Log son el mismo mÃ³dulo porque miran los mismos hechos?"
Experto de dominio: "No. Pueden consumir hechos parecidos, pero cambian por razones distintas."

Dev: "Entonces el Ranking no decide nada."
Experto de dominio: "Exacto. El Ranking resume resultados; no gobierna la sesiÃ³n."
