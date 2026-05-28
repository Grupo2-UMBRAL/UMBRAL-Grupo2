
## UMBRAL

## Introducción

Propósito del Documento

El propósito de este documento es definir de manera detallada los requerimientos
funcionales y no funcionales de la plataforma UMBRAL. Este documento servirá como guía
para el desarrollo, implementación y pruebas de la solución, asegurando que se cumplan
los estándares de arquitectura limpia y operación en tiempo real exigidos.
Alcance del Proyecto
UMBRAL es una plataforma multiplataforma diseñada para la operación de
experiencias narrativas inmersivas de investigación interactiva. La solución soporta dos
tipos de juego: Búsqueda del Tesoro y Trivia. Permite el diseño de misiones, la gestión de
sesiones en vivo, la coordinación de equipos, la validación de evidencias y la supervisión
del progreso mediante tableros actualizados en tiempo real.
El sistema cuenta con dos clientes diferenciados:
● Aplicación web desarrollada en React para los roles Administrador y Operador.
● Aplicación móvil desarrollada en React native mediante accesible desde
dispositivos móviles. para los usuarios de rol participante. Toda la experiencia de
juego del equipo ocurre exclusivamente en la interfaz móvil.


## Descripción General

## Objetivo General
Desarrollar una aplicación multiplataforma para la operación en tiempo real de
experiencias de investigación inmersiva. La solución debe aplicar principios de arquitectura
limpia y hexagonal, modelado del dominio, separación CQRS, persistencia relacional,
comunicación en tiempo real y mensajería asíncrona
## Objetivos Específicos

● Diseño Arquitectónico: Diseñar una arquitectura del sistema que separe con total
claridad el dominio, la aplicación, la infraestructura y las interfaces externas.


● Modelado del Dominio: Representar el problema de negocio mediante entidades,
agregados, repositorios, value objects y servicios de dominio.
● Interfaz Web: Implementar una interfaz web funcional en React para las tareas de
administración y operación de sesiones, destinada a los roles Administrador y
## Operador.
● Interfaz Móvil: Implementar una aplicación móvil mediante react native accesible
desde dispositivos móviles en React para la experiencia de juego del rol
Participante, con soporte para escaneo de QR y visualización de mapas estáticos.
● Lógica de Aplicación: Construir los casos de uso empleando MediatR y el patrón
CQRS, diferenciando estrictamente entre comandos de escritura y consultas de
lectura.
● Persistencia de Datos: Gestionar la información del sistema en PostgreSQL
mediante Entity Framework Core.
● Sincronización en Tiempo Real: Incorporar comunicación basada en WebSockets
con SignalR para que los cambios en la operación se reflejen instantáneamente.
● Desacoplamiento de Procesos: Utilizar colas de mensajería con RabbitMQ para
separar procesos secundarios.
● Aseguramiento de Calidad: Aplicar pruebas unitarias bajo criterios estrictos de
cobertura.
● Buenas Prácticas: Demostrar la aplicación de principios SOLID y patrones de
diseño en componentes reales.
● Despliegue y Automatización: Empaquetar la solución en contenedores Docker y
automatizar las validaciones mediante un pipeline de integración continua.
Tipos de Juego
UMBRAL soporta dos tipos de juego con comportamiento diferenciado. El tipo de
juego se define al crear la misión y determina cómo se validan las evidencias durante la
sesión.
Búsqueda del Tesoro
● La evidencia se valida mediante el escaneo de un código QR desde la app móvil del
equipo.
● Cada etapa tiene un QR con un hash único que el backend compara contra el
contenido escaneado.
● Si el QR coincide, la evidencia es aceptada automáticamente por el sistema.
● Las etapas son lineales y el avance es individual: cuando un equipo completa su
etapa actual, avanza automáticamente a la siguiente, los otros equipos siguen en su
etapa actual
● Si no existe una etapa siguiente, la sesión finaliza automáticamente.
## Trivia
● La evidencia es una respuesta de texto enviada por el equipo desde la app móvil.


● La respuesta se valida automáticamente al ser registrada por el sistema. El operador
puede validar manualmente las respuestas que considere correctas desde el panel
web(errores de escritura, sinónimos o casos ambiguos).
● No existe reclamación exclusiva de etapas. Todos los equipos pueden obtener
puntaje por la misma etapa.
● Las etapas también son lineales y el avance es individual.
Comportamiento Común a Ambos Tipos
● Al cambiar el estado de la sesión a Finalizada, todas las pistas y soluciones se
revelan automáticamente a todos los equipos.
● Se pueden crear pistas nuevas durante una sesión activa. El cambio se propaga en
tiempo real a los equipos.

## Requerimientos Funcionales

## Código Requerimiento
RF-01 El sistema debe permitir crear, editar, consultar y desactivar misiones.
RF-02 Cada misión debe tener un tipo de juego definido: BusquedaDelTesoro o Trivia.
RF-03 Cada misión debe permitir registrar etapas con orden lineal estricto, pistas y
tiempo máximo de ejecución.
RF-04 Las pistas deben poder tener coordenadas geográficas opcionales marcadas
por el administrador.
RF-05 El sistema debe permitir crear una sesión en vivo a partir de una misión activa.


RF-06 La sesión debe manejar los siguientes estados: Programada, Activa, Pausada,
Finalizada y Cancelada.
RF-07 El sistema debe permitir registrar equipos participantes y asociarlos a una
sesión mediante un código único.
RF-09 Las etapas son estrictamente lineales. Un equipo no puede enviar evidencia de
una etapa que no sea su etapa actual.
RF-10 Cuando un equipo completa su etapa actual, avanza automáticamente a la
siguiente etapa. Si no hay siguiente etapa, la sesión finaliza automáticamente.
RF-11 En Búsqueda del Tesoro la evidencia se valida mediante el escaneo de un
código QR desde la app móvil. El backend compara el contenido escaneado
contra el hash de la etapa.
RF-13 En Trivia la evidencia es una respuesta de texto que el sistema valida
automáticamente, en caso de errores de escritura o que requieran validación
manual, el operador las puede validar manualmente.
RF-14 El operador debe poder liberar pistas de forma manual o condicionada por
reglas de avance.
RF-15 El operador debe poder crear pistas nuevas durante una sesión activa. El
cambio debe propagarse en tiempo real a los equipos.


RF-16 Los equipos deben visualizar únicamente las pistas habilitadas
correspondientes a su etapa actual.
RF-17 Al cambiar el estado de la etapa Finalizada, todas las pistas y soluciones deben
revelarse automáticamente a todos los equipos.
RF-18 Los equipos deben poder enviar evidencias asociadas a su etapa actual de la
sesión.
RF-19 Cada envío de evidencia debe quedar registrado con fecha, equipo, sesión,
etapa y estado de validación.
RF-20 El sistema debe recalcular el puntaje del equipo cuando una evidencia sea
validada o cuando se aplique una penalización.
RF-21 El operador debe poder aplicar penalizaciones justificadas a un equipo
registrando motivo y momento.
RF-22 El ranking de la sesión debe mostrarse y actualizarse en tiempo real, ordenado
por puntaje y usando el tiempo de resolución como criterio de desempate.
RF-23 El panel del operador debe reflejar cambios de estado y eventos relevantes de
la sesión en tiempo real.


