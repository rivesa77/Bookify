# Bookify.Api

Esta es la capa de entrada HTTP y el host ejecutable de Bookify. Recibe solicitudes, convierte sus datos en comandos o queries de Application y adapta los resultados a respuestas HTTP. Tambien compone las dependencias, configura autenticacion/autorizacion, publica `/health` y, durante desarrollo, OpenAPI, Swagger UI, migraciones y datos de ejemplo. Revision: 22 de septiembre de 2026.

[Guia de la solucion](../readme.md) | [Domain](../Bookify.Domain/readme.md) | [Application](../Bookify.Application/readme.md) | [Infrastructure](../Bookify.Infrastructure/readme.md)

## Registro de usuarios

POST /api/users/register recibe CreateUserRequest (FirstName, LastName, Email, Password). UsersController tiene [Authorize], pero Register declara [AllowAnonymous], por lo que el alta no requiere JWT. El controlador convierte la entrada a CreateUserCommand y llama a ISender.

```http
POST http://localhost:5285/api/users/register
Content-Type: application/json

{
  "firstName": "Ana",
  "lastName": "Garcia",
  "email": "ana@example.com",
  "password": "<clave>"
}
```

Sustituir la clave de ejemplo por una valida antes de enviar. No guardar credenciales reales en peticiones versionadas. El validador actual exige nombre y apellido no vacios de hasta 100 caracteres, email valido y password entre 5 y 10 caracteres. La politica de Keycloak puede ser mas restrictiva; este limite de longitud describe el codigo, no una recomendacion de seguridad.

La respuesta exitosa es **200 OK con el GUID local como valor JSON**, porque el controlador devuelve `Ok(result.Value)`, no el objeto Result completo. El identificador externo de Keycloak se guarda en User.IdentityId; el comando devuelve User.Id. Si se devuelve un Result fallido, el controlador responde 400; el handler actual no transforma errores de Keycloak en Result. Las excepciones HTTP o de persistencia llegan al middleware como errores tecnicos, normalmente 500.

Archivos: [UsersController.cs](Controllers/Users/UsersController.cs) implementa el endpoint y [CreateUserRequest.cs](Controllers/Users/CreateUserRequest.cs) define el contrato HTTP. El registro no devuelve tokens; el login es una operacion independiente.

## Login y perfil

`POST /api/users/login` es anonimo y recibe [LoginUserRequest](Controllers/Users/LoginUserRequest.cs), con Email y Password. Envia LogInUserCommand; Infrastructure solicita el token a Keycloak con el cliente AuthClientId/AuthClientSecret y `grant_type=password`. El realm debe permitir ese flujo para dicho cliente. Esto describe el flujo implementado, no Authorization Code/PKCE.

El exito devuelve 200 con `{"accessToke":"<access_token>"}`. La propiedad se llama literalmente AccessToke en el DTO actual. Un Result fallido devuelve 401 con UserErrors.InvalidCredentials; los errores de validacion del comando son 400. No hay endpoint de refresh ni de logout.

`GET /api/users/LogInUser` devuelve el perfil, no un token. Exige JWT, rol local Registered y permiso users:read, y envia GetLoggedInUserQuery. El DTO contiene Id, FirstName, LastName y Email. La lectura usa IdentityId del contexto actual; si no encuentra una unica fila, QuerySingleAsync lanza y el middleware responde 500. La accion accede a result.Value sin tratar un Result fallido.

### Configuracion administrativa de Keycloak

La seccion Authentication configura validacion de tokens entrantes; Keycloak configura llamadas salientes. AdminUrl, TokenUrl, AdminClientId y AdminClientSecret sirven al registro; AuthClientId y AuthClientSecret al login. BaseUrl sirve exclusivamente al nuevo health check y se lee directamente de IConfiguration, no de KeycloakOptions.

El JSON Development utiliza localhost:18080 y rutas sin /auth, de acuerdo con el Keycloak actual. Compose sobrescribe AdminUrl y TokenUrl con bookify-idp:8080. Configuracion para API ejecutada en Windows:

```json
{
  "Keycloak": {
    "BaseUrl": "http://localhost:18080",
    "AdminUrl": "http://localhost:18080/admin/realms/bookify/",
    "TokenUrl": "http://localhost:18080/realms/bookify/protocol/openid-connect/token",
    "AdminClientId": "bookify-admin-client"
  }
}
```

AdminUrl debe terminar en / porque el cliente anade la ruta relativa users. Desde Compose utilizar el nombre bookify-idp y puerto 8080 en lugar de localhost:18080. Proporcionar AdminClientSecret desde secretos de usuario o Keycloak__AdminClientSecret; no copiar secretos reales a la documentacion. El JSON versionado contiene valores de secretos: si se han compartido, deben rotarse y externalizarse.

Preparar un cliente confidencial con cuenta de servicio y permisos administrativos para crear usuarios en el realm (por ejemplo, el rol realm-management/manage-users cuando corresponda a la configuracion usada). El alta solicita client_credentials; no usa la cuenta admin del bootstrap ni el token del solicitante. Usar HTTPS en produccion para proteger contrasenas y secretos.

Aplicar la migracion Add_User_IdentityId antes de guardar nuevos usuarios. El flujo no es atomico entre Keycloak y PostgreSQL: un fallo local despues del alta externa puede dejar un usuario solo en Keycloak. No hay compensacion ni reintento idempotente implementados. No se ha verificado aqui el registro real contra ambos sistemas.


## Autenticacion JWT

### Flujo y endpoints

Keycloak emite access tokens; Bookify expone login delegando su obtencion en Keycloak y valida los tokens entrantes mediante JWT Bearer. En Program.cs se ejecutan, por este orden, UseCustomExceptionHandler, UseAuthentication, UseAuthorization, MapControllers y MapHealthChecks. La autenticacion construye HttpContext.User; la autorizacion decide si puede acceder al endpoint.

