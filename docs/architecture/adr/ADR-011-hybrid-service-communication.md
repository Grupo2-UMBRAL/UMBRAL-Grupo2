# ADR-011: Usar comunicacion hibrida entre microservicios

## Status

Accepted

## Context

UMBRAL se implementara como microservicios reales desde el inicio. Al mismo tiempo, el enunciado original solo exige mensajeria asincrona para una parte del sistema, no para todo el flujo distribuido.

Forzar una arquitectura totalmente orientada a eventos agregaria complejidad innecesaria en coordinacion, debugging, consistencia y trazabilidad para el alcance actual del proyecto.

## Decision

- La comunicacion principal entre microservicios sera sincronica.
- La mensajeria asincrona con `RabbitMQ` se usara solo para responsabilidades secundarias y desacoplables.
- El flujo principal del negocio no dependera por defecto de coreografia distribuida.
- `Session Operations Service` conserva la autoridad del flujo operativo principal.

## Consequences

- El sistema sigue siendo de microservicios reales, pero sin sobreingenieria de eventos.
- Los contratos REST entre servicios se vuelven una pieza central del diseño.
- `RabbitMQ` se concentra en auditoria, historial, notificaciones o procesos que no requieran respuesta inmediata.
- La complejidad operativa y cognitiva del sistema baja respecto a una arquitectura totalmente asincrona.
