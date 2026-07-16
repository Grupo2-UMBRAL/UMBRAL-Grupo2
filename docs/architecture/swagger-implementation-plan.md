# Plan de implementación: documentación OpenAPI/Swagger

> Registro de ejecución de la rama `feature/swagger-endpoint-documentation`.
> **Las decisiones de arquitectura viven en [ADR-018](adr/ADR-018-openapi-documents-behind-the-edge.md)**;
> este documento solo deja el rastro de qué se aprobó y qué se hizo.

## Alcance aprobado

- Publicar un documento OpenAPI nativo en `/openapi/v1.json` para Mission, Session, Scoring y User Management.
- Publicar una referencia interactiva Scalar en `/swagger`.
- Centralizar la configuración común en `Umbral.ServiceDefaults`.
- Declarar el esquema Bearer JWT y metadatos comunes del documento.
- Mantener fuera de OpenAPI los health checks y hubs SignalR.
- Verificar con pruebas de contrato que cada host publica OpenAPI y sus rutas REST.

## Desviaciones respecto del plan original

Tres cosas se descubrieron al verificar contra el stack levantado y cambiaron el alcance. El detalle y el
porqué están en el ADR-018; acá queda el registro:

1. **El edge no publica documento propio.** El plan asumía `MapUmbralOpenApi()` también en el edge proxy.
   Ese documento solo contenía `/`, `/edge-info` y `/health` — las rutas proxiadas nunca pasan por
   ApiExplorer. Se reemplazó por una referencia Scalar multi-source que lista los cuatro documentos: una
   sola URL de entrada para la demo.
2. **`servers[]` detrás del edge estaba roto.** Cada service anunciaba su dirección interna de contenedor
   (`http://user-management-service:8080`), inalcanzable desde el navegador: la UI cargaba pero ningún
   request se podía ejecutar. Se resolvió derivando `servers[]` de los `X-Forwarded-*`, con el edge
   seteando `X-Forwarded-Prefix` por ruta.
3. **La configuración compartida no podía envolver `AddOpenApi`.** El plan decía "centralizar en
   ServiceDefaults". El source generator de comentarios XML solo intercepta call sites del proyecto que
   compila, así que la llamada quedó en cada `Program.cs` y ServiceDefaults aporta la configuración como
   extensión sobre `OpenApiOptions` (`AddUmbralDefaults`). Envolverla descartaba todas las descripciones
   en silencio.

Además se sumó, fuera del plan original:

- **Comentarios XML en los DTOs de contrato**, que es de donde salen las descripciones del documento.
- **`ActionResult<T>` en user-management**: sus 5 endpoints devolvían `Task<IActionResult>`, que borra el
  tipo de respuesta y dejaba los 200/201 sin schema.

## Estado

| Hito | Estado | Evidencia |
|---|---|---|
| Worktree y rama Gitflow | Completado | `feature/swagger-endpoint-documentation` |
| Configuración OpenAPI compartida | Completado | `Umbral.ServiceDefaults/OpenApiExtensions.cs` (`AddUmbralDefaults` sobre `OpenApiOptions`) |
| Scalar `/swagger` en los cuatro hosts | Completado | `MapUmbralOpenApi()` en las 4 APIs; el edge sirve `MapUmbralApiReferenceHub()` con los 4 documentos |
| Metadatos y seguridad JWT | Completado | Documento `v1` y esquema Bearer JWT en las 4 APIs |
| `servers[]` correcto detrás del edge | Completado | Transformer de `X-Forwarded-*` + transforms por ruta en el edge; verificado en el stack y con test por service |
| Documentación de los 4 hosts | Completado | Controllers anotados; DTOs de contrato con comentarios XML |
| Schemas de respuesta en user-management | Completado | `ActionResult<T>` en los 5 endpoints |
| Pruebas de contrato | Completado | Mission (6), Session (6), Scoring (6), User (7). Cubren: publica documento, `/swagger` HTML, health y hubs ausentes, anónimos sin Bearer, `servers[]` directo y proxiado, y descripciones XML presentes |
| Validación local | **Bloqueado por rama desactualizada** | `Invoke-RepositoryValidation.ps1` falla en `npm run lint` con 25 errores del web, en archivos que esta rama no toca. La rama está 14 commits detrás de `develop`, y `e6a3751 fix(validation): resolve lint and compiler warnings` ya los corrigió. Requiere traer `develop` antes del cierre |
| Prueba de mutación | Bloqueado | El repositorio no configura ni referencia una herramienta de mutación. Decisión pendiente, fuera del alcance de esta rama |

## Decisiones técnicas

Ver [ADR-018](adr/ADR-018-openapi-documents-behind-the-edge.md). En resumen:

- `Microsoft.AspNetCore.OpenApi` nativo alineado con `net10.0` genera el documento; la UI (`Scalar.AspNetCore`)
  es un detalle intercambiable.
- El edge no duplica las rutas de los services: sirve la referencia de los cuatro documentos, no un documento propio.
- El prefijo de ruta viaja en `servers[]`, nunca en los `paths`.

## Deuda detectada al documentar

Documentar obligó a leer los handlers y afloró deuda de contratos (tipos nullable que son obligatorios,
un campo siempre null, un campo muerto, un endpoint sin controller). **No se tocó nada de eso en esta rama.**
Está inventariado fuera del repo, en `findings/umbral-deuda-logica.md`.
