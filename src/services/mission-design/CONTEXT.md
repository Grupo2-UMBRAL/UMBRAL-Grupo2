# Mission Design

Contexto responsable del diseno reusable de las experiencias de juego. Aqui viven las definiciones base que pueden reutilizarse en multiples sesiones en vivo.

## Language

**Mission**:
Plantilla reusable que define una experiencia de juego. Una **Mission** contiene un arbol ordenado de **Mission Nodes** y puede combinar varios **Game Types** a traves de sus **Mission Stages** jugables. Las **Hints** viven dentro de los **Mission Stages**.
_Avoid_: LiveSession, partida, ejecucion

**Mission Stage**:
**Mission Node** hoja que representa una unidad jugable concreta dentro de una **Mission**. Un **Mission Stage** no tiene hijos, tiene exactamente un **Game Type**, define su propia **Difficulty**, y es la unidad que luego puede participar en flujo operativo.
_Avoid_: Session Stage, etapa ejecutada, paso puramente visual

**Mission Node**:
Componente estructural ordenable dentro de una **Mission**. Un **Mission Node** puede ser compuesto si tiene hijos o puede materializarse como un **Mission Stage** si es hoja jugable. Cuando un **Mission Node** compuesto agrupa un bloque tematico de negocio, sus descendientes jugables deben compartir el mismo **Game Type** y puede definir un **Default Time Budget** heredable para su subarbol.
_Avoid_: LiveSession node, UI tree

**Substage**:
Relacion padre-hijo entre un **Mission Node** compuesto y otro **Mission Node** dentro de la misma **Mission**. La composicion puede repetirse recursivamente en varios niveles.
_Avoid_: session checkpoint, runtime progress marker, paso visual sin significado de dominio

**Stage Template Reuse**:
Capacidad de reutilizar un **Mission Stage** o un subarbol de **Mission Nodes** dentro de otra **Mission** para acelerar el diseno administrativo. El reuse crea una copia independiente en la mision destino, conserva referencia de trazabilidad al origen visible como metadata de solo lectura, solo puede tomar como origen nodos de misiones visibles para el **Administrator** que ejecuta la accion, copia el subarbol completo con sus descendientes, **Hints**, tiempos y metadata, y deja un evento auditable de la operacion.
_Avoid_: runtime cloning, referencia compartida obligatoria, herencia viva entre misiones, exigir mision activa solo para copiar, copia parcial ambigua en el mismo acto de reuse, trazabilidad solo escondida en logs

**Hint**:
Pieza de informacion asociada directamente a un **Mission Stage**. Puede ser visible durante la sesion o revelarse como solucion al finalizar.
_Avoid_: Event, evidence, notification

**Game Type**:
Clasificacion fija de un **Mission Stage** que determina su estrategia de validacion de evidencias. En UMBRAL los valores actuales son Treasure Hunt y Trivia.
_Avoid_: session mode, mission-wide rule cuando la mision mezcla tipos

**Difficulty**:
Clasificacion discreta de un **Mission Stage** usada para determinar su puntaje base cuando esa hoja se resuelve. En el primer release los valores canonicos son Easy, Medium y Hard.
_Avoid_: dificultad global de la Mission, dificultad heredada desde Mission Node compuesto, estimacion informal del operador

**Default Time Budget**:
Valor temporal por defecto definido en un **Mission Node** compuesto para sus descendientes. Se hereda hacia abajo mientras un subnodo o un **Mission Stage** no declare su propio tiempo explicito. Cuando aparece un valor mas especifico, reemplaza por completo al heredado.
_Avoid_: limite total de bloque, timer visual solamente, duracion estimada sin efecto operativo

**Time Budget**:
Limite temporal efectivo de un **Mission Stage** jugable. Puede declararse directamente en la hoja o resolverse por herencia desde el **Default Time Budget** de sus ancestros.
_Avoid_: promedio calculado automaticamente por cantidad de hijos, limite total de subarbol

## Flagged Ambiguities

**Stage**:
No usar `stage` para cualquier nodo del arbol. Si el nodo tiene hijos, hablar de **Mission Node** compuesto. Si es hoja jugable, hablar de **Mission Stage**.

## Example Dialogue

Dev: "Si una etapa resulta demasiado dificil en una ejecucion, edito la Mission?"
Experto de dominio: "No. La Mission conserva el diseno base; el ajuste operativo ocurre en la sesion."

Dev: "Entonces el Game Type pertenece al diseno, no a la operacion."
Experto de dominio: "Correcto. La sesion deriva ese tipo desde cada Mission Stage jugable."

Dev: "Si una etapa tiene subetapas, todas son jugables?"
Experto de dominio: "No. Solo los nodos hoja son Mission Stages jugables; los nodos internos componen la estructura."

Dev: "Entonces una Mission completa tiene un solo Game Type?"
Experto de dominio: "No necesariamente. Cada Mission Stage define su propio Game Type y la Mission puede mezclar varios."

Dev: "Y la dificultad para scoring vive en la Mission completa o en el nodo hoja?"
Experto de dominio: "En el Mission Stage. El nodo compuesto estructura el arbol, pero la hoja jugable define la Difficulty que alimenta el puntaje."

Dev: "El tiempo del bloque grande es solo decorativo?"
Experto de dominio: "No. Si el nodo compuesto define un Default Time Budget, sus descendientes lo heredan salvo que alguno lo sobreescriba."

Dev: "Y si una hoja define su propio tiempo, se combina con el heredado?"
Experto de dominio: "No. El valor local reemplaza por completo al heredado."

Dev: "Y esa etapa puede usarse otra vez en otra Mission?"
Experto de dominio: "Si, mediante Stage Template Reuse. Pero lo reutilizado entra como copia independiente, no como referencia global compartida."

Dev: "Solo puedo reutilizar nodos de misiones activas?"
Experto de dominio: "No. La regla correcta es visibilidad para el Administrator, no estado operativo de la Mission."

Dev: "Cuando reutilizo un bloque, puedo traerme solo algunos hijos?"
Experto de dominio: "No en el acto de reuse. Primero se copia el subarbol completo y luego editas la copia destino si quieres recortarla."

Dev: "Y como se sabe de donde salio esa copia?"
Experto de dominio: "La copia muestra su origen como metadata de solo lectura y ademas la operacion queda registrada en auditoria."

Dev: "Entonces, Mission Stage y Mission Node son lo mismo?"
Experto de dominio: "No. Mission Node es el concepto general; Mission Stage es solo el nodo hoja jugable."
