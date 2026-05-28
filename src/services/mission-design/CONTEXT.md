# Mission Design

Contexto responsable del diseno reusable de las experiencias de juego. Aqui viven las definiciones base que pueden reutilizarse en multiples sesiones en vivo.

## Language

**Mission**:
Plantilla reusable que define una experiencia de juego. Una **Mission** contiene sus **Mission Stages**, **Hints** y un **Game Type**.
_Avoid_: LiveSession, partida, ejecucion

**Mission Stage**:
Nodo jugable definido dentro de una **Mission**. Una **Mission Stage** expresa el orden base dentro de la mision y representa, por defecto, una unidad de juego completa, salvo que contenga subetapas explicitas.
_Avoid_: Session Stage, etapa ejecutada, paso puramente visual

**Mission Node**:
Concepto estructural para modelar composicion dentro de una **Mission**. Un **Mission Node** puede representar una **Mission Stage** completa o una subetapa dentro de una jerarquia de etapas reutilizable.
_Avoid_: LiveSession node, UI tree

**Substage**:
Etapa hija dentro de una **Mission Stage** o dentro de otro **Mission Node**. Una **Substage** permite descomponer un bloque de juego mayor sin dejar de pertenecer al diseno reusable de la mision.
_Avoid_: session checkpoint, runtime progress marker

**Stage Template Reuse**:
Capacidad de usar una **Mission Stage** o un bloque compuesto de nodos como plantilla dentro de otra **Mission**, ya sea reutilizandolo como bloque completo o aplicando modificaciones sobre esa base.
_Avoid_: copy-paste accidental, runtime cloning

**Hint**:
Pieza de informacion asociada a una **Mission Stage**. Puede ser visible durante la sesion o revelarse como solucion al finalizar.
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
Experto de dominio: "Por defecto si, pero puede contener subetapas cuando el diseno necesite descomponer ese bloque."

Dev: "Y esa etapa puede usarse otra vez en otra Mission?"
Experto de dominio: "Si. Una Mission Stage o un bloque compuesto puede reutilizarse como plantilla en otra mision, completo o con ajustes."

Dev: "Entonces, Mission Stage y Mission Node son lo mismo?"
Experto de dominio: "No exactamente. Mission Stage es la unidad jugable y ordenable de la mision. Mission Node es el concepto estructural que permite composicion y subetapas."