| Endpoint | Proteccion actual |
| --- | --- |
| GET /api/apartments | [Authorize] en ApartmentsController. |
| POST /api/apartments | [Authorize] en ApartmentsController. |
| GET /api/bookings/{id} | Sin [Authorize] ni politica global; Application compara el propietario con IUserContext.UserId. |
| POST /api/bookings | Sin [Authorize] ni politica global. |
| POST /api/reviews | Sin [Authorize] ni politica global. |
| POST /api/users/register | Anonimo por [AllowAnonymous], aunque UsersController tiene [Authorize]. |
| POST /api/users/login | Anonimo por [AllowAnonymous]. |
| GET /api/users/LogInUser | [Authorize], rol Registered y permiso users:read. |
| GET /health | Sin RequireAuthorization ni politica global. |

En endpoints protegidos, un token ausente o invalido produce normalmente 401 y una identidad sin el rol o permiso requerido produce 403. CustomClaimsTransformation consulta el usuario local por IdentityId y agrega su Guid y roles al principal. Si ese usuario no existe, GetRolesForUserAsync usa FirstAsync y lanza: el middleware devuelve 500, no un 403 controlado. Los permisos se consultan en PostgreSQL y el handler exige el nombre exacto. Consultar [Roles](../Bookify.Infrastructure/Roles.md), [Permisos](../Bookify.Infrastructure/Permisos.md) y [Recursos](../Bookify.Infrastructure/Recursos.md).

### Opciones y configuracion local

Infrastructure lee Authentication mediante AuthenticationOptions y JwtBearerOptionsSetup:

| Clave | Uso |
| --- | --- |
| Audience | Audiencia que debe incluir el token en aud; actualmente account. |
| Issuer | Emisor esperado, asignado a TokenValidationParameters.ValidIssuer. |
| MetadataUrl | Documento OpenID Connect; anuncia tambien las claves en jwks_uri. |
| RequireHttpsMetadata | Exige HTTPS al obtener metadatos; false solo para desarrollo HTTP. |

appsettings.Development.json utiliza ahora Issuer, coincidiendo con AuthenticationOptions, y MetadataUrl con localhost:18080 para el proceso local. Compose sobrescribe MetadataUrl con bookify-idp:8080, manteniendo el emisor publico http://localhost:18080/realms/bookify. Keycloak fija KC_HOSTNAME=http://localhost:18080 y KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true para anunciar claves y endpoints de backend alcanzables por la red desde la que se consulta. No se desactiva la validacion de firma, audiencia ni emisor.

Configuracion Development para API local y Keycloak publicado en localhost:18080:

```json
{
  "Authentication": {
    "Audience": "account",
    "Issuer": "http://localhost:18080/realms/bookify",
    "MetadataUrl": "http://localhost:18080/realms/bookify/.well-known/openid-configuration",
    "RequireHttpsMetadata": false
  }
}
```

Las variables equivalentes son Authentication__Audience, Authentication__Issuer, Authentication__MetadataUrl y Authentication__RequireHttpsMetadata. Los secretos de desarrollo se configuran en Bookify.Api; las variables de entorno prevalecen sobre ellos. En produccion proporcionar la seccion externamente, con HTTPS confiable y RequireHttpsMetadata=true: Development no se carga.

El emisor debe concordar con iss y con el hostname publico de Keycloak. La API debe alcanzar tanto MetadataUrl como el jwks_uri anunciado, tambien cuando corre en Docker. localhost dentro de un contenedor es ese contenedor. No desactivar firma, audiencia o emisor para resolver problemas de red.

El client_id no implica automaticamente que exista esa audiencia en aud. Comprobar los tokens reales; para produccion configurar una audiencia dedicada a Bookify y su mapper en Keycloak. RequireHttpsMetadata no habilita HTTPS en la API. No hay validacion propia de estas opciones al arrancar.

### Uso manual

Configurar un cliente en Keycloak y un flujo adecuado: Authorization Code con PKCE para usuarios interactivos o Client Credentials para servicios con cuenta de servicio habilitada. La configuracion de Bookify no crea clientes ni habilita flujos. Consultar las URLs de autorizacion/token en el documento OpenID Connect del realm y utilizar el access_token obtenido, no el id_token.

```http
GET http://localhost:5285/api/apartments?startDate=2026-10-01&endDate=2026-10-05
Authorization: Bearer <access_token>
Accept: application/json
```

En Postman usar Authorization > Bearer Token. Bookify.Api.http ya contiene los endpoints actuales, pero aun no incluye la cabecera Authorization; anadirla para apartamentos. No versionar tokens ni secretos. Program.cs no configura un esquema Bearer ni OAuth en OpenAPI/Swagger: no asumir que [Authorize] habilita por si solo un boton Authorize funcional.

Ante un 401, comprobar vencimiento, firma, aud, iss, realm y conectividad a metadatos/claves, sin publicar tokens completos. Un token valido no demuestra que el usuario sea propietario de una reserva.

### Alcance de las pruebas

Los tests directos de controladores y de ISender no ejecutan autorizacion HTTP. [Api.Tests](../Tests/Bookify.Api.Tests/readme.md) tambien usa WebApplicationFactory para comprobar 401/403, rutas, JSON y middleware, pero sustituye JWT y la consulta de permisos por autenticacion de prueba. No verifica firmas, expiracion, emisor/audiencia ni un realm real. El 22/09/2026 pasan los 57 casos tras agregar BaseUrl ficticia a ApiFactory; aun no hay pruebas dedicadas a /health.

## Inventario de archivos

