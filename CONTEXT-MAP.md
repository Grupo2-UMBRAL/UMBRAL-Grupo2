# Context Map

## Contexts

- [Mission Management](./src/services/mission-management/CONTEXT.md) - define el diseno reusable de misiones como rutas lineales de Challenges jugables y sus jugadas (Questions / Searches).
- [Session Operations](./src/services/session-management/CONTEXT.md) - opera sesiones en vivo, equipos, estado y el Session Flow efectivo (lista lineal de Plays).
- [Scoring and Monitoring](./src/services/scoring-monitoring/CONTEXT.md) - calcula puntajes, registra penalizaciones, mantiene ranking e historial auditable.
- [User Management](./src/services/user-management/CONTEXT.md) - contexto de soporte para autenticacion, autorizacion y administracion de usuarios operativos.

## Relationships

- **Mission Design -> Session Operations**: Session Operations usa una Mission activa como plantilla para crear una LiveSession y derivar su Session Flow aplanando (flatten depth-first) las Questions / Searches de la Mission en una lista ordenada de Plays.
- **Session Operations -> Scoring and Monitoring**: Session Operations entrega evidencias validadas y penalizaciones para calculo sincrono de puntaje, y ademas emite hechos relevantes para trazabilidad.
- **Scoring and Monitoring -> Session Operations**: Scoring and Monitoring devuelve resultados de puntaje para el flujo observable de la sesion y mantiene el historial auditable como preocupacion separada.
- **User Management -> Mission Design**: User Management provee identidades y roles para autorizar acciones administrativas sobre misiones.
- **User Management -> Session Operations**: User Management provee identidades y roles para autorizar acciones operativas y acceso participante.

## Atomo cruzado

La unidad puntuable tiene un solo nombre cruzando tres contextos: una `Question` / `Search` autoreada en **Mission Design** -> (flatten) -> **Play** en **Session Operations** -> **Play Credit** en **Scoring and Monitoring**. Un unico `PlayId` la identifica en los tres, eliminando la capa de traduccion `stage`/`play`. La respuesta correcta de Trivia (`CorrectChoiceId`) viaja solo server-to-server (mission -> session) y nunca al jugador.
