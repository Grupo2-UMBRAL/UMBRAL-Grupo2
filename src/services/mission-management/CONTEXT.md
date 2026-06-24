# Mission Management

Contexto responsable del diseno reusable de las experiencias de juego. Aqui viven las definiciones base que pueden reutilizarse en multiples sesiones en vivo.

El recorrido jugable es **lineal**; la organizacion admite **Sections** recursivas pero **inertes** (patron Composite). El path es el flatten depth-first de los **Challenges** hoja. La decision esta registrada en `docs/adr/0001-linear-path-challenge-model.md`.

## Language

**Mission**:
Plantilla reusable que define una experiencia de juego como una secuencia lineal ordenada de **Path Items**. Puede mezclar varios **Game Types** a traves de sus **Challenges**, y deriva (no autorea) su dificultad y sus tipos a partir de ellos.
_Avoid_: LiveSession, partida, ejecucion, arbol de nodos

**Path Item**:
Componente ordenable de una **Mission**: o un **Challenge** jugable (hoja), o una **Section** organizativa (composite) que agrupa mas Path Items. Es el rol Component del patron Composite.
_Avoid_: Mission Node con datos de juego, hoja jugable que se anida

**Section**:
**Path Item** organizativo (composite) que agrupa de forma ordenada a otros Path Items, incluidas otras **Sections** de forma recursiva. Es **inerte**: solo aporta titulo y orden, sin **Game Type**, **Difficulty**, tiempo, validacion ni herencia. No se "juega".
_Avoid_: Mission Node compuesto con datos de juego, divisor plano sin anidacion, contenedor que hereda tiempo o dificultad

**Challenge**:
Bloque jugable y tipado dentro de la ruta, y la **hoja** (Leaf) del Composite. Agrupa varias jugadas homogeneas (**Questions** o **Searches**) que comparten su configuracion general (**Game Type**, **Difficulty** y **Time Limit** por defecto). Es la estacion del recorrido; sus jugadas se resuelven en orden y nunca se anida en otro **Challenge**.
_Avoid_: Mission Stage, etapa, Mission Node, challenge anidado

**Trivia Challenge**:
**Challenge** de tipo Trivia. Contiene una secuencia ordenada de **Questions** estilo Kahoot.
_Avoid_: quiz suelto, pregunta unica

**Treasure Hunt Challenge**:
**Challenge** de tipo Treasure Hunt. Contiene una secuencia ordenada de **Searches**.
_Avoid_: busqueda unica, mapa

**Question**:
Jugada de un **Trivia Challenge**: un enunciado de texto con entre 2 y 4 **Choices**, de las cuales exactamente una es correcta. Es una unidad puntuable.
_Avoid_: Prompt, trivia valid answer, criterio de validacion, respuesta de texto libre

**Choice**:
Una opcion de respuesta de una **Question**. Exactamente una **Choice** por **Question** es la correcta.
_Avoid_: alternativa de texto abierto, respuesta libre

**Search**:
Jugada de un **Treasure Hunt Challenge**: una pista que conduce a un codigo QR esperado, con sus propias **Hints**. Es una unidad puntuable.
_Avoid_: Mission Stage QR, pregunta

**Hint**:
Pieza de ayuda asociada a una **Search**. Puede acompanar la busqueda o revelarse como solucion, y puede llevar coordenadas. Las **Questions** de trivia no usan **Hints**.
_Avoid_: Event, evidence, notification, pista de trivia

**Game Type**:
Clasificacion de un **Challenge** que determina su estrategia de validacion y la forma de sus jugadas. Valores actuales: Trivia y Treasure Hunt. Ya no es un valor unico de la **Mission**.
_Avoid_: session mode, mission-wide rule, tipo a nivel mision

**Difficulty**:
Clasificacion (Easy, Medium, Hard) que alimenta el puntaje de una jugada validada. Se define por defecto en el **Challenge** y una jugada puede sobreescribirla. La **Mission** no tiene **Difficulty** propia: la deriva.
_Avoid_: mission-wide difficulty, dificultad heredada por arbol, puntaje libre

**Time Limit**:
Limite temporal efectivo de una jugada. Se define por defecto en el **Challenge** y una jugada puede sobreescribirlo; no se hereda en cadena por niveles.
_Avoid_: Default Time Budget heredable, limite total de subarbol, timer decorativo

**Challenge Reuse**:
Capacidad de copiar un **Challenge** completo (con sus jugadas, **Hints** y configuracion) hacia otra **Mission**. Crea una copia independiente, conserva trazabilidad de origen como metadata de solo lectura y deja un evento auditable.
_Avoid_: referencia compartida viva, clonado en runtime, copia parcial ambigua

## Flagged Ambiguities

**Stage / Etapa**:
Termino retirado para el diseno. La estacion jugable es un **Challenge**; la unidad puntuable es una **Question** o una **Search**.

**Node / Arbol jugable**:
No existe arbol de nodos jugables ni el viejo **Mission Node**. La recursion vive solo en el arbol organizativo de **Sections**, que es inerte. Lo jugable es el flatten lineal de los **Challenges** hoja; no hay anidacion de jugadas ni herencia por niveles.

**Stage (cross-context)**:
En `scoring-monitoring` y `session-management` el credito por "stage" corresponde a una jugada (**Question** o **Search**) de este contexto, no a un **Challenge** completo.

## Example Dialogue

Dev: "Entonces una Mission es un arbol de nodos?"
Experto: "Solo para organizar. Hay un Composite de Sections recursivas pero inertes; lo jugable es el flatten lineal de los Challenges hoja. Ninguna jugada se anida."

Dev: "Una Section puede contener otra Section?"
Experto: "Si, recursivamente. Pero la Section no tiene juego, tiempo ni dificultad: solo agrupa y ordena."

Dev: "Si quiero 5 preguntas, creo 5 etapas?"
Experto: "No. Creas un Trivia Challenge y le agregas 5 Questions. Tipo, dificultad y tiempo se setean una vez en el Challenge."

Dev: "Una Question puede tener respuesta de texto libre?"
Experto: "No. Tiene entre 2 y 4 Choices y exactamente una correcta, estilo Kahoot."

Dev: "Un Treasure Hunt Challenge tiene una sola busqueda?"
Experto: "Puede tener varias Searches en orden, cada una con su pista y su QR esperado."

Dev: "La Section agrupa Challenges como un nodo padre?"
Experto: "Si, es un composite que los contiene y puede anidar otras Sections, pero inerte: sin juego, tiempo ni dificultad, y no altera el orden lineal del flatten."

Dev: "La dificultad vive en la Mission?"
Experto: "No. Se define en el Challenge, y opcionalmente por jugada. La Mission la deriva solo para mostrar."

Dev: "El tiempo del Challenge se hereda hacia las jugadas en cadena?"
Experto: "No hay cadena. La jugada usa su tiempo propio si lo tiene; si no, el del Challenge. Dos niveles, sin recursion."

Dev: "Y el puntaje por velocidad estilo Kahoot?"
Experto: "Vive en scoring-monitoring. Este contexto solo aporta Difficulty y la respuesta correcta; la sesion aporta el tiempo de resolucion."