| Archivo | Responsabilidad |
| --- | --- |
| [Bookify.Api.csproj](Bookify.Api.csproj) | SDK web, dependencias, referencias, identificador de secretos y enlace con Compose. |
| [Program.cs](Program.cs) | Registro de servicios, construccion y ejecucion del host y pipeline por entorno. |
| [Extensions/ApplicationBuilderExtensions.cs](Extensions/ApplicationBuilderExtensions.cs) | Metodo `ApplyMigration`, que crea un scope y aplica migraciones con EF Core. |
| [Extensions/SeedDataExtensions.cs](Extensions/SeedDataExtensions.cs) | Comprueba si apartments esta vacia e inserta 100 ejemplos dentro de una transaccion con bloqueo de tabla. |
| [MiddleWare/ExceptionHandlingMiddleware.cs](MiddleWare/ExceptionHandlingMiddleware.cs) | Captura y registra excepciones; devuelve ProblemDetails 400 para ValidationException y 500 para otros errores. |
| [MiddleWare/ExceptionDetails.cs](MiddleWare/ExceptionDetails.cs) | Record con estado, tipo, titulo, detalle y errores opcionales usado por el middleware. |
| [Controllers/Apartments/ApartmentsController.cs](Controllers/Apartments/ApartmentsController.cs) | Busqueda de disponibilidad y alta mediante `SearchApartmentsQuery` y `CreateApartmentCommand`. |
| [Controllers/Apartments/CreateApartmentRequest.cs](Controllers/Apartments/CreateApartmentRequest.cs) | Cuerpo JSON para dar de alta un apartamento. |
| [Controllers/Bookings/BookingsController.cs](Controllers/Bookings/BookingsController.cs) | Consulta por id y creacion de reservas mediante MediatR. |
| [Controllers/Bookings/ReserveBookingRequest.cs](Controllers/Bookings/ReserveBookingRequest.cs) | Contrato del cuerpo JSON para reservar. |
| [Controllers/Reviews/ReviewsController.cs](Controllers/Reviews/ReviewsController.cs) | Alta de resenas: 201, 404 si falta reserva y 400 si no es elegible. |
| [Controllers/Reviews/CreateReviewRequest.cs](Controllers/Reviews/CreateReviewRequest.cs) | BookingId, Rating y Comment del cuerpo JSON. |
| [Controllers/Users/UsersController.cs](Controllers/Users/UsersController.cs) | Registro, login y perfil con requisitos de rol y permiso. |
| [Controllers/Users/CreateUserRequest.cs](Controllers/Users/CreateUserRequest.cs) | FirstName, LastName, Email y Password de registro. |
| [Controllers/Users/LoginUserRequest.cs](Controllers/Users/LoginUserRequest.cs) | Email y Password de login. |
| [Controllers/Constants/RolesConstants.cs](Controllers/Constants/RolesConstants.cs) | Constante Registered usada en Authorize. |
| [Controllers/Constants/PermissionsConstants.cs](Controllers/Constants/PermissionsConstants.cs) | Constante users:read usada en HasPermission. |
| [appsettings.json](appsettings.json) | Niveles generales de logging y `AllowedHosts`. |
| [appsettings.Development.json](appsettings.Development.json) | Conexion PostgreSQL, JWT y Keycloak local, incluida BaseUrl para salud. No copiar sus secretos a otros documentos. |
| [Properties/launchSettings.json](Properties/launchSettings.json) | Perfiles locales HTTP, HTTPS y Container (Dockerfile). |
| [Dockerfile](Dockerfile) | Construccion por etapas y ejecucion de `Bookify.Api.dll`. |
| [Dockerfile.original](Dockerfile.original) | Copia del Dockerfile inicial, sin copiar las tres bibliotecas antes de restaurar; Compose no la utiliza. |
| [Bookify.Api.http](Bookify.Api.http) | Peticiones de apartamentos, reservas, reviews, Swagger y OpenAPI. Anadir Authorization: Bearer a las peticiones protegidas. |

El archivo `.csproj.user`, si existe en una maquina, contiene preferencias locales de Visual Studio, como el perfil seleccionado. No sustituye la configuracion compartida. Las salidas `bin/` y `obj/` se generan al construir.

## Por que existe esta capa

El controlador conoce HTTP y `ISender`, pero delega disponibilidad, precios, validacion y guardado a las capas internas. Esto mantiene el mismo caso de uso invocable desde otros puntos de entrada. `ReserveBookingRequest` separa el contrato HTTP del comando interno, aunque actualmente sus cuatro campos coincidan.

Api referencia Application para enviar mensajes e Infrastructure para registrar implementaciones y aplicar migraciones. Domain se alcanza transitivamente. El acceso de `ApplyMigration` al contexto es una responsabilidad de arranque del host; los controladores no usan directamente EF.

## Bookify.Api.csproj

Usa `Microsoft.NET.Sdk.Web`, apunta a `net10.0` y activa implicit usings y nullable. Declara:

| Paquete | Version declarada | Uso |
| --- | --- | --- |
| `Microsoft.AspNetCore.OpenApi` | 10.0.11 | Generacion y endpoint del documento OpenAPI. |
| `AspNetCore.HealthChecks.UI.Client` | 9.0.0 | Serializa el informe de /health como JSON compatible con HealthChecks UI; no agrega un panel web. |
| `Bogus` | 35.6.5 | Genera datos ficticios para SeedData. |
| `Swashbuckle.AspNetCore.SwaggerUI` | 10.2.3 | Interfaz web que consume ese documento. |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.3 | Comandos de EF en la consola de paquetes de Visual Studio, como `Add-Migration`. |
| `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` | 1.24.1-preview.1 | Integracion de contenedores y depuracion con Visual Studio. |

Las referencias de proyecto son Application e Infrastructure. `UserSecretsId` identifica los secretos locales de desarrollo, pero no contiene sus valores. `DockerDefaultTargetOS` es Linux y `DockerComposeProjectPath` apunta a `../docker-compose.dcproj`. La CLI `dotnet ef` es una herramienta separada del paquete para la consola de Visual Studio.

## Program.cs

El arranque sigue este orden:

1. `WebApplication.CreateBuilder(args)` configura el host y sus fuentes de configuracion.
2. `AddControllers()` registra controladores; `AddEndpointsApiExplorer()` agrega exploracion de endpoints; `AddOpenApi()` registra la generacion del documento.
3. `AddApplication()` registra MediatR, handlers, behaviors, validadores y precios.
4. `AddInfrastructure(builder.Configuration)` registra persistencia, reloj, correo, autenticacion, autorizacion y health checks.
5. `builder.Build()` construye la aplicacion.
6. Si el entorno es Development, configura OpenAPI y Swagger UI y ejecuta `ApplyMigration()` y despues `SeedData()`.
7. En los demas entornos, agrega `UseHttpsRedirection()`.
8. `UseCustomExceptionHandler()`, `UseAuthentication()` y `UseAuthorization()` forman el pipeline HTTP.
9. `MapControllers()` expone acciones y `MapHealthChecks("health", ...)` publica `/health` con UIResponseWriter.WriteHealthCheckUIResponse.
10. `Run()` inicia la atencion de solicitudes.

