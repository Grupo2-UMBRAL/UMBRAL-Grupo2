# ADR-008: Usar SignalR para comunicación en tiempo real

## Status

Accepted

## Context

UMBRAL requiere actualización observable en tiempo real para ranking, cambios de estado, liberación de pistas, avance de etapas, monitoreo y reconexión. El ERS ya fija esta necesidad y además concreta `SignalR` como tecnología objetivo.

Implementar tiempo real con una solución diferente sin una razón fuerte agregaría variación innecesaria respecto a la especificación actual.

## Decision

- La comunicación en tiempo real del proyecto se implementa con `SignalR`.
- `SignalR` se usa para eventos operativos visibles por web y móvil.
- La solución debe contemplar reconexión automática y resincronización al reconectar.

## Consequences

- El backend debe exponer hubs o adaptadores equivalentes sobre `SignalR`.
- Frontend web y móvil deben modelar suscripciones, reconexión y refresh de estado.
- Las decisiones de UX en paneles y tablero deben asumir conectividad en tiempo real como parte del comportamiento normal del sistema.
