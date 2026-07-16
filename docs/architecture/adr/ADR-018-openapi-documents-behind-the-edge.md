# ADR-018: Documentos OpenAPI detrás del edge proxy

## Status

Accepted (complementa a [ADR-015](ADR-015-framework-free-shared-kernel.md))

## Context

Las cuatro APIs deben exponer sus endpoints de forma navegable y **probable a mano** en la demo
publicada. Desde .NET 9 la plantilla ya no trae Swashbuckle: `Microsoft.AspNetCore.OpenApi` genera el
documento y la UI es un paquete aparte que se elige por separado.

Tres hechos del entorno condicionan el diseño, y ninguno es evidente desde el código:

1. Un service **construye su documento a partir del request que está sirviendo**. Detrás del edge eso
   significa anunciar `http://user-management-service:8080` — la dirección interna del contenedor, que
   ningún navegador alcanza. La UI carga, pero cada "Send Request" falla.
2. YARP borra el prefijo de ruta (`PathRemovePrefix`) y **no** lo publica en `X-Forwarded-Prefix`: ese
   header sale de `Request.PathBase`, y el edge no tiene PathBase.
3. El source generator de comentarios XML es frágil de una forma silenciosa: si falta cualquiera de sus
   condiciones, **todas** las descripciones desaparecen y el proyecto compila y sirve igual.

## Decision

### El documento es el contrato; la UI es intercambiable

`Microsoft.AspNetCore.OpenApi` genera el documento. La UI (hoy `Scalar.AspNetCore` en `/swagger`) es un
detalle reemplazable por Swagger UI u otra sin tocar controllers ni DTOs. Las decisiones de valor viven
en el documento, no en el renderizador.

### El edge no publica documento propio

Sus únicos endpoints son `/`, `/edge-info` y `/health`; las rutas proxiadas nunca pasan por ApiExplorer,
así que su documento solo contendría ruido. El edge sirve **una** página de referencia multi-source
(`MapUmbralApiReferenceHub`) que lista los cuatro documentos, cada uno pedido **a través del propio edge**
— lo que los mantiene same-origin y evita CORS. La demo tiene una URL, no cuatro.

### `servers[]` se deriva de los `X-Forwarded-*`

El transformer de `AddUmbralDefaults` reescribe `servers[]` cuando llega `X-Forwarded-Prefix`; si no llega,
el documento se pidió directo al service y el default del framework ya es correcto. Los dos caminos
funcionan sin bifurcar código.

**Los `paths` no se tocan.** OpenAPI resuelve una llamada como `server + path`, así que el prefijo viaja en
el server (`http://host/user-management` + `/api/operators`). Duplicarlo en ambos lo rompería.

El edge setea `X-Forwarded-Prefix` **explícitamente por ruta**, junto a `{"X-Forwarded": "Set", "Prefix": "Off"}`.
Ese `"Prefix": "Off"` no es opcional: el transform `X-Forwarded` que YARP aplica por defecto toma el valor
del PathBase y, al no encontrarlo, **borra el header como medida anti-spoofing**, pisando el nuestro.

### `AddOpenApi` se llama desde cada `.Api`, no desde ServiceDefaults

ServiceDefaults aporta la configuración compartida como extensión sobre `OpenApiOptions`
(`AddUmbralDefaults`), pero **la llamada a `AddOpenApi` vive en cada `Program.cs`**. El source generator
solo intercepta call sites del proyecto que compila; envolverlo en ServiceDefaults compila, corre y
descarta todas las descripciones.

Para que los comentarios XML lleguen al documento hacen falta **cuatro** condiciones simultáneas:

1. `GenerateDocumentationFile` (en `Directory.Build.props`, para el `.Api` y el `.Application`).
2. `PackageReference` **directo** a `Microsoft.AspNetCore.OpenApi` en cada `.Api` — llega transitivo vía
   ServiceDefaults, pero sus build targets **no**, y son los que alimentan los `.xml` al generador.
3. `InterceptorsNamespaces` en cada `.Api`, o el build falla con `CS9137`.
4. El call site de `AddOpenApi` dentro del `.Api`.

### Alcance de la documentación

Se documentan los **DTOs de contrato** (request/response), no todo el código: `CS1591` queda silenciado.
Los comandos internos de MediatR que no se bindean desde la red quedan fuera.

## Consequences

Positivas:

- La demo publicada es ejecutable, no solo navegable: `servers[]` apunta a una URL alcanzable.
- Una sola entrada (`/swagger` en el edge) para las cuatro APIs.
- El acceso directo por puerto sigue funcionando, útil en desarrollo.
- Las descripciones salen del código, así que envejecen con él en vez de en un documento aparte.

Coste / notas:

- **Acoplamiento edge ↔ services**: el contrato de `X-Forwarded-Prefix` es una convención compartida. Si
  alguien agrega una ruta al edge y olvida los dos transforms, el swagger de ese service queda
  inejecutable, y el síntoma (una URL rara en un recuadro) no señala la causa.
- **`X-Forwarded-Host` se confía sin validar.** Es deliberado y acotado: solo cambia la URL que *muestra*
  la página de referencia; no interviene en routing ni en autorización. La mitigación, si esto sale del
  ámbito académico, es una allowlist de hosts conocidos.
- Las cuatro condiciones del XML son una trampa de mantenimiento. Se mitiga con un test por service
  (`OpenApiDocument_DescribesSchemaPropertiesFromXmlComments`) que falla si se rompe cualquiera.
- Las reglas de FluentValidation y de `PasswordPolicy` no llegan al schema; se describen en prosa en los
  `<param>`. El documento no puede expresarlas hoy.

## Guardrails

- El edge nunca publica documento propio ni duplica las rutas de los services.
- El prefijo viaja en `servers[]`, nunca en los `paths`.
- Toda ruta de API nueva en el edge lleva los dos transforms: `X-Forwarded`/`Prefix: Off` y el
  `RequestHeader` con su prefijo. Los hubs SignalR no los llevan: no tienen documento.
- `AddOpenApi` se llama desde el `.Api`. Si alguien lo mueve a ServiceDefaults "para no repetir", las
  descripciones desaparecen sin aviso.

## Related

- [ADR-015](ADR-015-framework-free-shared-kernel.md): qué puede vivir en el shared kernel.
- Deuda de contratos detectada al documentar: fuera del repo, `findings/umbral-deuda-logica.md`.