| Comportamiento | Development | Otros entornos |
| --- | --- | --- |
| Controladores | Si. | Si. |
| `/openapi/v1.json` | Si. | No se mapea. |
| `/swagger/index.html` | Si. | No se agrega Swagger UI. |
| Migraciones al arrancar | Si, de forma sincronica. | No se invoca `ApplyMigration`. |
| Sembrado de apartamentos | Si la tabla esta vacia, despues de migrar. | No se invoca SeedData. |
| `/health` | Si. | Si. |
| Redireccion HTTP a HTTPS | No. | Si. |

El registro `AddOpenApi()` se ejecuta siempre; el endpoint solo se publica en Development. `MapOpenApi()` proporciona JSON y `UseSwaggerUI()` proporciona la interfaz. Esta ultima se configura con `SwaggerEndpoint("/openapi/v1.json", "Bookify API")`, por lo que no usa `/swagger/v1/swagger.json`.

No hay un endpoint para `/`: un 404 en la raiz no demuestra que la API este detenida. `/health` solo es accesible despues de completar el arranque; un error de configuracion, migracion o sembrado puede impedir que llegue a publicarse.

## Estado de salud: /health

El endpoint se mapea en Program.cs, no en un controlador ni mediante MediatR. Infrastructure registra dos comprobaciones:

| Entrada | Operacion | Configuracion |
| --- | --- | --- |
| `npgsql` | Abre conexion PostgreSQL y ejecuta la comprobacion del proveedor (SELECT 1 por defecto). | ConnectionStrings:Database. |
| `keycloak` | Envia GET a la URL base configurada. | Keycloak:BaseUrl. |

Cada consulta ejecuta los checks registrados: no hay cache ni sondeo periodico propio. La respuesta contiene `status`, `totalDuration` y `entries`, con el estado y duracion de cada dependencia. Healthy y Degraded usan HTTP 200 por defecto; Unhealthy usa 503. El codigo no cambia ese mapeo ni separa liveness de readiness.

```powershell
Invoke-RestMethod -Uri 'http://localhost:5285/health'
```

En Compose la direccion externa es `http://localhost:5000/health`. Tambien existe una peticion en [la coleccion Postman](../Postman/Bookify_AddHealthCheck.postman_collection.json). El paquete UI.Client escribe JSON, pero no registra una pagina HealthChecks UI.

### BaseUrl segun el entorno

| Ejecucion de API | BaseUrl alcanzable desde ese proceso |
| --- | --- |
| Windows, Keycloak en Docker con 18080 publicado | `http://localhost:18080`, valor actual de Development. |
| API y Keycloak en la misma red Compose | `http://bookify-idp:8080`. |
| Fuera de Development | Proporcionar Keycloak:BaseUrl desde configuracion del despliegue, ademas de los otros ajustes. |

**Ajuste pendiente en Compose:** agregar al environment de bookify.api `Keycloak__BaseUrl=http://bookify-idp:8080`. El YAML actual solo sobrescribe AdminUrl y TokenUrl; el valor localhost heredado apunta al contenedor API. Esta documentacion no modifica el YAML.

BaseUrl no sustituye Issuer, MetadataUrl, AdminUrl ni TokenUrl. `new Uri(configuration["KeyCloak:BaseUrl"]!)` se evalua durante AddInfrastructure: si falta, lanza ArgumentNullException antes de arrancar. El operador `!` solo silencia una advertencia del compilador. El nombre KeyCloak/Keycloak no causa el problema porque las claves no distinguen mayusculas.

### Alcance y limites

El check PostgreSQL no valida tablas, migraciones aplicadas ni permisos de escritura. El GET base de Keycloak no comprueba que exista el realm bookify, que sus clientes funcionen ni que un JWT sea valido. Un Healthy no demuestra que una reserva o un login completos puedan realizarse.

La ruta no exige autorizacion y puede publicar informacion operativa y descripciones de errores. Su acceso debe decidirse en el despliegue; no se ha agregado proteccion nueva. El healthcheck de Docker para bookify-db es otro mecanismo: usa pg_isready para ordenar el arranque y no consume `/health`. Tampoco hay un healthcheck Docker configurado para bookify.api ni recuperacion automatica por este endpoint.

## Extensions/SeedDataExtensions.cs

SeedData abre un scope, obtiene ISqlConnectionFactory y crea una transaccion. Ejecuta `LOCK TABLE public.apartments IN SHARE ROW EXCLUSIVE MODE`, comprueba existencia de filas y, si ya hay alguna, confirma la transaccion y termina. Si no hay filas, genera 100 apartamentos con Bogus y los inserta con Dapper antes del commit.

Los nombres tienen exactamente Name.ExactLength (200), las monedas son EUR y las comodidades son Parking y MountainView. No crea usuarios ni reservas y no pasa por los handlers o fabricas de dominio. El bloqueo serializa este proceso frente a otra instancia que ejecute el mismo sembrado.

Se consulta la tabla apartments, no si toda la base esta vacia. No hay una marca persistente de "ejecutado una vez": si se borran todos los apartamentos, el siguiente arranque Development vuelve a sembrar. La migracion debe haberse aplicado previamente. Un fallo interrumpe el arranque; el middleware HTTP no envuelve esta fase.

## Middleware de excepciones

ExceptionHandlingMiddleware envuelve el siguiente delegado, registra la excepcion con ILogger y construye ExceptionDetails. ValidationException produce 400 y agrega sus ValidationError a `errors`; otras excepciones producen 500 con detalle generico, sin devolver el mensaje interno al cliente. Se escribe un ProblemDetails como JSON. No traduce automaticamente todos los Result fallidos: cada controlador decide como devolverlos.