RF-24 La aplicación debe publicar eventos del dominio en RabbitMQ ante cambios
significativos como envío de evidencias o cambios de estado.
RF-25 Debe existir un historial de eventos de la sesión con trazabilidad mínima para
auditoría.
RF-26 La aplicación debe diferenciar funcionalidades por rol.
RF-27 Las consultas de misiones, sesiones, equipos y ranking deben realizarse de
forma separada a los comandos, siguiendo el patrón CQRS.
RF-28 La aplicación debe validar reglas de negocio antes de aceptar cambios de
estado o evidencias.
RF-29 La autenticación debe realizarse con un sistema de autenticación estándar
mediante JWT .
RF-30 La experiencia de juego del rol Equipo Participante debe desarrollarse
completamente en la app móvil.
RF-31 La app móvil debe soportar el escaneo de códigos QR desde la cámara del
dispositivo para el envío de evidencias en Búsqueda del Tesoro.
RF-32 La app móvil debe mostrar las coordenadas de las pistas en un mapa estático
cuando estén disponibles.


RF-33 El administrador debe poder activar y desactivar etapas de una misión
individualmente.
RF-34 El operador debe poder definir el flujo de etapas al crear una sesión,
seleccionando qué etapas de la misión participarán y en qué orden se
ejecutarán.
RF-35 Una etapa inactiva no puede ser incluida en el flujo de ninguna sesión.
RF-36 El operador debe poder desactivar etapas pendientes del flujo de una sesión en
estado Programada o Activa. La desactivación aplica únicamente a esa sesión y
no modifica la estructura original de la misión.
RF-38 El sistema debe garantizar que el flujo de una sesión mantenga al menos una
etapa activa en todo momento mientras la sesión no haya finalizado o
cancelado.

## Requerimientos No Funcionales
Requerimiento de Seguridad
● Control de Acceso: La aplicación debe diferenciar funcionalidades y permisos
mediante roles específicos para Administrador, Operador y Participante.
● Validación de Identidad: Los roles se leen del claim de roles del token JWT. Los
controllers del backend usan autorización basada en roles leídos del token.
Requerimiento de Usabilidad
● Interfaz Web: La interfaz web para Administrador y Operador debe ser clara,
utilizable y coherente con los flujos principales de navegación del sistema.
● Interfaz Móvil: La app móvil para los Participantes debe ofrecer una experiencia de
juego fluida, con soporte para escaneo de QR, visualización de pistas y mapas, y
actualización en tiempo real del tablero del equipo.
● Experiencia en Tiempo Real: El panel del operador y el del equipo deben reflejar
cambios de estado de manera observable e instantánea.


● Tecnología Frontend Web: React con TypeScript, separando componentes, hooks
y servicios de acceso a la API.
● Tecnología Frontend Móvil: React native con TypeScript
Requerimiento de Calidad
● Cobertura de Pruebas: El backend debe alcanzar una meta académica de
cobertura de pruebas unitarias, de integración y E2E de al menos el 90%.
● Robustez: La aplicación debe incorporar mecanismos consistentes de logging,
manejo global de excepciones y validaciones de negocio antes de aceptar cambios
de estado.
● Arquitectura: La solución debe seguir arquitectura hexagonal o limpia con
separación estricta de dominio, aplicación, infraestructura y capa de entrada.
Requerimiento de Disponibilidad
● Contenedorización: La solución completa debe ejecutarse localmente mediante
## Docker Compose.
● Persistencia Confiable: El almacenamiento se resuelve con PostgreSQL
gestionado mediante Entity Framework Core.
Requerimiento de Escalabilidad
● Separación de Responsabilidades: Se debe aplicar el enfoque CQRS con MediatR
para separar las consultas de lectura de los comandos de escritura, permitiendo un
escalado independiente si fuera necesario.
● Desacoplamiento mediante Eventos: La publicación de eventos del dominio en
RabbitMQ permite que procesos secundarios crezcan sin afectar el núcleo de la
plataforma.
Requerimiento de Mantenibilidad
● Independencia del Dominio: El núcleo de la lógica de negocio no debe depender
de infraestructura ni del framework web.
● Principios de Diseño: El código debe evidenciar los principios SOLID y los
patrones Strategy, State y quizás Facade.
● Modelado Táctico: Se deben usar entidades, agregados, repositorios y value
objects para mantener el código organizado.
Requerimiento de Portabilidad
● Entorno de Ejecución: La solución completa (frontend, backend, base de datos y
mensajería) debe poder ejecutarse localmente de forma idéntica mediante Docker
## Compose.
● Tecnología Base: Backend en .NET Core. App móvil en React native.
Requerimiento de Conectividad
● Comunicación Bidireccional: SignalR para ranking en tiempo real, liberación de
pistas y avance de etapas.
● Integración de Mensajería: Integración funcional con RabbitMQ para eventos de
auditoría y notificaciones.


● Conectividad Móvil: La app móvil se comunica con el backend mediante API REST y
SignalR client.
● Adaptadores Externos: La arquitectura debe definir claramente puertos y
adaptadores para la API HTTP y cualquier otra integración externa necesaria.
Requerimiento de Compatibilidad Móvil
● Cámara: La app móvil debe acceder a la cámara nativa del dispositivo para el escaneo de
## QR.
● Mapas: La app móvil debe mostrar coordenadas de pistas en un mapa estático sin tracking
en vivo del jugador.
Requerimiento de Resiliencia y Reconexión
● Reconexión automática nativa: Activar la función de reintentos automáticos que ya trae
SignalR por defecto para que el cliente intente reconectarse solo.
● Sincronización al conectar: Programar que, cada vez que el sistema detecte que recuperó
la conexión, realice una consulta automática al backend para poner al día el tablero del
equipo.

## Pruebas:
● Pruebas unitarias, de integración y de sistema se realizan para asegurar la calidad
del software con una cobertura mínima del 90% en el backend.

