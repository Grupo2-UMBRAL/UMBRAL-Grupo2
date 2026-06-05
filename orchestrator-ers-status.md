# Estado ERS vs tickets completados

Fecha de corte: 2026-06-02

## Criterio usado

- Fuente funcional: `README/ERS UMBRAL .md`.
- Fuente operativa: proyecto de Linear `UMBRAL MVP Backlog`.
- El ERS academico contiene `CU-01` a `CU-28`, pero el backlog de ejecucion fue normalizado en `26` historias base numeradas `[01]` a `[26]`.
- Cuando una historia fue dividida en tickets `A/B`, se considero **lista** solo si todos sus derivados necesarios estaban en `Done`.
- Tickets tecnicos fuera de esa numeracion, como `UMB-41`, no cuentan para el avance de historias del ERS.
- Tickets cancelados por refactor de alcance, como `UMB-30`, no cuentan como completos por si solos; su historia solo se considera lista si sus reemplazos quedaron cerrados.

## Resumen

- Historias base del ERS consideradas: `26`
- Historias listas: `13`
- Historias no completas: `13`
- Porcentaje no completo: `50.00%`

## Estado por historia base

| Historia | Estado | Evidencia Linear |
| --- | --- | --- |
| 01 | Lista | `UMB-5` |
| 02 | Lista | `UMB-6` |
| 03 | Lista | `UMB-7` |
| 04 | Lista | `UMB-8` |
| 05 | Lista | `UMB-9` |
| 06 | Lista | `UMB-10` |
| 07 | Lista | `UMB-11` |
| 08 | Lista | `UMB-12` |
| 09 | Lista | `UMB-13` |
| 10 | Lista | `UMB-15`, `UMB-40`, `UMB-33` |
| 11 | Lista | `UMB-16` y `UMB-34` done
| 12 | Lista | `UMB-17` backlog |
| 13 | No completa | `UMB-18` backlog |
| 14 | No completa | `UMB-19` backlog |
| 15 | No completa | `UMB-20` backlog |
| 16 | No completa | `UMB-21` backlog |
| 17 | No completa | `UMB-29` y `UMB-39` done, `UMB-22` backlog |
| 18 | No completa | `UMB-23` backlog |
| 19 | No completa | `UMB-24` done, `UMB-35` backlog |
| 20 | No completa | `UMB-25` backlog, `UMB-36` backlog |
| 21 | No completa | `UMB-26` backlog |
| 22 | No completa | `UMB-27` backlog |
| 23 | No completa | `UMB-28` backlog |
| 24 | Lista | `UMB-37`, `UMB-38` reemplazan `UMB-30` cancelado |
| 25 | Lista | `UMB-31` |
| 26 | Lista | `UMB-32` |

## Lectura rapida

- El bloque `01` a `10` ya quedo cubierto.
- El bloque `11` a `23` sigue mayormente pendiente, con avances parciales en `11`, `17` y `19`.
- El bloque `24` a `26` quedo cubierto.

## Nota importante

Si quieres el mismo analisis pero usando los `CU-01..CU-28` literales del ERS en vez de las `26` historias base del backlog, hay que rehacer el mapeo uno a uno porque Linear no sigue esa numeracion de forma directa.
