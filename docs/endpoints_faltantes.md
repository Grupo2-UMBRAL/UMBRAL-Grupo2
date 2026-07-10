# Endpoints faltantes — Rediseño "Diseño de misiones"

Relevado al reestructurar el dashboard de creación de misiones
(`src/apps/web/src/components/mission-builder-*`,
`mission-list-column.tsx`, `missions-admin-workspace.tsx`) para que coincida
con el layout de 3 paneles (rail de app · lista de misiones · editor de
estructura).

Regla del refactor web: **no se cambian los endpoints que el front ya
consume.** Este documento sólo lista los faltantes/gaps que el nuevo layout
deja expuestos, para resolver en el backend (`mission-management`) más
adelante.

Base actual: `/mission-management/api/mission-management`.

---

## 1. Conteos en el listado de misiones (BLOQUEANTE para paridad visual)

**Qué pide el diseño:** cada tarjeta de la columna de misiones muestra
`N etapas · Nm` (p. ej. *"5 etapas · 82m"*) más el estado Activa/Inactiva.

**Qué hay hoy:** `GET /missions` devuelve `MissionSummaryResponse`:

```
(Id, Name, MaximumDurationMinutes, IsActive)
```

No trae ningún conteo de estructura. Hoy la duración y el estado sí se
renderizan; **`N etapas` no se puede mostrar** sin pedir el detalle completo
de cada misión (`GET /missions/{id}`), lo que serían N requests al abrir la
lista.

**Falta uno de:**

- Enriquecer `MissionSummaryResponse` con `SectionCount` y `ChallengeCount`
  (idealmente calculado en la query `ListMissions`), **o**
- un endpoint dedicado `GET /missions/summary` que devuelva la misma lista con
  esos conteos.

> Nota de vocabulario: en la lista, **"etapa" = sección de nivel raíz** del
> modelo de autoría (Section/Challenge/Play), no la `MissionStage` de runtime
> (que vive en `/missions/{id}/stages` y la usa el flujo de operador). Definir
> con producto qué conteo se muestra (secciones vs. retos de nivel raíz).

**Estado en el front:** `MissionListColumn` acepta un prop opcional
`stageCounts?: Record<string, number>`; hoy se deja `undefined` y la tarjeta
cae al fallback `"{duración}m"`. En cuanto exista el conteo en el contrato,
sólo hay que poblar ese prop.

---

## 2. Catálogo de ítems reutilizables (mejora de performance/correctitud)

**Qué pide el diseño:** el modal *"Agregar reto / sección"* lista retos y
secciones de **otras** misiones para reutilizarlos (clonar).

**Qué hace hoy:** `reuse-item-modal.tsx` pide el detalle completo de **todas**
las demás misiones (`GET /missions/{id}` × N, en paralelo) y camina el árbol en
el cliente para juntar los ítems. Funciona, pero:

- es O(N) requests cada vez que se abre el modal,
- transfiere árboles completos (preguntas, opciones, pistas) sólo para mostrar
  un título + meta.

**Falta:** un endpoint plano de reutilizables, p. ej.

- `GET /missions/reusable-items?kind=challenge`
- `GET /missions/reusable-items?kind=section`

devolviendo por ítem: `{ id, kind, title, missionId, missionName, gameType,
difficulty, questionCount|searchCount }` (lo justo para la tarjeta del modal).
El clon en profundidad seguiría trayendo el detalle sólo del ítem elegido.

---

## 3. Eliminar misión (gap funcional, no está en el diseño)

Hoy se puede **crear / actualizar / activar / desactivar** una misión, pero no
existe borrado. El nuevo layout tiene la lista siempre visible, así que un
`DELETE /missions/{missionId}` (o soft-delete) es la acción natural que falta.
No es bloqueante; confirmar con producto si el ciclo de vida es
sólo activar/desactivar.

---

## 4. (Contexto) Endpoints ya existentes que el editor NO usa

Para dejar claro el alcance: el editor de autoría guarda la estructura
completa con `PUT /missions/{missionId}` (reemplazo total del árbol de items).
No usa los endpoints por-stage (`/missions/{id}/stages`, `/stages/{id}/...`),
que pertenecen al modelo de runtime/operador. No hay nada que agregar acá — se
documenta sólo para evitar confundir "etapa de autoría" con "MissionStage".

---

## Resumen

| # | Falta | Prioridad | Desbloquea |
|---|-------|-----------|-----------|
| 1 | Conteos (`SectionCount`/`ChallengeCount`) en `GET /missions` | Alta | `N etapas` en las tarjetas de la lista |
| 2 | `GET /missions/reusable-items?kind=` | Media | Modal de reutilización sin N fetches |
| 3 | `DELETE /missions/{id}` | Baja | Borrar misiones desde la consola |