Reglas de Negocio
● Una misión solo puede utilizarse para crear sesiones si se encuentra activa.
● Una sesión no puede iniciar si no posee al menos un equipo registrado.
● No se deben aceptar evidencias si la sesión está pausada, finalizada o cancelada.
● Una pista no puede liberarse dos veces al mismo equipo para la misma etapa.
● Cada evidencia debe asociarse exactamente a un equipo, una sesión y una etapa.
● Toda penalización debe registrar motivo y momento de aplicación.
● El puntaje acumulado de un equipo no puede quedar sin trazabilidad de origen.
● El ranking debe ordenarse de mayor a menor puntaje y usar tiempo de resolución
como criterio de desempate cuando aplique.
● Los cambios de estado de la sesión deben respetar las siguientes transiciones
válidas: Programada → Activa → Pausada → Activa → Finalizada. Cualquier estado
activo puede transicionar a Cancelada excepto Finalizada.
● En Búsqueda del Tesoro, el contenido del QR escaneado debe coincidir con el hash
de la etapa para que la evidencia sea aceptada.
● Las etapas son estrictamente lineales. Un equipo no puede enviar evidencia de una
etapa distinta a su etapa actual de la sesión.
● Al cambiar el estado de la sesión a Finalizada, todas las pistas incluyendo las
marcadas como solución se revelan automáticamente a todos los equipos de esa
sesión.


● Las misiones tienen un tipo de juego definido en su creación que no puede
modificarse una vez que la misión tiene sesiones asociadas.
● Solo pueden desactivarse etapas con estado pendiente dentro del flujo de la sesión.
Una etapa que ya fue completada no puede desactivarse.
● El flujo de una sesión debe mantener al menos una etapa activa pendiente en todo
momento.
● La desactivación de una etapa aplica únicamente a esa sesión. La estructura original
de la misión y el is_active de mission_stages no se ven afectados.

Casos de uso
CU-01: Login
## ● Actores: Administrador, Operador, Participante
● Descripción: Cualquier usuario que intente acceder al sistema es redirigido a la
interfaz de inicio de sesión. El usuario ingresa sus credenciales y, si estas son
válidas, se emite un token JWT que contiene su rol. El cliente (web o móvil) recibe el
token y lo utiliza en todas las peticiones al backend. Finalmente, el backend valida el
token y extrae el rol para autorizar el acceso a los recursos correspondientes.
● Flujo principal:
- El usuario intenta acceder al sistema desde el cliente web o móvil.
- El sistema redirige al usuario a la interfaz de inicio de sesión.
- El usuario ingresa sus credenciales.
- El sistema valida las credenciales.
- El sistema emite un token JWT que contiene el rol del usuario.
- El cliente recibe el token y lo almacena para usarlo en todas las peticiones al
backend.
- El backend valida el token y extrae el rol para autorizar el acceso a los
recursos correspondientes.
- El usuario accede al sistema con los permisos de su rol.
● Flujo alterno — credenciales inválidas:
- El usuario ingresa credenciales incorrectas.
- El sistema no emite token.
- El sistema muestra un mensaje de error.
- El flujo regresa al paso 3 del flujo principal.
CU-02: Crear Misión
## ● Actores: Administrador
● Descripción: El administrador crea una nueva misión definiendo nombre,
descripción, dificultad, tipo de juego (BusquedaDelTesoro o Trivia) y tiempo máximo.
Al seleccionar BusquedaDelTesoro el sistema habilita un hash para validar las
etapas. Este caso de uso incluye obligatoriamente gestionar etapas y configurar
pistas.
● Flujo principal:
- El administrador accede al módulo de misiones.
- El administrador selecciona la opción de crear misión.


- El administrador ingresa nombre, descripción, dificultad, tipo de juego y
tiempo máximo.
- El sistema valida los datos ingresados.
- Si el tipo de juego es BusquedaDelTesoro el sistema habilita el campo hash
para las etapas.
- El administrador gestiona las etapas de la misión (incluye CU-04
obligatoriamente).
- El administrador configura las pistas de cada etapa (incluye CU-05
obligatoriamente).
- El sistema guarda la misión en estado Inactiva.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos inválidos:
- El administrador ingresa datos incompletos o incorrectos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 3 del flujo principal.
● Flujo alterno — datos de etapa inválidos:
- El administrador ingresa datos incorrectos en una etapa.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 6 del flujo principal.
● Flujo alterno — datos de pista inválidos:
- El administrador ingresa datos incorrectos en una pista.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 7 del flujo principal.
CU-03: Editar Misión
## ● Actores: Administrador
● Descripción: El administrador modifica los datos de una misión existente: nombre,
descripción, dificultad y tiempo máximo. El tipo de juego no puede modificarse si la
misión tiene sesiones asociadas. Puede también gestionar las etapas y pistas de la
misión.
● Flujo principal:
- El administrador accede al módulo de misiones.
- El administrador selecciona una misión existente.
- El administrador modifica nombre, descripción, dificultad o tiempo máximo.
- El sistema valida los datos modificados.
- El sistema guarda los cambios.
- El sistema muestra mensaje de éxito.
● Flujo alterno — intento de modificar el tipo de juego con sesiones asociadas:
- El administrador intenta modificar el tipo de juego de la misión.
- El sistema detecta que la misión tiene sesiones asociadas.
- El sistema no permite la modificación del tipo de juego.
- El sistema muestra mensaje informando que el tipo de juego no puede
modificarse.
- El administrador puede continuar editando otros campos.
● Flujo alterno — datos inválidos:
- El administrador ingresa datos incorrectos.
- El sistema muestra mensaje de error.


- El flujo regresa al paso 3 del flujo principal.
● Flujo alterno — gestión de etapas y pistas durante edición:
- El administrador decide gestionar etapas o pistas desde la edición.
- El flujo extiende hacia CU-04 o CU-05 según corresponda.
- Al finalizar retorna al flujo de edición de la misión.
CU-04: Gestionar Etapas
## ● Actores: Administrador
● Descripción: El administrador agrega, edita o elimina etapas de una misión. Cada
etapa tiene título, descripción, orden lineal y puntaje base. Si el tipo de juego es
BusquedaDelTesoro, cada etapa requiere un hash único. Las etapas pueden tener
subetapas mediante una relación jerárquica con la misma estructura. El orden de las
etapas determina la secuencia lineal del juego y no puede saltarse. El administrador
puede activar y desactivar etapas individualmente. Una etapa desactivada no
aparece disponible para el operador al definir el flujo de una sesión.
● Flujo principal — agregar etapa:
- El administrador accede a la gestión de etapas de una misión.
- El administrador selecciona agregar nueva etapa.
- El administrador ingresa título, descripción, order_index y puntaje base.
- Si el tipo de juego es BusquedaDelTesoro el administrador ingresa el hash
único de la etapa.
- El sistema valida los datos.
- El sistema guarda la etapa en estado activo (is_active = TRUE).
- El administrador decide si agrega otra etapa, una subetapa o finaliza.
- El sistema muestra mensaje de éxito.
● Flujo principal — desactivar etapa:
- El administrador selecciona una etapa activa de la misión.
- El administrador selecciona la opción de desactivar.
- El sistema cambia is_active a FALSE.
- La etapa ya no aparece disponible para el operador al definir el flujo de
sesiones.
- El sistema muestra mensaje de éxito.
● Flujo principal — activar etapa:
- El administrador selecciona una etapa inactiva de la misión.
- El administrador selecciona la opción de activar.
- El sistema cambia is_active a TRUE.
- La etapa vuelve a estar disponible para el operador al definir flujos de
sesiones.
- El sistema muestra mensaje de éxito.
● Flujo principal — agregar subetapa:
- El administrador selecciona una etapa existente como padre.
- El administrador selecciona agregar subetapa.
- El administrador ingresa los datos de la subetapa con la misma estructura
que una etapa.
- El sistema vincula la subetapa con la etapa padre mediante parent_stage_id.
- El sistema valida los datos.
- El sistema guarda la subetapa.


