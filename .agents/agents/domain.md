# Domain Docs

Este repo usa un unico conjunto compartido de documentos de dominio para varios bounded contexts.

## Leer antes de explorar

- [../../docs/product/ers.md](../../docs/product/ers.md)
- [../../CONTEXT-MAP.md](../../CONTEXT-MAP.md)
- [../../src/services/mission-management/CONTEXT.md](../../src/services/mission-management/CONTEXT.md)
- [../../src/services/session-management/CONTEXT.md](../../src/services/session-management/CONTEXT.md)
- [../../src/services/scoring-monitoring/CONTEXT.md](../../src/services/scoring-monitoring/CONTEXT.md)
- [../../src/services/user-management/CONTEXT.md](../../src/services/user-management/CONTEXT.md)
- `docs/architecture/adr/` cuando exista y sea relevante

## Uso esperado

- El `ERS` define alcance y requerimientos.
- `CONTEXT-MAP.md` define relaciones entre bounded contexts.
- Cada `src/services/*/CONTEXT.md` define el vocabulario permitido en su contexto.

## Regla principal

Cuando un termino del ERS choque con el lenguaje del contexto, priorizar senalar la discrepancia y no mezclar conceptos.
