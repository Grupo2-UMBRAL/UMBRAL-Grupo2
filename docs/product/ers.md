# ERS Normalizado

Documento operativo derivado de [../../README/ERS UMBRAL .md](../../README/ERS%20UMBRAL%20.md) para consumo de agentes.

## Proposito

UMBRAL es una plataforma para operar experiencias narrativas inmersivas en tiempo real con dos tipos de juego:

- `Treasure Hunt`
- `Trivia`

Existen dos superficies principales:

- web para `Administrator` y `Operator`
- movil para `Participant`

## Decisiones estables del enunciado

- Arquitectura limpia o hexagonal.
- Separacion CQRS implementada con `MediatR`.
- `CQRS` se aplica a nivel logico de comandos y consultas, sin separar bases de datos de escritura y lectura.
- Persistencia relacional.
- Comunicacion en tiempo real implementada con `SignalR`.
- Mensajeria asincrona.
- Roles `Administrator`, `Operator` y `Participant`.
- Identidad y acceso gestionados con `Keycloak`.
- Autenticacion basada en tokens `JWT` emitidos por `Keycloak`.
- Autorizacion basada en roles y permisos definidos en `Keycloak` y consumidos desde los claims del token.
- La experiencia del participante ocurre en movil.
- El diseno reusable de la mision esta separado de la operacion en vivo.

## Bounded contexts esperados

- `Mission Design`
- `Session Operations`
- `Scoring and Audit`
- `Identity and Access`

## Reglas de negocio transversales

- Una mision debe estar activa para usarse en sesiones.
- Una sesion no inicia sin equipos registrados.
- No se aceptan evidencias en sesion pausada, finalizada o cancelada.
- Las etapas progresan de forma lineal por equipo.
- El ranking se ordena por puntaje y desempata por tiempo de resolucion.
- Las penalizaciones deben registrar motivo y momento.
- Al finalizar la sesion se revelan pistas y soluciones.

## Decisiones aclaradas del ERS

### Mission Design para primer release

- El diseno reusable de una `Mission` se modela, para primer release, como una lista lineal de `Mission Stages`.
- `Hint` pertenece directamente a un `Mission Stage`.
- `Mission Node` se conserva solo como concepto de modelado futuro. No introduce contrato funcional, pantalla, API ni persistencia propia en primer release.
- `Substage` queda fuera de alcance en primer release. Si algun texto del ERS detallado menciona subetapas, debe leerse como expansion futura y no como capacidad obligatoria del MVP.
- `Stage Template Reuse` queda fuera de alcance en primer release. La unidad reusable soportada es la `Mission` completa, no una etapa o bloque parcial reutilizable entre misiones.
- Las decisiones operativas de flujo en sesion para primer release actuan sobre `Mission Stages`, no sobre `Substages`.

### Trivia

- La validacion de respuestas de `Trivia` ocurre automaticamente por defecto.
- El `Operator` puede corregir el resultado cuando detecte que la respuesta enviada corresponde a una alternativa valida.
- No debe modelarse `Trivia` como un flujo donde toda evidencia entra primero en estado `Pending` para revision humana obligatoria.

## Uso esperado

Este archivo resume el ERS para trabajo diario. El documento academico completo sigue siendo la referencia funcional detallada.