- El sistema muestra mensaje de éxito.
● Flujo principal — editar etapa:
- El administrador selecciona una etapa existente.
- El administrador modifica los datos que desee.
- El sistema valida los datos modificados.
- El sistema guarda los cambios.
- El sistema muestra mensaje de éxito.
● Flujo principal — eliminar etapa:
- El administrador selecciona una etapa existente.
- El administrador selecciona la opción de eliminar.
- El sistema elimina la etapa y sus pistas asociadas.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos inválidos al agregar o editar:
- El administrador ingresa datos incorrectos o incompletos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso de ingreso de datos correspondiente.
● Flujo alterno — hash duplicado en BusquedaDelTesoro:
- El administrador ingresa un hash que ya existe en otra etapa de la misma
misión.
- El sistema detecta la duplicidad.
- El sistema muestra mensaje de error indicando que el hash debe ser único.
- El administrador ingresa un hash diferente.

CU-05: Configurar Pistas
## ● Actores: Administrador
● Descripción: El administrador agrega, edita o elimina pistas asociadas a una etapa.
Cada pista tiene título y contenido. El administrador puede marcar una pista como
solución, en cuyo caso solo será visible al finalizar la sesión. El administrador puede
opcionalmente agregar coordenadas geográficas a la pista marcando un punto en un
mapa estático desde la interfaz web.
● Flujo principal — agregar pista:
- El administrador accede a la configuración de pistas de una etapa.
- El administrador selecciona agregar nueva pista.
- El administrador ingresa título y contenido de la pista.
- El administrador selecciona el modo de liberación: Manual o Automática.
- El administrador decide si marca la pista como solución (is_solution = TRUE
o FALSE).
- El administrador decide opcionalmente agregar coordenadas geográficas
marcando un punto en el mapa estático.
- El sistema valida los datos.
- El sistema guarda la pista.
- El sistema muestra mensaje de éxito.
● Flujo principal — editar pista:
- El administrador selecciona una pista existente.
- El administrador modifica los datos que desee incluyendo coordenadas o flag
de solución.
- El sistema valida los datos modificados.


- El sistema guarda los cambios.
- El sistema muestra mensaje de éxito.
● Flujo principal — eliminar pista:
- El administrador selecciona una pista existente.
- El administrador selecciona la opción de eliminar.
- El sistema elimina la pista.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos inválidos:
- El administrador ingresa datos incorrectos o incompletos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso de ingreso de datos.

CU-06: Desactivar Misión
## ● Actores: Administrador
● Descripción: El administrador desactiva una misión activa. Una misión desactivada
no puede usarse para crear nuevas sesiones. Si tiene sesiones activas, el sistema
no permitirá la acción
● Flujo principal:
- El administrador accede al módulo de misiones.
- El administrador selecciona una misión activa.
- El administrador selecciona la opción de desactivar.
- El sistema solicita confirmación.
- El administrador confirma la acción.
- El sistema cambia el estado de la misión a Inactiva.
- La misión ya no puede usarse para crear nuevas sesiones.
- El sistema muestra mensaje de éxito.
● Flujo alterno — misión con sesiones activas:
- El administrador intenta desactivar una misión.
- El sistema detecta que la misión tiene sesiones activas asociadas.
- El sistema no permite la acción.
- El sistema muestra mensaje informando que existen sesiones activas.
- El flujo termina sin cambios.
● Flujo alterno — administrador cancela la confirmación:
- El sistema solicita confirmación.
- El administrador cancela la acción.
- El flujo termina sin cambios.

CU-07: Consultar Misiones
## ● Actores: Administrador, Operador
● Descripción: El usuario consulta la lista de misiones disponibles. Puede filtrar por
nombre y tipo de juego. El Administrador ve todas las misiones. El Operador solo ve
las misiones activas disponibles para crear sesiones.
● Flujo principal — sin filtro:
- El usuario accede al módulo de consulta de misiones.
- El sistema muestra la lista de misiones disponibles según el rol.


- El Administrador ve todas las misiones. El Operador solo ve las misiones
activas.
- El usuario revisa la lista.
● Flujo alterno — con filtro:
- El usuario decide aplicar un filtro por nombre o tipo de juego.
- El usuario ingresa los criterios de búsqueda.
- El sistema busca misiones que coincidan con los criterios.
- Si encuentra resultados el sistema muestra las misiones filtradas.
- Si no encuentra resultados el sistema muestra mensaje indicando que no hay
coincidencias.
CU-08: Crear Sesión
## ● Actores: Operador
● Descripción: El operador crea una sesión en vivo seleccionando una misión activa.
Define nombre, código único de acceso, fecha programada y debe definir el flujo de
etapas seleccionando un subconjunto de las etapas activas de la misión,
asignándoles un orden de ejecución específico para esa sesión. No es obligatorio
incluir todas las etapas de la misión. La sesión se crea en estado Programada.
Incluye el registro de al menos un equipo participante para poder iniciarla
posteriormente.
● Flujo principal:
- El operador accede al módulo de sesiones.
- El operador selecciona la opción de crear sesión.
- El operador selecciona una misión activa.
- El operador ingresa nombre, código único de acceso y fecha programada.
- El sistema valida los datos iniciales.
- El operador define el flujo de etapas seleccionando un subconjunto de etapas
activas de la misión y asignándoles un order_index de ejecución específico
para esta sesión.
- El sistema valida que todas las etapas seleccionadas estén activas y
pertenezcan a la misión.
- El sistema guarda el flujo en session_stage_flow.
- El operador registra al menos un equipo participante (incluye CU-09).
- El sistema crea la sesión en estado Programada.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos iniciales inválidos:
- El operador ingresa datos incorrectos o incompletos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 4 del flujo principal.
● Flujo alterno — intento de incluir etapa inactiva en el flujo:
- El operador selecciona una etapa con is_active = FALSE para incluirla en el
flujo.
- El sistema rechaza la selección.
- El sistema muestra mensaje indicando que la etapa está inactiva y no puede
incluirse.
- El operador selecciona otra etapa activa.
● Flujo alterno — flujo de etapas vacío:


- El operador intenta crear la sesión sin definir ninguna etapa en el flujo.
- El sistema detecta que session_stage_flow está vacío.
- El sistema no permite continuar.
- El sistema muestra mensaje indicando que debe definir al menos una etapa
en el flujo.
● Flujo alterno — datos de equipo inválidos:
- El operador ingresa datos incorrectos para el equipo participante.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 9 del flujo principal.

