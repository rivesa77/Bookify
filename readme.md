# Bookify

## Estado de salud

`GET /health` devuelve JSON con el estado de PostgreSQL y de la URL base de Keycloak. Se publica en todos los entornos, sin exigir autorizacion; no es una interfaz grafica ni una prueba completa de login o del esquema. Consultar [funcionamiento y diagnostico](Bookify.Api/readme.md#estado-de-salud-health) y [registro de los checks](Bookify.Infrastructure/readme.md#health-checks).

La nueva clave `Keycloak:BaseUrl` es obligatoria al registrar Infrastructure: Development usa `http://localhost:18080`. Dentro de Compose debe proporcionarse `Keycloak__BaseUrl=http://bookify-idp:8080`. **El Compose actual todavia no incluye esa variable**; sus otras URLs de Keycloak no sustituyen BaseUrl. Sin ella, el valor local heredado apunta al propio contenedor API y el check puede fallar.

## Registro de usuarios: cambios actuales

Existe POST /api/users/register, anonimo mediante [AllowAnonymous]. Recibe nombre, apellido, email y password, registra la identidad en Keycloak, guarda User.IdentityId y persiste el usuario local con el rol Registered. Responde 200 con el GUID User.Id de Bookify, no con el objeto Result ni con un JWT.

Application incorpora CreateUser e IAuthenticationService. Infrastructure implementa el cliente administrativo HTTP, obtiene un token por client_credentials y agrega la migracion Add_User_IdentityId. No hay transaccion comun ni compensacion entre Keycloak y PostgreSQL.

Consultar [API](Bookify.Api/readme.md#registro-de-usuarios), [Application](Bookify.Application/readme.md#userscreateuser), [Infrastructure](Bookify.Infrastructure/readme.md#registro-administrativo-en-keycloak) y [Domain](Bookify.Domain/readme.md#useridentityid-y-registro-externo). Los clientes y sus permisos deben estar configurados en el realm real. Los README no reproducen valores secretos del JSON.


## JWT y Keycloak

La API incorpora JWT Bearer: Infrastructure registra el esquema y API ejecuta UseAuthentication antes de UseAuthorization. GET y POST /api/apartments requieren un access token por [Authorize]. POST /api/users/login obtiene un token de Keycloak; GET /api/users/LogInUser exige autenticacion, rol Registered y permiso users:read. Los roles y permisos proceden de PostgreSQL, no de los roles del realm.

CustomClaimsTransformation resuelve la identidad externa hacia el usuario local. GetBooking comprueba que la reserva pertenezca a IUserContext.UserId. Reservas y reviews siguen sin [Authorize] ni politica global: el control de propiedad de la lectura no protege por si solo todos esos endpoints. Veanse [roles](Bookify.Infrastructure/Roles.md), [permisos](Bookify.Infrastructure/Permisos.md) y [recursos](Bookify.Infrastructure/Recursos.md).

La seccion Authentication utiliza Audience, Issuer, MetadataUrl y RequireHttpsMetadata. Development apunta a localhost:18080; Compose sobrescribe metadatos y URLs administrativas con bookify-idp:8080. El emisor publico se mantiene en http://localhost:18080/realms/bookify en ambos modos. Audience=account debe coincidir con aud del token real.

Para depurar la API local con dependencias en Docker, arrancar solo bookify-db y bookify-idp, y ejecutar el perfil http o https de Bookify.Api. PostgreSQL usa localhost:5432 y Keycloak localhost:18080. No hace falta editar hosts. Si Docker se creo desde Visual Studio, reutilizar su proyecto Compose para no duplicar contenedores. No arrancar otra instancia PostgreSQL local en el mismo puerto.

Las variables de entorno y secretos de usuario prevalecen sobre appsettings.Development.json: revisar posibles URLs antiguas configuradas alli. Reiniciar la API tras cambiar las opciones. La API aplica migraciones y sembrado en Development. Al cambiar el hostname de Keycloak, obtener tokens nuevos. Estas URLs HTTP son solo para desarrollo.

La [guia JWT de API](Bookify.Api/readme.md#autenticacion-jwt) explica configuracion, uso manual y diagnostico. [Infrastructure](Bookify.Infrastructure/readme.md#authentication-jwt-bearer) detalla las clases. Los tests que invocan metodos de controladores no verifican [Authorize]; no se ha validado aqui un flujo JWT de extremo a extremo.



Bookify es una solucion .NET 10 para reservar apartamentos, organizada en Domain, Application, Infrastructure y Api. El host actual es `Bookify.Api`: registra las capas, expone controladores HTTP y, en Development, publica OpenAPI y Swagger UI y aplica las migraciones de PostgreSQL al arrancar.

## Guias por capa

| Guia | Contenido |
| --- | --- |
| [Domain](Bookify.Domain/readme.md) | Entidades, objetos de valor, reglas, eventos, resultados y constructores de materializacion. |
| [Application](Bookify.Application/readme.md) | Comandos, queries, MediatR, validacion, DTO, contratos y limitaciones actuales del SQL. |
| [Infrastructure](Bookify.Infrastructure/readme.md) | EF Core, repositorios, mapeos, migraciones, Outbox/Quartz, concurrencia y servicios externos. |
| [Api](Bookify.Api/readme.md) | Cada archivo del host, endpoints, respuestas HTTP, configuracion, arranque y Dockerfile. |

Las guias describen el codigo y las migraciones versionadas. La existencia de estos archivos no demuestra que una base de datos tenga el esquema aplicado ni que todos los endpoints hayan superado pruebas de integracion.

## Arquitectura y dependencias

```text
Bookify.Api -> Bookify.Application -> Bookify.Domain
Bookify.Api -> Bookify.Infrastructure -> Bookify.Application
```

Domain define reglas e interfaces de repositorios y no referencia EF Core. Application orquesta los casos de uso y define contratos externos. Infrastructure implementa persistencia, reloj y correo. Api compone las dependencias y adapta HTTP a mensajes de Application.

Domain utiliza `MediatR.Contracts` para los eventos. Application usa MediatR, FluentValidation y Dapper; sus queries contienen SQL de PostgreSQL, por lo que un cambio de esquema o proveedor exige revisarlas. Infrastructure accede a Domain mediante la referencia transitiva de Application.

Esta separacion permite que las reglas de una reserva no dependan de un controlador ni de un proveedor de correo. Los constructores privados de las entidades permiten reconstruir datos con EF sin exponer una forma publica de crear objetos incompletos.

## Archivos de la raiz

| Archivo | Funcion actual |
| --- | --- |
| [Bookify.slnx](Bookify.slnx) | Agrupa Api, Application, Domain e Infrastructure bajo `/Scr/`, Docker Compose y los proyectos de arquitectura y pruebas unitarias bajo `/Tests/`. |
| [Bookify.csproj](Bookify.csproj) | Proyecto de consola residual, fuera de la solucion y sin referencias a las capas. No es el host actual; ya no existe `Program.cs` en la raiz. |
| [docker-compose.yml](docker-compose.yml) | Define API, PostgreSQL 17 y Keycloak, sus redes/puertos, credenciales locales y persistencia. |
| [Postman/Bookify_AddHealthCheck.postman_collection.json](Postman/Bookify_AddHealthCheck.postman_collection.json) | Coleccion con la nueva solicitud de salud y ejemplos de operaciones. Revisar variables y credenciales antes de compartirla o ejecutarla. |
| [docker-compose.override.yml](docker-compose.override.yml) | Configuracion local: Development, puertos HTTP/HTTPS y montajes de secretos y certificados de Windows. |
| [docker-compose.dcproj](docker-compose.dcproj) | Integra Compose con las herramientas de contenedores de Visual Studio para Linux. |
| [launchSettings.json](launchSettings.json) | Perfil Docker Compose: inicia `bookify.api` con `StartDebugging` desde Visual Studio. |
| [.dockerignore](.dockerignore) | Excluye del contexto archivos como `bin`, `obj`, `.vs`, `.env` y configuraciones del IDE. |

Las carpetas `bin/` y `obj/` contienen salidas generadas. `obj/Docker` puede contener archivos Compose de depuracion generados por Visual Studio; no son la configuracion fuente que debe editarse.

## Flujo de una solicitud

```text
POST /api/bookings
  -> BookingsController convierte ReserveBookingRequest en ReserveBookingCommand
  -> ISender.Send
  -> LoggingBehavior -> ValidationBehavior
  -> ReserveBookingCommandHandler consulta usuario, apartamento y disponibilidad
  -> Booking.Reserve calcula precios, cambia el apartamento y acumula un evento
  -> IUnitOfWork / ApplicationDbContext guarda los cambios y el mensaje Outbox
  -> Respuesta HTTP 201 o 400 para fallos de negocio

En segundo plano, dentro del host API:
  -> Quartz ejecuta ProcessOutboxMessagesJob
  -> IPublisher publica BookingReservedDomainEvent
  -> El job actualiza processed_on_utc y error
```

Las lecturas pasan del controlador a la query de MediatR y despues a Dapper mediante `ISqlConnectionFactory`. Devuelven DTO y no materializan entidades EF. Los behaviors actuales solo se aplican a comandos; no envuelven queries ni notificaciones.

Los eventos se serializan y persisten en `outbox_messages` junto con los cambios del negocio. La respuesta no espera a sus consumidores: Quartz publica los mensajes pendientes en segundo plano. Development configura un intervalo de 10 segundos y lotes de 10. Actualmente un fallo de publicacion se guarda en `error` y tambien marca el mensaje como procesado: no hay reintento automatico de esos mensajes. Tampoco hay garantia de entrega exactamente una vez. `EmailService` sigue siendo provisional y no envia correo real. Consultar [Outbox y Quartz](Bookify.Infrastructure/readme.md#outbox-y-quartz).

## Capacidades actuales

El alta de apartamentos esta disponible en `POST /api/apartments`: contrato HTTP, comando, FluentValidation, handler y guardado EF mediante repositorio/unidad de trabajo. Devuelve `201` con el id. No necesita una migracion adicional. Consultar [contrato y ejemplo de alta](Bookify.Api/readme.md#alta-de-apartamentos), incluida la regla actual de nombre de exactamente 200 caracteres.

Las pruebas se organizan en `Tests/Bookify.Application.Tests`, `Bookify.Domain.Tests`, `Bookify.Infrastructure.Tests`, `Bookify.Api.Tests` y `Bookify.Architecture.Tests`. `Bookify.TestUtilities` comparte contextos y datos. Las pruebas HTTP usan WebApplicationFactory; no deben confundirse con llamadas directas a controladores.

| Operacion | Estado |
| --- | --- |
| Buscar apartamentos | `GET /api/apartments?startDate=2026-10-01&endDate=2026-10-05`, con JWT. |
| Consultar reserva | `GET /api/bookings/{id}`; devuelve detalle solo al propietario identificado por IUserContext, o 404. |
| Reservar | `POST /api/bookings`; comando, validacion, dominio y persistencia conectados. Requiere usuario y apartamento existentes. |
| Confirmar, rechazar, completar y cancelar | Metodos y eventos en Domain; todavia sin comandos ni endpoints. |
| Crear resena | `POST /api/reviews`, comando, validador y repositorio EF; exige una reserva completada. Ver [contrato y ejemplo](Bookify.Api/readme.md#alta-de-resenas). |
| Crear usuario | `POST /api/users/register`; alta en Keycloak y PostgreSQL, 200 con GUID local. |
| Login y perfil | `POST /api/users/login` y `GET /api/users/LogInUser`. El JSON del token usa actualmente `accessToke`. |
| Salud | `GET /health`; conectividad PostgreSQL y GET a Keycloak:BaseUrl. |
| Documentacion interactiva | `/swagger/index.html` consume `/openapi/v1.json`, solo en Development. |
| Migraciones | Seis migraciones hasta `20260923111807_Add_OutBoxMessages`; aplicacion automatica en Development. |

`Result` representa fallos de negocio. Los controladores de alta los convierten en `400`; la consulta de reserva devuelve `404` ante un resultado fallido. El middleware propio convierte `ValidationException` de Application en 400 y otros errores tecnicos en 500.

## Ejecucion local

Compilar desde la raiz con el SDK de .NET 10:

```powershell
dotnet build Bookify.Api/Bookify.Api.csproj
```

Este proyecto construye tambien las tres capas referenciadas. La solucion completa incluye las herramientas Docker de Visual Studio.

Para ejecutar la API sin Docker, instala y arranca PostgreSQL en la maquina local. Los perfiles `http` y `https` usan Development y cargan `appsettings.Development.json`, con `Host=localhost`, puerto 5432, base `bookify` y usuario/clave de ejemplo `postgres`/`postgres`.

```powershell
dotnet run --project Bookify.Api/Bookify.Api.csproj --launch-profile http
```

Para otro servidor, puerto o credenciales, configura secretos de desarrollo (sustituye los valores por los reales):

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=bookify;Username=mi_usuario;Password=mi_clave" --project Bookify.Api/Bookify.Api.csproj
```

Estos secretos no se guardan en el repositorio y prevalecen sobre el JSON en Development. Para un servidor remoto, cambia `Host` por su nombre o IP y configura PostgreSQL y el firewall para aceptar la conexion. No guardes credenciales reales en los archivos versionados.

Para ejecutar directamente una API publicada, desde la carpeta de publicacion, sin depender de los perfiles de Visual Studio:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=bookify;Username=mi_usuario;Password=mi_clave"
dotnet Bookify.Api.dll
```

La variable de entorno prevalece sobre el JSON y los secretos. Si no se especifica un entorno, el arranque directo usa Production: no ejecuta las migraciones ni el sembrado automatico; prepara la base previamente. Ademas de la conexion, hay que proporcionar Authentication y Keycloak, incluida `Keycloak__BaseUrl`: el archivo Development no se carga en Production. `launchSettings.json` solo interviene al usar un perfil de lanzamiento, no al ejecutar la DLL.

Production tambien necesita `Outbox__IntervalInSeconds` y `Outbox__BatchSize` con valores positivos: el job se registra en todos los entornos, pero sus valores actuales solo estan en Development. Consultar [Outbox en el host](Bookify.Api/readme.md#outbox-en-el-host).

Alternativamente, para ejecutar la API en Windows con PostgreSQL publicado por Docker:

```powershell
docker compose up -d bookify-db bookify-idp
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=bookify;Username=postgres;Password=postgres"
dotnet run --project Bookify.Api/Bookify.Api.csproj --launch-profile http
```

Con el perfil `http`, Swagger se abre en [http://localhost:5285/swagger/index.html](http://localhost:5285/swagger/index.html). La variable afecta a los procesos de esa sesion PowerShell. En Windows se usa `localhost`; `bookify-db` es el nombre DNS dentro de la red Compose. `docker-compose.yml` establece explicitamente la cadena del contenedor API mediante `ConnectionStrings__Database`. No arranques el contenedor PostgreSQL con el puerto 5432 publicado si tu instalacion local ya ocupa ese puerto.

El arranque ejecuta `Database.Migrate()` en Development antes de atender solicitudes. PostgreSQL debe estar disponible; un error de migracion impide terminar el arranque. La guia de Infrastructure explica como generar y aplicar migraciones manualmente.

## Docker Compose

La API se construye con [Bookify.Api/Dockerfile](Bookify.Api/Dockerfile). PostgreSQL usa `postgres:17`, base `bookify` y usuario y clave `postgres`, configurados para desarrollo local. Guarda sus datos en el volumen administrado `bookify-db-data`, montado en `/var/lib/postgresql/data`. Docker antepone el nombre del proyecto Compose al nombre del volumen. La antigua carpeta `./containers/database` ya no se monta y se conserva sin modificar.

| Servicio | Puerto del equipo | Puerto del contenedor | Uso |
| --- | --- | --- | --- |
| `bookify.api` | 5000 | 8080 | HTTP. |
| `bookify.api` | 5001 | 8081 | HTTPS, requiere certificado accesible y configurado. |
| `bookify-db` | 5432 | 5432 | PostgreSQL desde Windows o pgAdmin. |
| `bookify-idp` | 18080 (127.0.0.1) | 8080 | Keycloak de desarrollo. |

Con Docker Desktop activo y los secretos/certificados de desarrollo preparados:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f bookify.api
```

Swagger: [http://localhost:5000/swagger/index.html](http://localhost:5000/swagger/index.html). En Development ya no se aplica `UseHttpsRedirection`, por lo que OpenAPI se puede solicitar por ese mismo puerto HTTP. El override sigue habilitando el listener HTTPS: abrir HTTP no evita un fallo de arranque si Kestrel no puede configurar su certificado.

`depends_on` usa `condition: service_healthy`: Compose espera a que PostgreSQL acepte conexiones TCP antes de iniciar la API. El healthcheck ejecuta `pg_isready -h 127.0.0.1 -U postgres -d bookify`, cada 5 segundos, con timeout de 5 segundos, 12 intentos y un periodo inicial de 10 segundos. Esto ordena el arranque; no reinicia ni recupera automaticamente la API si la base cae mas tarde. Los montajes de certificados son de solo lectura y no generan ni dan confianza automaticamente a un certificado.

Para pgAdmin ejecutado en Windows: host `localhost`, puerto `5432`, base de mantenimiento `bookify`, usuario `postgres` y clave local `postgres`. Si pgAdmin se ejecuta en otro contenedor, la direccion depende de su red; `localhost` designaria ese otro contenedor.

### Visual Studio y ejecucion independiente

En modo rapido, Visual Studio construye la etapa `base`, monta los binarios y puede mantener `DistrolessHelper.dll --wait`. Docker puede mostrar el contenedor activo mientras la API no esta ejecutandose. Las etiquetas de Visual Studio, el entrypoint y los montajes de depuracion ayudan a identificarlo en Inspect.

Compose desde terminal construye la etapa final y su entrada es `dotnet Bookify.Api.dll`. Usa los nombres fijos `Bookify.Api` y `Bookify.db`; no pueden coexistir otros contenedores con esos nombres creados por un proyecto de Visual Studio diferente. Para conservar la misma base al cambiar de modo, mantener el mismo nombre de proyecto Compose (opcion `-p`) y su volumen. Otro proyecto obtiene un volumen distinto. `docker compose down` conserva los volumenes nombrados; agregar `-v` los elimina junto con sus datos.

Si PostgreSQL termina con `directory ... exists but is not empty`, comprobar su almacenamiento antes de modificarlo: una carpeta no vacia sin `PG_VERSION` no se reconoce como un cluster inicializado. En el incidente corregido solo quedaron directorios vacios en la carpeta antigua. La API mostraba `Name or service not known` porque el contenedor de base de datos estaba detenido; el nombre `bookify-db` era correcto. Revisar primero los logs de `bookify-db` antes de cambiar ese host o los puertos.

Una imagen final compilada en Release no cambia `ASPNETCORE_ENVIRONMENT`: con el override actual sigue siendo Development. Usar Compose sin Visual Studio no convierte esta configuracion local en un despliegue de produccion.

## Cambios recientes y limites

- `Entity` tiene constructor protegido vacio; `Apartment`, `Booking`, `Review` y `User` tienen constructores privados vacios. EF puede construir el modelo sin enlazar las navegaciones owned al constructor de negocio.
- Existen seis migraciones con sus Designer y el Snapshot actual. Outbox agrega `outbox_messages`, con `processed_on_utc` y `error` anulables. Crear una migracion genera codigo; aplicarla modifica la base de datos.
- `BookingResponse` ya expone `PriceAmount`, codigos de moneda `string` y `DurationStart`/`DurationEnd` alineados con los alias de la query.
- `Change_TableName_And_Field` renombra las tablas a minusculas y crea el indice unico de `identity_id`. GetBooking ya envia `BookingId` y consulta `amenities_up_change_*` con alias hacia el DTO. No basta aplicar solo Initial_Database.
- `Address` sigue siendo opcional para EF; sus columnas admiten `NULL`. El constructor vacio no establece obligatoriedad de campos ni navegaciones.
- El conflicto de EF se traduce a `ConcurrencyException` y despues a `BookingErrors.Overlap`. El token sombra del apartamento usa `xmin` y requiere una actualizacion efectiva del apartamento; no protege todas las entidades ni cualquier insercion por otros medios.
- Hay pruebas HTTP con autenticacion simulada y pruebas opcionales de integracion PostgreSQL. No verifican un flujo JWT real contra Keycloak; los resultados actuales se detallan abajo.

## Pruebas y verificacion

Ultimos resultados comprobados con `dotnet test` por proyecto, sin servicios externos. Infrastructure se verifico el 23/09/2026 tras agregar los tests Outbox; los demas resultados corresponden a las ejecuciones anteriores documentadas:

| Proyecto y guia | Resultado |
| --- | --- |
| [Domain.Tests](Tests/Bookify.Domain.Tests/readme.md) | 137 correctos. |
| [Application.Tests](Tests/Bookify.Application.Tests/readme.md) | 138 correctos. |
| [Infrastructure.Tests](Tests/Bookify.Infrastructure.Tests/readme.md), filtro Infrastructure | 108 correctos, incluidos 20 unitarios de Outbox. |
| [Api.Tests](Tests/Bookify.Api.Tests/readme.md) | 57 correctos. |
| Bookify.Architecture.Tests | 8 correctos. |

Los hosts de tests aportan Keycloak:BaseUrl ficticia y las opciones Outbox. ApiFactory retira el servicio alojado de Quartz para no iniciar trabajos SQL durante las pruebas HTTP. Queda pendiente probar /health con checks simulados. Los 26 casos PostgreSQL requieren BOOKIFY_TEST_POSTGRES y quedaron omitidos, incluidos cuatro de Outbox; no se cuentan como correctos. Los informes de cobertura anteriores a health checks son historicos, no una medida del codigo actual.

Para profundizar, seguir las guias en el orden Domain, Application, Infrastructure y Api. Cada una incluye el inventario de archivos y explica las decisiones y el comportamiento actual de su capa.
