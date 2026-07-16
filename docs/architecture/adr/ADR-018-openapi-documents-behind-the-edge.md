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

### `/swagger` es el directorio del stack, y el edge no bifurca por entorno

`/` redirige a `/swagger`. Antes servía un "developer hub" HTML con links a cada recurso del stack, apagado
en Producción vía `IsProduction()`. Se eliminó, por dos razones que se refuerzan:

- **Nunca funcionó desplegado.** Sus links estaban hardcodeados a `localhost:16672`, `localhost:19888` y
  `localhost:3000`: al visitante le renderizaba links a *su propia* máquina. El gate tapaba una página rota.
- **El gate contradecía el objetivo del despliegue.** La demo corre con `ASPNETCORE_ENVIRONMENT=Production`
  justamente para parecerse a producción; ramificar por ese nombre hacía que dev y demo ejecutaran caminos
  distintos, que es lo contrario de lo buscado.

Con el hub fuera, **`IsProduction()` desaparece de todo `src/`**: dev y demo ejecutan el mismo código. La
demo no es producción — es un artefacto **inspeccionable**, y sus afordancias (referencia navegable, una
URL pública) valen en los dos entornos.

El reporte de coverage (`/coverage`) no necesita gate: `Hub:CoverageReportPath` solo lo setea
`docker-compose.dev.yml`, así que un edge desplegado cae al default vacío y no sirve nada. La condición ya
era por existencia, no por entorno.

El dashboard de Aspire **no se despliega ni se rutea por el edge**: corre con
`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` y el edge es la única puerta pública. En Azure, el logging
centralizado lo da el workspace de Log Analytics que ya declara `deployment/azure/main.bicep`.

### `servers[]` se deriva de los `X-Forwarded-*`

El transformer de `AddUmbralDefaults` reescribe `servers[]` cuando llega `X-Forwarded-Prefix`; si no llega,
el documento se pidió directo al service y el default del framework ya es correcto. Los dos caminos
funcionan sin bifurcar código.

**Los `paths` no se tocan.** OpenAPI resuelve una llamada como `server + path`, así que el prefijo viaja en
el server (`http://host/user-management` + `/api/operators`). Duplicarlo en ambos lo rompería.

El edge setea `X-Forwarded-Prefix` **explícitamente por ruta**, junto a `{"X-Forwarded": "Set", "Prefix": "Off"}`.
Ese `"Prefix": "Off"` no es opcional: el transform `X-Forwarded` que YARP aplica por defecto toma el valor
del PathBase y, al no encontrarlo, **borra el header como medida anti-spoofing**, pisando el nuestro.

### El edge llama `UseForwardedHeaders` antes de leer el scheme

El ingress de Azure Container Apps **termina TLS y habla http** con el contenedor. Sin
`UseForwardedHeaders`, el edge ve `Request.Scheme == "http"`, y la acción `Set` del transform `X-Forwarded`
**pisa** con ese valor el `X-Forwarded-Proto: https` que mandó el ingress. Como los services derivan
`servers[]` de ese header, la referencia desplegada anunciaría `http://<fqdn>/...` sobre una página `https`
y el navegador la bloquearía por mixed content: el mismo bug que este ADR resuelve, reaparecido en la nube.

El allowlist de proxies por defecto es loopback, y el ingress no lo es, así que hay que limpiarlo
(`KnownIPNetworks` / `KnownProxies`) o el header se ignora.

**El stack local no puede detectar esto**: es http de punta a punta, así que ahí `scheme == "http"` es la
respuesta *correcta* y todo pasa en verde. Se verifica simulando el ingress:

```bash
curl -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: ejemplo.azurecontainerapps.io" \
     http://localhost:7500/user-management/openapi/v1.json | grep '"url"'
# -> "url": "https://ejemplo.azurecontainerapps.io/user-management"
```

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
- **El edge no ramifica por `ASPNETCORE_ENVIRONMENT`.** Lo que deba existir solo en local se auto-gatea por
  presencia del recurso o de su config (como `/coverage`), no por el nombre del entorno. Un `IsProduction()`
  nuevo en el edge significa que dev y demo dejaron de ejecutar el mismo código.
- `UseForwardedHeaders` va antes de cualquier middleware que lea el scheme. Si alguien lo mueve después de
  `MapReverseProxy` o lo borra, el swagger desplegado vuelve a romperse por mixed content y el CI local
  seguirá en verde.
- Nada que corra sin autenticar se rutea por el edge: es la única puerta pública del despliegue.
- Toda ruta de API nueva en el edge lleva los dos transforms: `X-Forwarded`/`Prefix: Off` y el
  `RequestHeader` con su prefijo. Los hubs SignalR no los llevan: no tienen documento.
- `AddOpenApi` se llama desde el `.Api`. Si alguien lo mueve a ServiceDefaults "para no repetir", las
  descripciones desaparecen sin aviso.

## Related

- [ADR-015](ADR-015-framework-free-shared-kernel.md): qué puede vivir en el shared kernel.
- Deuda de contratos detectada al documentar: fuera del repo, `findings/umbral-deuda-logica.md`.