CU-09: Registrar Equipo
## ● Actores: Operador
● Descripción: El operador registra equipos participantes en una sesión,
asignándoles un nombre y un código único de acceso. Un equipo queda vinculado a
la sesión. Este caso de uso puede ejecutarse durante la creación de la sesión o
después, siempre que la sesión no haya iniciado.
● Flujo principal:
- El operador accede al registro de equipos de una sesión en estado
## Programada.
- El operador ingresa nombre y código único de acceso del equipo.
- El sistema valida los datos.
- El sistema vincula el equipo a la sesión.
- El sistema muestra mensaje de éxito.
- El operador decide si registra otro equipo o finaliza.
● Flujo alterno — código de equipo duplicado:
- El operador ingresa un código que ya existe en el sistema.
- El sistema detecta la duplicidad.
- El sistema muestra mensaje de error indicando que el código debe ser único.
- El operador ingresa un código diferente.
● Flujo alterno — datos inválidos:
- El operador ingresa datos incompletos o incorrectos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 2 del flujo principal.
● Flujo alterno — sesión ya iniciada:
- El operador intenta registrar un equipo en una sesión que ya pasó al estado
## Activa.
- El sistema no permite la acción.
- El sistema muestra mensaje indicando que la sesión ya inició.

CU-10: Gestionar ciclo de vida de la Sesión
## ● Actores: Operador
● Descripción: El operador cambia el estado de la sesión respetando las transiciones
válidas: Programada → Activa → Pausada → Activa → Finalizada, y cualquier
estado activo → Cancelada. Cada transición dispara eventos en tiempo real hacia


todos los clientes conectados. Cuando la sesión pasa a Finalizada, el sistema revela
automáticamente todas las pistas y soluciones.
● Flujo principal — cambiar estado:
- El operador accede al panel de la sesión que tiene asignada.
- El operador consulta el estado actual de la sesión.
- El operador selecciona un estado válido al que desea transicionar.
- El sistema verifica que la transición es válida según las reglas definidas.
- El sistema ejecuta la transición.
- El sistema dispara eventos en tiempo real hacia todos los clientes
conectados.
- El sistema muestra mensaje de éxito.
● Flujo principal — transición a Finalizada:
- El operador transiciona la sesión al estado Finalizada.
- El sistema ejecuta la transición.
- El sistema revela automáticamente todas las pistas y soluciones a todos los
equipos.
- El sistema notifica en tiempo real a todos los clientes.
- El sistema muestra mensaje de éxito.
● Flujo alterno — transición inválida:
- El operador selecciona un estado al que no puede transicionar desde el
estado actual.
- El sistema detecta que la transición no es válida.
- El sistema no ejecuta el cambio.
- El sistema muestra mensaje indicando las transiciones válidas disponibles.
● Flujo alterno — intento de activar sin equipos registrados:
- El operador intenta transicionar la sesión a Activa.
- El sistema detecta que no hay equipos registrados.
- El sistema no permite la transición.
- El sistema muestra mensaje indicando que debe registrar al menos un
equipo.
● Flujo alterno — intento de activar sin flujo de etapas definido:
- El operador intenta transicionar la sesión a Activa.
- El sistema detecta que session_stage_flow está vacío.
- El sistema no permite la transición.
- El sistema muestra mensaje indicando que debe definir el flujo de etapas.
● Flujo alterno — sesión avanza automáticamente al completar última etapa:
- El sistema detecta que la etapa actual fue completada y no existe siguiente
etapa en el flujo.
- El sistema transiciona automáticamente la sesión a Finalizada sin
intervención del operador.
- El sistema revela todas las pistas y soluciones automáticamente.
- El sistema notifica en tiempo real a todos los clientes.

CU-11: Liberar Pista
## ● Actores: Operador
● Descripción: El operador libera una pista de la etapa actual hacia uno o todos los
equipos. La liberación puede ser manual o condicionada por reglas de avance


configuradas en la misión. Una pista no puede liberarse dos veces al mismo equipo
en la misma etapa. El cambio se refleja en tiempo real en el tablero del equipo en la
app móvil.
● Flujo principal — liberación manual:
- El operador accede al panel de la sesión activa.
- El operador selecciona la opción de liberar pista.
- El operador selecciona la pista que desea liberar de la etapa.
- El operador selecciona a quién liberar: un equipo específico o todos los
equipos.
- El sistema verifica que la pista no haya sido liberada previamente al equipo
seleccionado.
- El sistema registra la liberación en team_clue_releases.
- El sistema notifica en tiempo real al tablero del equipo en la app móvil.
- El sistema muestra mensaje de éxito.
● Flujo principal — liberación condicionada por reglas de avance:
- El operador accede al panel de la sesión activa.
- El operador identifica una pista con release_mode = Automatica.
- El sistema aplica automáticamente las reglas de avance configuradas.
- Si se cumple la condición el sistema libera la pista automáticamente.
- El sistema registra la liberación.
- El sistema notifica en tiempo real al tablero del equipo.
● Flujo alterno — pista ya liberada al equipo:
- El operador intenta liberar una pista a un equipo que ya la recibió.
- El sistema detecta que ya existe un registro en team_clue_releases para esa
combinación.
- El sistema no permite la liberación duplicada.
- El sistema muestra mensaje indicando que la pista ya fue liberada a ese
equipo.

CU-12: Crear Pista en Sesión activa
## ● Actores: Operador
● Descripción: El operador crea una pista nueva para una etapa durante una sesión
activa. La pista creada puede liberarse inmediatamente o quedar pendiente de
liberación manual. El cambio se propaga en tiempo real a los equipos conectados.
● Flujo principal:
- El operador asignado a la sesión activa accede al panel de la sesión.
- El operador selecciona la opción de crear pista nueva durante la sesión.
- El operador ingresa título, contenido, modo de liberación y opcionalmente
coordenadas.
- El operador decide si marca la pista como solución.
- El sistema valida los datos.
- El sistema guarda la nueva pista asociada a la etapa correspondiente.
- El operador decide si la libera inmediatamente o la deja pendiente de
liberación manual.
- El sistema propaga el cambio en tiempo real a los equipos conectados.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos inválidos:


- El operador ingresa datos incorrectos o incompletos.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 3 del flujo principal.
● Flujo alterno — operador no asignado intenta crear pista:
- Un operador no asignado a la sesión intenta crear una pista.
- El sistema detecta que el operador no tiene permisos sobre esa sesión.
- El sistema rechaza la acción.
- El sistema muestra mensaje de acceso denegado.

