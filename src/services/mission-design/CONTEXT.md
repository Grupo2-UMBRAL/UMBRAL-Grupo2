# Mission Design

Contexto responsable del diseno reusable de las experiencias de juego. Aqui viven las definiciones base que pueden reutilizarse en multiples sesiones en vivo.

## Language

**Mission**:
Plantilla reusable que define una experiencia de juego. En primer release, una **Mission** contiene una lista lineal de **Mission Stages** y un **Game Type**. Las **Hints** viven dentro de cada **Mission Stage**.
_Avoid_: LiveSession, partida, ejecucion

**Mission Stage**:
Unidad jugable y ordenable definida dentro de una **Mission**. En primer release, una **Mission Stage** es la unidad minima de diseno, activacion y flujo operativo soportada por producto.
_Avoid_: Session Stage, etapa ejecutada, paso puramente visual

**Mission Node**:
Concepto estructural reservado para posible composicion futura dentro de una **Mission**. En primer release no se expone como capacidad funcional separada: todo nodo soportado por producto se materializa como **Mission Stage**.
_Avoid_: LiveSession node, UI tree

**Substage**:
Posible etapa hija dentro de una **Mission Stage** o dentro de otro **Mission Node**. Queda fuera de alcance en primer release y no debe asumirse en contratos funcionales ni flujo de sesion actual.
_Avoid_: session checkpoint, runtime progress marker

**Stage Template Reuse**:
Posible capacidad futura de reutilizar una **Mission Stage** o bloque compuesto dentro de otra **Mission**. Queda fuera de alcance en primer release. Unidad reusable soportada hoy: **Mission** completa.
_Avoid_: copy-paste accidental, runtime cloning

**Hint**:
Pieza de informacion asociada directamente a una **Mission Stage**. Puede ser visible durante la sesion o revelarse como solucion al finalizar.
_Avoid_: Event, evidence, notification

**Game Type**:
Clasificacion fija de una **Mission** que determina la estrategia de validacion de evidencias. En UMBRAL los valores actuales son Treasure Hunt y Trivia.
_Avoid_: session mode, runtime rule

## Example Dialogue

Dev: "Si una etapa resulta demasiado dificil en una ejecucion, edito la Mission?"
Experto de dominio: "No. La Mission conserva el diseno base; el ajuste operativo ocurre en la sesion."

Dev: "Entonces el Game Type pertenece al diseno, no a la operacion."
Experto de dominio: "Correcto. La sesion hereda ese tipo desde la Mission."

Dev: "Una Mission Stage es siempre un unico juego indivisible?"
Experto de dominio: "En primer release si. Si luego necesitamos descomponerla, eso sera una expansion explicita."

Dev: "Y esa etapa puede usarse otra vez en otra Mission?"
Experto de dominio: "No en primer release. Reuse soportado hoy ocurre al nivel de Mission completa."

Dev: "Entonces, Mission Stage y Mission Node son lo mismo?"
Experto de dominio: "En producto actual, tratarlos igual. Mission Node queda reservado como concepto futuro, no como capacidad separada."
