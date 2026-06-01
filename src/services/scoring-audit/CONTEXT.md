# Scoring and Audit

Contexto responsable de transformar hechos operativos de una sesión en puntaje, ranking y trazabilidad. Aquí viven las reglas de cálculo, penalización y registro auditable.

## Language

**Score Entry**:
Registro atómico que explica una variación de puntaje. Un **Score Entry** pertenece a una **LiveSession** y a un equipo dentro de esa sesión.
_Avoid_: evidence submission, generic score

**Single-Stage Credit**:
Regla por la que un **Session Team** solo puede recibir puntaje positivo una vez por cada **Mission Stage** resuelto dentro de una **LiveSession**. Reintentos y correcciones posteriores pueden cambiar el resultado auditable de validacion, pero no duplican credito positivo sobre la misma hoja.
_Avoid_: doble conteo por reenvio, sumar puntos otra vez por override manual, premio acumulado por la misma hoja

**Full Credit Override**:
Regla por la que una correccion manual valida de `Trivia` otorga el mismo puntaje base que habria otorgado la validacion automatica de esa hoja. La intervencion humana cambia la decision de validacion, no la tabla de scoring.
_Avoid_: descuento por revision manual, multiplicador especial por override, scoring distinto segun canal de validacion

**No Partial Credit**:
Regla por la que un **Mission Stage** solo puede otorgar `0` o su puntaje base completo dentro del **Scoreboard**. El primer release no permite puntajes fraccionados ni creditos parciales por respuestas incompletas o casi correctas.
_Avoid_: porcentaje manual de puntos, premio intermedio discrecional, score fraccionado por interpretacion del operador

**Scoring**:
Capacidad interna de **Scoring and Audit** que transforma hechos operativos en puntaje, penalizaciones y ranking.
_Avoid_: session control, hint release

**Scoreboard**:
Agregado principal de **Scoring and Audit** para una **LiveSession**. El **Scoreboard** mantiene la consistencia del puntaje acumulado por equipo, aplica penalizaciones, resuelve criterios de desempate y sirve como fuente de verdad para derivar el **Ranking**.
_Avoid_: pure read model, session controller

**Score Floor**:
Regla por la que el puntaje acumulado visible de un equipo en el **Scoreboard** no puede bajar de cero. La penalizacion completa sigue quedando explicada en auditoria y en los **Score Entries**, aunque el acumulado visible quede saturado en `0`.
_Avoid_: score negativo visible, borrar rastro de penalizacion por saturar en cero, delegar esta regla solo a UI

**Penalty**:
Descuento de puntaje aplicado con motivo explícito y momento registrado. Una **Penalty** se origina en **Session Operations** y puede originar uno o más **Score Entries** dentro del **Scoreboard**.
_Avoid_: correction, warning

**Penalty Severity**:
Clasificacion predefinida que determina el descuento fijo de una **Penalty**. En el primer release los valores canonicos son Minor, Major y Critical.
_Avoid_: numero libre ingresado por el operador, etiqueta informal sin efecto en scoring

**Explicit Penalty Only**:
Regla por la que un intento invalido o una evidencia rechazada no descuentan puntaje por si mismos. El **Scoreboard** solo reduce puntaje cuando recibe una **Penalty** explicita originada por el **Operator**.
_Avoid_: castigo automatico por error, descuento implicito por rechazo, mezclar validacion con sancion

**Distinct Penalty Events**:
Regla por la que varias **Penalty** pueden afectar al mismo equipo dentro de una **LiveSession** si corresponden a hechos distintos. La deduplicacion solo aplica a reenvios tecnicos del mismo comando, no a sanciones de negocio separadas.
_Avoid_: colapsar penalizaciones reales en una sola, sumar dos veces el mismo comando tecnico, perder trazabilidad entre eventos

**Ranking**:
Ordenamiento vigente de los equipos de una **LiveSession** según puntaje y criterio de desempate. El **Ranking** se deriva del **Scoreboard**, no es la fuente original de verdad.
_Avoid_: leaderboard snapshot as source of truth

**Resolution Time**:
Tiempo auditable usado para desempatar el **Ranking** entre equipos con el mismo puntaje dentro de una **LiveSession**. No altera el puntaje, se mide con precision de medio segundo y usa como instante oficial la recepcion del envio en backend.
_Avoid_: multiplicador de score, latencia cruda de red como verdad oficial, tiempo informal de cliente

**Shared Rank Tie**:
Resultado por el que dos o mas equipos conservan empate en el **Ranking** cuando coinciden tanto en puntaje como en **Resolution Time** a precision de medio segundo. No se rompe el empate con criterios internos no visibles al negocio.
_Avoid_: desempate oculto por timestamp exacto, orden interno tecnico, criterio arbitrario no auditado

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