CU-13: Aplicar penalización
## ● Actores: Operador
● Descripción: El operador selecciona un equipo de la sesión activa y le aplica una
penalización registrando el motivo, los puntos a descontar y el momento de
aplicación. El sistema recalcula el puntaje del equipo y actualiza el ranking en tiempo
real.
● Flujo principal:
- El operador accede al panel de la sesión activa.
- El operador selecciona la opción de aplicar penalización.
- El operador selecciona el equipo a penalizar.
- El operador registra el motivo de la penalización y los puntos a descontar.
- El sistema valida que se haya registrado un motivo y el momento de
aplicación.
- El sistema aplica la penalización al equipo.
- El sistema recalcula el puntaje del equipo.
- El sistema actualiza el ranking en tiempo real.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos incompletos:
- El operador intenta aplicar la penalización sin registrar el motivo.
- El sistema detecta que falta información obligatoria.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 4 del flujo principal.

CU-14: Validar evidencia
## ● Actores: Operador
● Descripción: En sesiones de tipo Trivia, el operador revisa las respuestas de texto
enviadas por los equipos y decide si son correctas o incorrectas. Si la evidencia es
aceptada, el sistema recalcula el puntaje y evalúa si la etapa fue completada. Si es
rechazada, el operador puede registrar el motivo y decidir si aplica penalización.
● Flujo principal — evidencia aceptada:
- El operador accede al panel de la sesión activa de tipo Trivia.
- El operador selecciona la opción de validar evidencia.
- El operador selecciona una evidencia en estado Pendiente.
- El operador revisa la respuesta enviada por el equipo.
- El operador determina que la evidencia es correcta.
- El sistema marca la evidencia como Aceptada.


- El sistema recalcula el puntaje del equipo.
- El sistema evalúa si la etapa fue completada.
- El sistema notifica en tiempo real a todos los clientes conectados.
- El sistema muestra mensaje de éxito.
● Flujo alterno — evidencia rechazada sin penalización:
- El operador determina que la evidencia es incorrecta.
- El operador registra el motivo de rechazo.
- El sistema marca la evidencia como Rechazada.
- El operador decide que no amerita penalización.
- El flujo termina.
● Flujo alterno — evidencia rechazada con penalización:
- El operador determina que la evidencia es incorrecta.
- El operador registra el motivo de rechazo.
- El sistema marca la evidencia como Rechazada.
- El operador decide que amerita penalización.
- El flujo extiende hacia CU-13 para aplicar la penalización.

CU-15: Supervisar ranking e historial
## ● Actores: Operador, Administrador
● Descripción: El usuario consulta el ranking actual de la sesión ordenado por puntaje
y tiempo de resolución como desempate. También puede consultar el historial de
eventos de la sesión para auditoría: evidencias enviadas, pistas liberadas,
penalizaciones aplicadas y cambios de estado.
● Flujo principal — consultar ranking:
- El usuario accede al panel de supervisión de una sesión.
- El usuario selecciona la opción de consultar el ranking.
- El sistema muestra el ranking actualizado ordenado por puntaje de mayor a
menor.
- En caso de empate el sistema usa el tiempo de resolución como criterio de
desempate.
● Flujo principal — consultar historial:
- El usuario accede al panel de supervisión de una sesión.
- El usuario selecciona la opción de consultar el historial.
- El sistema muestra el historial de eventos de la sesión: evidencias enviadas,
pistas liberadas, penalizaciones aplicadas y cambios de estado.

CU-16: Desactivar Sesión
## ● Actores: Operador
● Descripción: El operador cancela una sesión que no ha finalizado. La sesión pasa
al estado Cancelada. Esta acción es irreversible. El sistema notifica en tiempo real a
todos los equipos conectados que la sesión fue cancelada.
● Flujo principal:
- El operador accede al panel de la sesión que desea cancelar.
- El operador selecciona la opción de desactivar sesión.


- El sistema solicita confirmación indicando que la acción es irreversible.
- El operador confirma la acción.
- El sistema transiciona la sesión al estado Cancelada.
- El sistema notifica en tiempo real a todos los equipos conectados que la
sesión fue cancelada.
- El sistema muestra mensaje de éxito.
● Flujo alterno — sesión ya finalizada:
- El operador intenta cancelar una sesión en estado Finalizada.
- El sistema detecta que la transición a Cancelada no es válida desde
## Finalizada.
- El sistema no permite la acción.
- El sistema muestra mensaje indicando que la sesión ya finalizó.
● Flujo alterno — operador cancela la confirmación:
- El sistema solicita confirmación.
- El operador cancela la acción.
- El flujo termina sin cambios.

CU-17: Unirse a Sesión
## ● Actores: Participante
● Descripción: El equipo ingresa el código único de la sesión en la app móvil. El
sistema verifica que el código corresponde a una sesión activa y que el equipo está
registrado en ella. Si es válido, el equipo accede a su tablero de juego y queda
suscrito en tiempo real a los eventos de la sesión.
● Flujo principal:
- El participante abre la app móvil.
- El participante ingresa el código único de la sesión.
- El sistema verifica que el código corresponde a una sesión activa.
- El sistema verifica que el equipo está registrado en esa sesión.
- El equipo accede a su tablero de juego.
- El sistema suscribe al equipo en tiempo real a los eventos de la sesión via
SignalR.
- El tablero muestra la etapa actual del equipo, las pistas habilitadas, el
temporizador y el puntaje.
● Flujo alterno — código de sesión inválido:
- El participante ingresa un código que no corresponde a ninguna sesión
activa.
- El sistema no encuentra una sesión activa con ese código.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 2 del flujo principal.
● Flujo alterno — equipo no registrado en la sesión:
- El participante ingresa el código de una sesión activa.
- El sistema encuentra la sesión pero el equipo no está registrado en ella.
- El sistema no permite el acceso.
- El sistema muestra mensaje indicando que el equipo no pertenece a esa
sesión.



CU-18: Consultar tablero de equipo
## ● Actores: Participante
● Descripción: El equipo visualiza su tablero de juego en la app móvil. El tablero
muestra la etapa actual de la sesión, las pistas habilitadas para esa etapa, el
temporizador de la sesión y el puntaje acumulado del equipo. El tablero se actualiza
en tiempo real cada vez que hay un evento relevante en la sesión.
● Flujo principal:
- El participante accede al tablero de juego en la app móvil.
- El sistema muestra la etapa actual del flujo de la sesión.
- El sistema muestra las pistas habilitadas para esa etapa.
- El sistema muestra el temporizador de la sesión.
- El sistema muestra el puntaje acumulado del equipo.
- El tablero se actualiza en tiempo real cada vez que ocurre un evento
relevante en la sesión via SignalR.
● Flujo alterno — nuevo evento en tiempo real:
- Ocurre un evento en la sesión: nueva pista liberada, etapa avanzada, puntaje
actualizado u otro evento relevante.
- El sistema envía la notificación via SignalR al cliente móvil.
- El tablero se actualiza automáticamente sin necesidad de recargar.