ApplicationBuilderExtensions tambien contiene UseCustomExceptionHandler, que registra este middleware. El comportamiento actual no trata especialmente cancelaciones o respuestas ya iniciadas; los tests caracterizan el funcionamiento existente, no corrigen esas limitaciones.

## Extensions/ApplicationBuilderExtensions.cs

La clase publica estatica expone `ApplyMigration(this IApplicationBuilder app)`:

```csharp
using IServiceScope scope = app.ApplicationServices.CreateScope();
using DbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
context.Database.Migrate();
```

El scope permite resolver correctamente el contexto scoped fuera de una solicitud HTTP. El contexto se usa a traves del tipo base `DbContext`; su instancia real sigue siendo `ApplicationDbContext`, con las configuraciones y el proveedor registrados. Los `using` liberan los recursos al salir.

`Migrate()` aplica migraciones pendientes y registra su historial. No genera archivos de migracion ni inserta datos de ejemplo. Si la base no existe, el proveedor puede crearla cuando el usuario dispone de permisos. No hay captura de errores, reintento propio ni ejecucion en segundo plano: un fallo de conexion, permisos o migracion interrumpe el arranque antes de `Run()`.

El metodo no comprueba el entorno por si mismo; la restriccion a Development esta en quien lo llama, `Program.cs`. Las migraciones pertenecen a Infrastructure y su uso manual se explica en [su guia](../Bookify.Infrastructure/readme.md#migrations).

## Controllers/Apartments/ApartmentsController.cs

Hereda de `ControllerBase`, lleva `[ApiController]` y `[Authorize]` y usa la ruta `api/apartments`. Recibe `ISender` por constructor. Su namespace es `Controllers.Apartments`; la ruta HTTP viene del atributo.

`SearchApartments(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)` responde a GET. Construye `SearchApartmentsQuery(startDate, endDate)`, espera `sender.Send` y devuelve `Ok(result.Value)`.

Ejemplo sobre el puerto publicado por Compose:

```http
GET http://localhost:5000/api/apartments?startDate=2026-10-01&endDate=2026-10-05
```

El nombre de entrada actual es `startDate`. Los parametros de fecha proceden de la query string y el token procede de la solicitud; se necesita tambien Authorization: Bearer.

El resultado exitoso es un array de `ApartmentResponse`: id, nombre, descripcion, precio base, moneda y direccion. No es el precio total de la estancia. Application devuelve una lista vacia si inicio es posterior al fin. El controlador no comprueba `IsFailure` antes de leer `Value`, de modo que un fallo de resultado futuro lanzaria una excepcion. Las excepciones SQL se propagan.

## Alta de apartamentos

`POST /api/apartments` recibe `CreateApartmentRequest`, un record con `Name`, `Description`, los cinco campos planos de direccion (`Country`, `State`, `ZipCode`, `City`, `Street`), `PriceAmount`, `CleaningFeeAmount`, `Currency` y `List<Amenity> Amenities`. `ApartmentsController.CreateApartment` convierte la entrada en `CreateApartmentCommand` y envia el token de la solicitud a MediatR.

El pipeline valida la entrada y el handler guarda mediante el repositorio y la unidad de trabajo. La respuesta exitosa es `201 Created` con el GUID como cuerpo. No se agrega un encabezado Location hacia una consulta por id porque esa ruta de apartamentos no existe actualmente. Los fallos `Result` se convierten en `400` con `Error`; las excepciones de validacion del pipeline se traducen a `400` por el middleware `ExceptionHandlingMiddleware`, ya registrado mediante `UseCustomExceptionHandler()`.

El nombre conserva la regla actual de exactamente 200 caracteres. Descripcion y direccion son obligatorias; descripcion admite hasta 2000 caracteres. Precio debe ser positivo, limpieza admite cero, moneda EUR o USD y comodidades numericas de 1 a 10 sin duplicados; `[]` es valido. El alta utiliza una moneda comun para precio y limpieza.

Ejemplo desde PowerShell, con la API ejecutandose en el perfil HTTP local:

```powershell
$apartment = @{
    name = 'Apartamento Madrid'.PadRight(200, '.')
    description = 'Apartamento con wifi y aparcamiento'
    country = 'Spain'
    state = 'Madrid'
    zipCode = '28001'
    city = 'Madrid'
    street = 'Calle Mayor 1'
    priceAmount = 100
    cleaningFeeAmount = 25
    currency = 'EUR'
    amenities = @(1, 3)
}
Invoke-RestMethod -Method Post -Uri 'http://localhost:5285/api/apartments' -ContentType 'application/json' -Body ($apartment | ConvertTo-Json)
```

En Compose utilizar el puerto publicado 5000. El ejemplo requiere ademas la cabecera Authorization: Bearer con un access token valido. El endpoint no rellena ni recorta nombres silenciosamente. No hace falta otra migracion: se usan las tablas y columnas ya mapeadas por EF.

## Alta de resenas

`POST /api/reviews` se implementa en [ReviewsController](Controllers/Reviews/ReviewsController.cs) y recibe [CreateReviewRequest](Controllers/Reviews/CreateReviewRequest.cs). El controlador envia `CreateReviewCommand` mediante `ISender`, propagando el token de la solicitud.

```json
{
  "bookingId": "11111111-1111-1111-1111-111111111111",
  "rating": 5,
  "comment": "Muy buena estancia y apartamento limpio."
}
```

El id debe corresponder a una reserva existente y completada. La puntuacion admite de 1 a 5 y el comentario es obligatorio, con un maximo de 200 caracteres. El usuario y apartamento se obtienen de la reserva; no se aceptan como identificadores independientes del cuerpo. La fecha procede del reloj UTC de la aplicacion.

Devuelve 201 con el GUID creado, 404 con `BookingErrors.NotFound` si la reserva no existe o 400 con `ReviewErrors.NotEligible` si no esta completada. El middleware convierte errores del validador en 400 y errores tecnicos en 500. No se proporciona Location porque no existe un GET de resena por id.

Reutiliza Review.Create y la tabla reviews, sin una migracion especifica para este endpoint. El evento ReviewCreatedDomainEvent se acumula en el dominio y se publica al guardar; sigue sin tener un handler ni efectos externos asociados. No se agrega una regla de unicidad por reserva ni autenticacion: copiar el autor de la reserva no autoriza al solicitante HTTP. Para usar este endpoint, la reserva debe estar ya completada; no se incorpora aqui un endpoint de finalizacion.

## Controllers/Bookings/BookingsController.cs

Hereda de `ControllerBase`, lleva `[ApiController]`, usa `api/bookings` y recibe `ISender`. Tiene dos acciones:

### GetBooking

`GET /api/bookings/{id}` recibe un `Guid`, construye `GetBookingQuery(id)` y envia el token a MediatR. Devuelve `200` con `BookingResponse` si el resultado tiene exito o `404` sin cuerpo de error de dominio si falla.

El DTO incluye identificadores, estado numerico, importes, monedas, fechas de estancia y creacion. La query usa BookingId, tablas en minusculas y columnas amenities_up_change_* con los alias del DTO. Si la reserva no existe o pertenece a otro usuario, devuelve BookingErrors.NotFound. El endpoint sigue sin [Authorize]: una reserva existente con un principal sin el Guid local puede provocar 500 al acceder a IUserContext. Consultar [GetBooking en Application](../Bookify.Application/readme.md#bookingsgetbooking).

### ReserveBooking

`POST /api/bookings` recibe `ReserveBookingRequest` del cuerpo JSON. Copia sus datos a `ReserveBookingCommand` y llama a `sender.Send(command, cancellationToken)`.

```json
{
  "apartmentId": "11111111-1111-1111-1111-111111111111",
  "userId": "22222222-2222-2222-2222-222222222222",
  "startDate": "2026-10-01",
  "endDate": "2026-10-05"
}
```

Los identificadores son ilustrativos: deben sustituirse por registros existentes. La migracion inicial crea tablas e indices, no usuarios ni apartamentos.

Si `result.IsFailure`, devuelve `BadRequest(result.Error)`, con codigo y descripcion del error. Esto incluye usuario o apartamento inexistentes y solapamiento o conflicto convertido en `Overlap`; no distingue un estado HTTP diferente para cada caso.

Si tiene exito, `CreatedAtAction(nameof(GetBooking), new { id = result.Value }, result.Value)` devuelve `201 Created`, el GUID como cuerpo y un encabezado `Location` hacia la consulta de esa reserva. Que se genere esa URL no verifica que la query de lectura pueda ejecutarse correctamente.

## Controllers/Bookings/ReserveBookingRequest.cs

Record publico sellado con `Guid ApartmentId`, `Guid UserId`, `DateOnly StartDate` y `DateOnly EndDate`. Es un contenedor de datos del contrato HTTP, sin logica de dominio ni validacion propia. La validacion del comando exige GUID no vacios e inicio anterior al fin.

`[ApiController]` participa en el binding y la validacion de modelo HTTP. Eso es distinto de `Bookify.Application.Exceptions.ValidationException` lanzada dentro de MediatR, que ExceptionHandlingMiddleware transforma en un 400 estructurado. No se comprueba que el solicitante este autorizado a usar el UserId del cuerpo.

## Configuracion y perfiles

`appsettings.json` configura logging general en Information, ASP.NET Core en Warning y `AllowedHosts` como `*`. No incluye cadena de conexion. `appsettings.Development.json` agrega `ConnectionStrings:Database` con host `localhost`, puerto 5432, base `bookify` y credenciales de ejemplo `postgres`/`postgres`. Esto permite ejecutar los perfiles `http` y `https` contra PostgreSQL instalado en Windows, sin Docker. Las credenciales reales se configuran con secretos de desarrollo o variables de entorno. `docker-compose.yml` sobrescribe la cadena del contenedor API con `Host=bookify-db` mediante `ConnectionStrings__Database`, que prevalece sobre el JSON y los secretos de desarrollo.

Infrastructure solicita `GetConnectionString("DataBase")`; la configuracion de .NET resuelve las claves sin distinguir mayusculas, por lo que `Database` y `DataBase` coinciden. Una variable `ConnectionStrings__Database` permite sobrescribirla. En otros entornos hay que proporcionar la cadena, pues el archivo Development no se carga.

| Perfil de Properties/launchSettings.json | Direcciones/ajustes |
| --- | --- |
| `http` | `http://localhost:5285`, Development, ejecucion como proyecto. |
| `https` | `https://localhost:7261` y `http://localhost:5285`, Development. |
| `Container (Dockerfile)` | Puertos internos 8080/8081, `publishAllPorts: true`, `useSSL: true`; Visual Studio puede publicar puertos de host distintos de Compose. |

El perfil Docker Compose esta en el `launchSettings.json` de la raiz. Sus puertos publicados se fijan en el override: 5000 y 5001. `launchSettings.json` no es la configuracion que lee un DLL publicado ejecutado directamente; el entorno y las URLs de ese proceso deben proporcionarse mediante configuracion de runtime.

Para correr desde Windows contra PostgreSQL en Docker, ver [ejecucion local](../readme.md#ejecucion-local). Para Compose y pgAdmin, ver [Docker Compose](../readme.md#docker-compose). El host `bookify-db` se resuelve dentro de la red Compose; desde Windows se utiliza `localhost` con el puerto publicado.

## Dockerfile

| Etapa | Funcion |
| --- | --- |
| `base` | Imagen `mcr.microsoft.com/dotnet/aspnet:10.0`, usuario `$APP_UID`, directorio `/app`, puertos expuestos 8080 y 8081. Visual Studio la usa en fast mode. |
| `build` | Imagen SDK 10.0; copia primero los proyectos para restaurar dependencias y despues el codigo. Compila la API, por defecto en Release. |
| `publish` | Publica la API en `/app/publish` con `UseAppHost=false`. |
| `final` | Copia la publicacion a `/app` y ejecuta `dotnet Bookify.Api.dll`. |

Separar las etapas evita necesitar el SDK para ejecutar la imagen final. Copiar primero los archivos de proyecto permite reutilizar la restauracion cuando cambian solo archivos de codigo. `EXPOSE` describe puertos internos, pero su publicacion en el equipo se realiza mediante Compose o Docker.

Visual Studio puede reemplazar el entrypoint por un ayudante de depuracion y montar los binarios, mientras que Compose independiente usa el DLL publicado. Un contenedor activo con el ayudante no garantiza una API activa. Ademas, Release es una configuracion de compilacion y Development es un entorno de ejecucion: el override actual mantiene Development incluso al construir la etapa final.

## Keycloak: desarrollo y produccion

El servicio bookify-idp ejecuta la imagen keycloak/keycloak:latest con `command: ["start-dev", "--import-realm"]`. Es desarrollo, no produccion. La consola local esta en [http://localhost:18080/admin/](http://localhost:18080/admin/) y el puerto se publica solo en 127.0.0.1.

Las variables KC_BOOTSTRAP_ADMIN_USERNAME y KC_BOOTSTRAP_ADMIN_PASSWORD crean el administrador inicial. Actualmente el usuario es admin y la clave se obtiene de KEYCLOAK_ADMIN_PASSWORD, con admin como valor local de ejemplo. No cambian la clave de un administrador existente. Los datos se conservan en ./.containers/identity. El Compose ya monta ./.files/bookify-realm-export.json como /opt/keycloak/data/import/bookify-realm.json, de solo lectura y con create_host_path: false. El origen debe ser un archivo real, no una carpeta; el argumento --import-realm ya esta activo. Un realm existente no se actualiza por volver a importar al arrancar.

Para produccion se utiliza `command: ["start"]`, pero tambien hay que fijar una version concreta de la imagen en lugar de `latest`, configurar una base PostgreSQL y un usuario propios para Keycloak, proporcionar secretos seguros, definir el dominio publico y habilitar HTTPS.

Ejemplo de las opciones de produccion cuando un proxy inverso termina HTTPS (sustituye valores y adapta la red; no es un Compose completo):

```yaml
command: ["start"]
environment:
  KC_HOSTNAME: https://auth.midominio.com
  KC_HTTP_ENABLED: "true"
  KC_PROXY_HEADERS: xforwarded
  KC_DB: postgres
  KC_DB_URL: jdbc:postgresql://servidor-postgres:5432/keycloak
  KC_DB_USERNAME: keycloak
  KC_DB_PASSWORD: ${KEYCLOAK_DB_PASSWORD:?Define la clave}
  KC_BOOTSTRAP_ADMIN_USERNAME: admin
  KC_BOOTSTRAP_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD:?Define la clave}
```

La conexion externa debe ser HTTPS. El puerto HTTP interno debe ser accesible solo desde el proxy, que debe sobrescribir las cabeceras reenviadas. `KC_PROXY_HEADERS` debe corresponder al tipo de cabeceras que realmente configura el proxy. Sin terminacion TLS en un proxy, configura HTTPS y certificados directamente en Keycloak; no expongas HTTP sin proteccion. Las variables del ejemplo deben proporcionarse mediante la configuracion segura del despliegue, sin guardar credenciales reales en el repositorio.

`command: ["start", "--optimized"]` es otra opcion, pero requiere una imagen preparada previamente con `/opt/keycloak/bin/kc.sh build` y las opciones de construccion correspondientes, como el proveedor de base de datos. No se debe agregar `--optimized` sin ese paso previo. Consulta la [guia oficial de contenedores de Keycloak](https://www.keycloak.org/server/containers).

Estas indicaciones no convierten el Compose actual en un despliegue de produccion: la configuracion activa se mantiene en desarrollo. Arrancar Keycloak tampoco configura automaticamente la autenticacion de la API.

### Ejemplo completo: HTTPS directo e importacion inicial

El comentario de `docker-compose.yml` incluye el siguiente ejemplo de **un servicio Keycloak de una sola instancia** con PostgreSQL externo ya preparado. No es un despliegue completo de Bookify ni una configuracion de alta disponibilidad. Crear un archivo independiente `keycloak.production.yml` con este contenido; no combinarlo con el Compose de desarrollo, cuyos puertos, credenciales y volumen H2 no deben heredarse.

```yaml
services:
  bookify-idp:
    image: quay.io/keycloak/keycloak:${KEYCLOAK_VERSION:?Define una version fija}
    command: ["start", "--import-realm"]
    restart: unless-stopped
    environment:
      KC_HOSTNAME: ${KEYCLOAK_PUBLIC_URL:?URL https del dominio publico}
      KC_HTTP_ENABLED: "false"
      KC_DB: postgres
      KC_DB_URL: ${KEYCLOAK_DB_URL:?URL JDBC PostgreSQL con TLS}
      KC_DB_USERNAME: ${KEYCLOAK_DB_USERNAME:?Usuario propio de Keycloak}
      KC_DB_PASSWORD: ${KEYCLOAK_DB_PASSWORD:?Clave de la BD}
      KC_BOOTSTRAP_ADMIN_USERNAME: ${KEYCLOAK_ADMIN_USERNAME:?Administrador inicial}
      KC_BOOTSTRAP_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD:?Clave inicial segura}
      KC_HTTPS_CERTIFICATE_FILE: /opt/keycloak/conf/tls/fullchain.pem
      KC_HTTPS_CERTIFICATE_KEY_FILE: /opt/keycloak/conf/tls/privkey.pem
    ports:
      - "443:8443"
    volumes:
      - type: bind
        source: ${KEYCLOAK_TLS_DIR:?Directorio absoluto con certificados PEM}
        target: /opt/keycloak/conf/tls
        read_only: true
        bind:
          create_host_path: false
      - type: bind
        source: ${KEYCLOAK_DB_CA_FILE:?Ruta absoluta al certificado CA de PostgreSQL}
        target: /opt/keycloak/conf/db-ca.pem
        read_only: true
        bind:
          create_host_path: false
      - type: bind
        source: ${KEYCLOAK_REALM_FILE:?Ruta absoluta al JSON exportado}
        target: /opt/keycloak/data/import/bookify-realm.json
        read_only: true
        bind:
          create_host_path: false
```

Preparativos obligatorios:

1. Fijar `KEYCLOAK_VERSION` a una version soportada y probada (no `latest`), y comprobar su compatibilidad con PostgreSQL antes de actualizar.
2. Configurar DNS para el servidor y `KEYCLOAK_PUBLIC_URL=https://auth.midominio.com`. Abrir 443 y comprobar que no lo ocupa otro servicio.
3. Preparar `KEYCLOAK_TLS_DIR` con `fullchain.pem` y `privkey.pem` coincidentes, emitidos para ese dominio. Deben ser legibles por el usuario del contenedor, sin dar permisos globales a la clave privada. Organizar su renovacion.
4. Crear previamente una base `keycloak` y un usuario propio, propietario de esa base y con permisos para crear y actualizar sus tablas, pero sin privilegios de superusuario. No usar las tablas de Bookify ni la base H2 de desarrollo. Preparar copias de seguridad y probar la restauracion.
5. Habilitar TLS en PostgreSQL y permitir en el firewall y `pg_hba.conf` solo el acceso necesario. Definir `KEYCLOAK_DB_CA_FILE` como archivo PEM de la CA que firma el certificado del servidor. Ejemplo de `KEYCLOAK_DB_URL`: `jdbc:postgresql://db.midominio.com:5432/keycloak?sslmode=verify-full&sslrootcert=/opt/keycloak/conf/db-ca.pem`. El nombre debe coincidir con el certificado y resolverse desde el contenedor. Su `localhost` no es la maquina anfitriona.
6. Inyectar usuario/clave de BD y administrador inicial mediante el despliegue. No versionar secretos, claves privadas ni exports que contengan credenciales. La interpolacion de variables no cifra secretos: restringir tambien acceso a Docker y al entorno del proceso. No publicar la salida completa de `docker compose config`.
7. Definir `KEYCLOAK_REALM_FILE` con la ruta absoluta a un JSON exportado real cuyo campo `realm` sea `bookify`. El montaje usa `bookify-realm.json` dentro del contenedor. `create_host_path: false` evita crear una carpeta cuando falta el archivo; comprobar ademas que el origen no sea ya una carpeta.

Un JSON minimo de arranque es:

```json
{
  "realm": "bookify",
  "enabled": true,
  "displayName": "Bookify",
  "registrationAllowed": false
}
```

Este JSON solo crea el realm; configurar los clientes, roles, URLs de redireccion y origenes permitidos antes de utilizarlo con aplicaciones. Un export completo puede contener secretos y debe tratarse como material sensible. La importacion de arranque omite un realm existente: no es un mecanismo de actualizacion ni una copia de seguridad de la BD. Para no importar, cambiar el comando a `["start"]` y eliminar el montaje del JSON.

Con las variables ya disponibles en el entorno del despliegue:

```powershell
docker compose -f keycloak.production.yml config --quiet
docker compose -f keycloak.production.yml up -d bookify-idp
docker compose -f keycloak.production.yml logs --tail 100 bookify-idp
```

Estas instrucciones son una plantilla: ese archivo de produccion no se crea ni se ejecuta como parte de este cambio documental. Verificar HTTPS sin desactivar la validacion de certificados en `https://auth.midominio.com/realms/bookify/.well-known/openid-configuration` y comprobar que `issuer` coincide con el dominio previsto. Tras el bootstrap, crear administradores permanentes, protegerlos con MFA y retirar la cuenta temporal y sus variables de bootstrap. Restringir la consola administrativa mediante controles de red/acceso; `KC_HOSTNAME` no es una regla de firewall.

El ejemplo no publica 8080 ni el puerto de gestion. Preparar monitorizacion, alertas, recursos suficientes y un procedimiento de actualizacion y recuperacion antes de operar en produccion. Para varias replicas se necesita disenar adicionalmente balanceo, descubrimiento/cache y disponibilidad de la BD. La alternativa con proxy descrita arriba sustituye la terminacion TLS directa; no habilitar HTTP publico mezclando ambos ejemplos.

Referencias: [TLS de Keycloak](https://www.keycloak.org/server/enabletls), [PostgreSQL y otras bases](https://www.keycloak.org/server/db), [importacion de realms](https://www.keycloak.org/server/importExport).


## Comprobaciones y limites actuales

`Bookify.Api.http` usa el host local 5285, pero no contiene aun login, registro, perfil ni /health. Ademas, su ejemplo de nombre de apartamento no alcanza los 200 caracteres exigidos y conserva un comentario antiguo que niega el alta de usuarios. Sustituir identificadores, ajustar el nombre y agregar Authorization a las peticiones protegidas; no obtiene tokens automaticamente. Estos limites del archivo se documentan, no se modifica el .http en esta revision.

`Dockerfile.original` conserva las etapas de la plantilla y solo copia el proyecto Api antes de `restore`. El Dockerfile activo copia tambien los proyectos referenciados antes de restaurar. Compose apunta expresamente a `Bookify.Api/Dockerfile`; la copia `.original` no interviene en esa construccion.

- Swagger y OpenAPI requieren que el host haya completado `ApplyMigration()` y que el entorno sea Development.
- La base debe estar lista al arrancar; Compose ahora espera el healthcheck TCP de PostgreSQL mediante `depends_on: condition: service_healthy`. Una caida posterior sigue requiriendo tratamiento de errores y recuperacion.
- HTTPS requiere configurar y confiar en el certificado local; montar la carpeta no resuelve automaticamente ambas cosas.
- La migracion Change_TableName_And_Field ya alinea los nombres en minusculas; GetBooking tiene corregidos parametro y columnas. Aplicar todas las migraciones, no solamente la inicial.
- El middleware propio devuelve 400 para `ValidationException` y 500 para otras excepciones. JWT y [Authorize] protegen apartamentos; reservas y reviews no tienen esa proteccion.
- Existen altas de apartamentos, resenas y usuarios mediante `POST /api/apartments`, `POST /api/reviews` y `POST /api/users/register`. No existen endpoints de confirmacion ni cancelacion.

Al agregar un endpoint, definir el contrato HTTP, enviar el mensaje apropiado por `ISender`, comprobar los resultados y concretar el tratamiento de excepciones. Mantener las reglas y el guardado en las capas que ya los poseen.