CU-19: Enviar evidencia por QR
## ● Actores: Participante
● Descripción: Es una instancia concreta del flujo unificado de envío de evidencia
(RF-34) cuya estrategia de validación corresponde a este tipo de juego. Exclusivo
para sesiones de tipo BusquedaDelTesoro. El equipo abre la cámara del dispositivo
desde la app móvil para escanear el código QR físico asociado a la etapa actual. La
app envía el hash del QR al backend. El sistema compara el hash del QR
escaneado contra el hash de la etapa actual. Si coincide, la evidencia es aceptada
automáticamente, se calcula el puntaje y el equipo avanza a la siguiente etapa.
● Flujo principal:
- El participante accede al tablero en la app móvil durante una sesión de tipo
BusquedaDelTesoro.
- El participante selecciona la opción de enviar evidencia.
- El participante abre la cámara del dispositivo desde la app.
- El participante escanea el código QR físico asociado a la etapa actual.
- La app extrae el hash del QR escaneado.
- La app envía el hash al backend.
- El backend compara el hash enviado contra el qr_hash de la etapa actual del
flujo.
- Los hashes coinciden.
- El sistema acepta la evidencia automáticamente.
- El sistema calcula el puntaje del equipo usando la estrategia correspondiente
a la dificultad.
- El sistema marca la etapa como completada para este equipo.
- El sistema notifica en tiempo real a todos los clientes conectados.
- Si no hay siguiente etapa el sistema finaliza la sesión automáticamente.


● Flujo alterno — QR no coincide:
- El backend compara el hash enviado contra el qr_hash de la etapa.
- Los hashes no coinciden.
- El sistema rechaza la evidencia.
- El sistema muestra mensaje de error al participante indicando que el QR no
es válido.
● Flujo alterno — etapa ya reclamada por otro equipo:
- El participante escanea el QR correctamente.
- El sistema detecta que la etapa ya tiene un claimed_by_team_id de otro
equipo.
- El sistema no otorga puntaje al equipo actual.
- El sistema informa al participante que la etapa ya fue reclamada por otro
equipo.
● Flujo alterno — sesión no activa:
- El participante intenta enviar evidencia con la sesión en estado Pausada,
Finalizada o Cancelada.
- El sistema rechaza el envío.
- El sistema muestra mensaje indicando que la sesión no está activa.
● Flujo alterno — evidencia de etapa distinta a la actual:
- El participante intenta enviar evidencia de una etapa que no es la etapa
actual.
- El sistema rechaza el envío.
- El sistema muestra mensaje indicando que solo puede enviar evidencia de la
etapa actual.

CU-20: Enviar evidencia por texto
## ● Actores: Participante
● Descripción:Es una instancia concreta del flujo unificado de envío de evidencia
(RF-34) cuya estrategia de validación corresponde a este tipo de juego. Exclusivo
para sesiones de tipo Trivia. El equipo escribe su respuesta en un campo de texto en
la app móvil y la envía. El sistema registra la evidencia con estado Pendiente. El
operador recibe la notificación en tiempo real y procede a validarla manualmente
desde el panel web.
● Flujo principal:
- El participante accede al tablero en la app móvil durante una sesión de tipo
## Trivia.
- El participante selecciona la opción de enviar evidencia.
- El participante escribe su respuesta en el campo de texto.
- El participante envía la respuesta.
- El sistema registra la evidencia con estado Pendiente, fecha, equipo, sesión
y etapa.
- El sistema notifica en tiempo real al operador que hay una evidencia
pendiente de validación.
- El sistema muestra al participante confirmación de que la respuesta fue
enviada.
● Flujo alterno — sesión no activa:


- El participante intenta enviar la respuesta con la sesión en estado Pausada,
Finalizada o Cancelada.
- El sistema rechaza el envío.
- El sistema muestra mensaje indicando que la sesión no está activa.
● Flujo alterno — evidencia de etapa distinta a la actual:
- El participante intenta enviar una respuesta asociada a una etapa diferente a
la actual del flujo.
- El sistema rechaza el envío.
- El sistema muestra mensaje indicando que solo puede enviar evidencia de la
etapa actual.

CU-21: Visualizar progreso
## ● Actores: Participante
● Descripción: El equipo consulta su progreso en la sesión: etapas completadas,
etapa actual, puntaje acumulado y posición en el ranking. Si alguna pista de la etapa
actual tiene coordenadas geográficas, el equipo puede ver el punto marcado en un
mapa estático dentro de la app móvil.
● Flujo principal:
- El participante accede al tablero en la app móvil.
- El participante selecciona la opción de visualizar progreso.
- El sistema muestra las etapas completadas del flujo de la sesión.
- El sistema muestra la etapa actual.
- El sistema muestra el puntaje acumulado del equipo.
- El sistema muestra la posición del equipo en el ranking.
● Flujo alterno — pista con coordenadas disponible:
- El sistema detecta que alguna pista habilitada de la etapa actual tiene latitude
y longitude definidos.
- El sistema muestra el punto marcado en un mapa estático dentro de la app
móvil.

CU-22: Consultar ranking
## ● Actores: Participante
● Descripción: El equipo consulta el ranking en tiempo real de la sesión, viendo la
posición de todos los equipos ordenados por puntaje y tiempo de resolución como
desempate.
● Flujo principal:
- El participante accede al tablero en la app móvil.
- El participante selecciona la opción de consultar ranking.
- El sistema muestra el ranking en tiempo real de la sesión.
- El ranking está ordenado de mayor a menor puntaje.
- En caso de empate el sistema usa el tiempo de resolución como criterio de
desempate.
- El ranking se actualiza automáticamente en tiempo real via SignalR cuando
hay cambios.



CU-23: Ver pistas y soluciones reveladas
## ● Actores: Participante
● Descripción: Al finalizar la sesión, el sistema revela automáticamente todas las
pistas y las soluciones de todas las etapas. El equipo puede consultar desde la app
móvil las resoluciones completas de la misión, incluyendo las pistas marcadas como
solución que permanecían ocultas durante el juego. Si alguna solución tiene
coordenadas, se muestra el punto en el mapa estático.
● Flujo principal:
- La sesión pasa al estado Finalizada.
- El sistema revela automáticamente todas las pistas y soluciones de todas las
etapas del flujo.
- El participante accede desde la app móvil a la sección de resoluciones.
- El sistema muestra todas las pistas incluyendo las marcadas como
is_solution = TRUE que estaban ocultas durante el juego.
● Flujo alterno — solución con coordenadas:
- El sistema detecta que una pista revelada tiene latitude y longitude definidos.
- El sistema muestra el punto marcado en un mapa estático junto con el
contenido de la pista.
● Flujo alterno — intento de ver soluciones antes de finalizar:
- El participante intenta acceder a las soluciones con la sesión aún activa.
- El sistema detecta que la sesión no está en estado Finalizada.
- El sistema no muestra las pistas marcadas como is_solution = TRUE.
- El sistema solo muestra las pistas que han sido habilitadas durante el juego.

CU-24: Crear Operadores
## ● Actores: Administrador
● Descripción: El administrador va a poder crear nuevos operadores para el sistema.
● Flujo principal:
- El administrador accede al módulo de gestión de usuarios.
- El administrador selecciona la opción de crear nuevo operador.
- El administrador ingresa los datos del nuevo operador.
- El sistema valida los datos.
- El sistema crea el usuario con rol Operador.
- El sistema muestra mensaje de éxito.
● Flujo alterno — datos inválidos o duplicados:
- El administrador ingresa datos incorrectos o un email ya registrado.
- El sistema muestra mensaje de error.
- El flujo regresa al paso 3 del flujo principal.
CU-25: Desactivar etapa en sesión

## ● Actores: Operador
● Descripción: El operador desactiva una etapa o subetapa del flujo de una sesión
cuando considere que su dificultad es excesiva o cuando necesite acortar la


duración del juego. La desactivación aplica únicamente a la sesión seleccionada y
no modifica la estructura original de la misión.
## ● Flujo Principal
- El operador accede al panel de una sesión en estado Programada o Activa.
- El operador selecciona la opción de gestionar etapas de la sesión.
- El operador selecciona una etapa o subetapa pendiente y elige la opción de
desactivar.
- El sistema solicita confirmación mostrando el impacto sobre el flujo restante.
- El operador confirma la acción.
- El sistema marca la etapa o subetapa como inactiva dentro de
session\_stage\_flow para esa sesión.
- El sistema recalcula el orden efectivo del flujo de etapas de la sesión.
- El sistema muestra mensaje de éxito.
● Flujo alterno — etapa o subetapa ya completada:
- El operador intenta desactivar una etapa o subetapa que ya fue completada
en la sesión.
- El sistema no permite la acción.
- El sistema muestra mensaje indicando que solo pueden desactivarse etapas
pendientes.
● Flujo alterno — intento de dejar la sesión sin etapas activas:
- El operador intenta desactivar la última etapa activa pendiente del flujo.
- El sistema detecta que la sesión quedaría sin etapas activas.
- El sistema no permite la acción.
- El sistema muestra un mensaje indicando que debe mantenerse al menos
una etapa activa o cancelar/finalizar la sesión.
CU-26: Monitorear estado general de la sesión (Dashboard)
## ● Actores: Operador, Administrador
● Descripción: El usuario visualiza un panel central con métricas agregadas de la
sesión activa. Este tablero permite observar de un vistazo el tiempo transcurrido, la
etapa actual de cada equipo, el número de equipos conectados y el estado de la
comunicación en tiempo real. La información se actualiza automáticamente mediante
SignalR.
● Flujo principal:
- El usuario accede al módulo de sesiones activas desde el panel web.
- El sistema muestra una lista de sesiones en curso gestionadas por el
usuario.
- El usuario selecciona la sesión que desea supervisar.
- El sistema carga el tablero operacional mostrando: temporizador de
ejecución, etapa actual de cada equipo, contador de equipos activos y un
gráfico comparativo de puntajes.
- El sistema mantiene la información sincronizada en tiempo real ante
cualquier evento de la sesión.
● Flujo alterno — fallo de conexión en tiempo real:
- El sistema detecta la pérdida de conexión SignalR.
- El sistema muestra un indicador visual de "Sin conexión / Datos
desactualizados".


- El usuario utiliza la función de refresco manual integrada en el tablero.
- El sistema realiza una petición Query (CQRS) al backend para obtener la
última foto del estado de la sesión.
- El flujo regresa al paso 4 del flujo principal.
CU-27: Supervisar progreso de equipos (Monitoreo)
## ● Actores: Operador
● Descripción: El operador selecciona un equipo específico desde la lista del tablero
operacional para revisar su actividad detallada. Esto permite verificar qué pistas ha
recibido el equipo, qué evidencias ha enviado y detectar posibles estancamientos en
el juego.
● Flujo principal:
- El operador se encuentra en la vista del tablero operacional de una sesión
activa (CU-26).
- El operador selecciona un equipo de la lista de participantes.
- El sistema despliega un panel lateral con el detalle del equipo: etapa actual
en la que se encuentran, tiempo que llevan en dicha etapa y lista cronológica
de pistas liberadas.
- El operador visualiza la galería de evidencias enviadas por el equipo
(pendientes y aceptadas).
- El operador puede filtrar las evidencias para enfocarse en aquellas que
requieren validación manual (en caso de Trivia).
● Flujo alterno — equipo sin actividad detectada:
- El operador selecciona un equipo que no ha realizado envíos en su etapa
actual.
- El sistema muestra un mensaje de advertencia indicando el tiempo de
inactividad del equipo.
- El operador decide si interviene liberando una pista adicional (CU-11) o
enviando una notificación.
- El flujo termina.
CU-28: Ejecutar acciones de control central
## ● Actores: Operador
● Descripción: Proporciona un centro de mando para que el operador realice ajustes
rápidos sobre la sesión de forma global. Permite controlar el flujo del juego (pausas,
saltos de etapa o avisos) sin necesidad de navegar por diferentes menús.
● Flujo principal:
- El operador se encuentra en el tablero operacional de la sesión.
- El operador selecciona el menú de "Acciones de Control".
- El operador elige una acción global.
- El sistema solicita confirmación antes de ejecutar cambios que afecten a
todos los participantes.
- El operador confirma la acción.
- El sistema procesa el comando a través del backend.
- El sistema propaga el cambio de estado instantáneamente a todos los
clientes conectados vía SignalR.


- El tablero operacional actualiza todos los indicadores para reflejar el nuevo
estado.
● Flujo alterno — acción bloqueada por regla de negocio:
- El operador intenta "Avanzar Etapa" cuando la sesión ya se encuentra en la
última etapa activa disponible.
- El sistema deshabilita el botón o muestra una notificación indicando que no
hay más etapas en el flujo.
- El flujo termina sin realizar cambios en la sesión.



## Diagramas
Diagrama de Caso de Uso

## Diagrama 1: Login




## Diagrama 2: Gestionar Misiones


## Diagrama 3: Gestionar Sesiones




Diagrama 4: Experiencia de Juego
Modelo de Dominio




- Modelo conceptual sugerido del dominio
Concepto Descripción académica esperada
Mission Agregado principal para definir una misión, sus etapas, pistas y reglas base.
MissionNode Entidad o componente jerárquico que representa una etapa, subetapa o pista.
LiveSession Agregado principal para la ejecución en vivo de una misión.
Team Entidad asociada a una sesión, con progreso, estado y puntaje.
EvidenceSubmission Registro de respuesta o evidencia enviada por un equipo.
ScoreEntry Trazabilidad de puntos otorgados o penalizados.
SessionEvent Evento significativo del dominio utilizado para historial y auditoría.
## Value Objects
Objetos de valor para conceptos como TeamCode, ScoreValue, Difficulty o
PenaltyReason.
