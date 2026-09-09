# Bookify.Api

Esta es la capa de entrada HTTP y el host ejecutable de Bookify. Recibe solicitudes, convierte sus datos en comandos o queries de Application y adapta los resultados a respuestas HTTP. Tambien compone las dependencias de las capas y configura OpenAPI, Swagger UI y la aplicacion de migraciones durante desarrollo.

[Guia de la solucion](../readme.md) | [Domain](../Bookify.Domain/readme.md) | [Application](../Bookify.Application/readme.md) | [Infrastructure](../Bookify.Infrastructure/readme.md)

## Inventario de archivos

| Archivo | Responsabilidad |
| --- | --- |
| [Bookify.Api.csproj](Bookify.Api.csproj) | SDK web, dependencias, referencias, identificador de secretos y enlace con Compose. |
| [Program.cs](Program.cs) | Registro de servicios, construccion y ejecucion del host y pipeline por entorno. |
| [Extensions/ApplicationBuilderExtensions.cs](Extensions/ApplicationBuilderExtensions.cs) | Metodo `ApplyMigration`, que crea un scope y aplica migraciones con EF Core. |
| [Controllers/Apartments/ApartmentsController.cs](Controllers/Apartments/ApartmentsController.cs) | Consulta HTTP de disponibilidad mediante `SearchApartmentsQuery`. |
| [Controllers/Bookings/BookingsController.cs](Controllers/Bookings/BookingsController.cs) | Consulta por id y creacion de reservas mediante MediatR. |
| [Controllers/Bookings/ReserveBookingRequest.cs](Controllers/Bookings/ReserveBookingRequest.cs) | Contrato del cuerpo JSON para reservar. |
| [appsettings.json](appsettings.json) | Niveles generales de logging y `AllowedHosts`. |
| [appsettings.Development.json](appsettings.Development.json) | Conexion de desarrollo a PostgreSQL y logging. |
| [Properties/launchSettings.json](Properties/launchSettings.json) | Perfiles locales HTTP, HTTPS y Container (Dockerfile). |
| [Dockerfile](Dockerfile) | Construccion por etapas y ejecucion de `Bookify.Api.dll`. |
| [Dockerfile.original](Dockerfile.original) | Copia del Dockerfile inicial, sin copiar las tres bibliotecas antes de restaurar; Compose no la utiliza. |
| [Bookify.Api.http](Bookify.Api.http) | Solicitud de ejemplo de la plantilla a `/weatherforecast/`, que ya no corresponde a un endpoint existente. |

El archivo `.csproj.user`, si existe en una maquina, contiene preferencias locales de Visual Studio, como el perfil seleccionado. No sustituye la configuracion compartida. Las salidas `bin/` y `obj/` se generan al construir.

## Por que existe esta capa

El controlador conoce HTTP y `ISender`, pero delega disponibilidad, precios, validacion y guardado a las capas internas. Esto mantiene el mismo caso de uso invocable desde otros puntos de entrada. `ReserveBookingRequest` separa el contrato HTTP del comando interno, aunque actualmente sus cuatro campos coincidan.

Api referencia Application para enviar mensajes e Infrastructure para registrar implementaciones y aplicar migraciones. Domain se alcanza transitivamente. El acceso de `ApplyMigration` al contexto es una responsabilidad de arranque del host; los controladores no usan directamente EF.

## Bookify.Api.csproj

Usa `Microsoft.NET.Sdk.Web`, apunta a `net10.0` y activa implicit usings y nullable. Declara:

| Paquete | Version declarada | Uso |
| --- | --- | --- |
| `Microsoft.AspNetCore.OpenApi` | 10.0.11 | Generacion y endpoint del documento OpenAPI. |
| `Swashbuckle.AspNetCore.SwaggerUI` | 10.2.3 | Interfaz web que consume ese documento. |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.3 | Comandos de EF en la consola de paquetes de Visual Studio, como `Add-Migration`. |
| `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` | 1.24.1-preview.1 | Integracion de contenedores y depuracion con Visual Studio. |

Las referencias de proyecto son Application e Infrastructure. `UserSecretsId` identifica los secretos locales de desarrollo, pero no contiene sus valores. `DockerDefaultTargetOS` es Linux y `DockerComposeProjectPath` apunta a `../docker-compose.dcproj`. La CLI `dotnet ef` es una herramienta separada del paquete para la consola de Visual Studio.

## Program.cs

El arranque sigue este orden:

1. `WebApplication.CreateBuilder(args)` configura el host y sus fuentes de configuracion.
2. `AddControllers()` registra controladores; `AddEndpointsApiExplorer()` agrega exploracion de endpoints; `AddOpenApi()` registra la generacion del documento.
3. `AddApplication()` registra MediatR, handlers, behaviors, validadores y precios.
4. `AddInfrastructure(builder.Configuration)` registra EF, repositorios, unidad de trabajo, conexiones SQL, reloj y correo.
5. `builder.Build()` construye la aplicacion.
6. Si el entorno es Development, configura OpenAPI y Swagger UI y ejecuta `ApplyMigration()`.
7. En los demas entornos, agrega `UseHttpsRedirection()`.
8. `MapControllers()` expone las acciones y `Run()` inicia la atencion de solicitudes.

| Comportamiento | Development | Otros entornos |
| --- | --- | --- |
| Controladores | Si. | Si. |
| `/openapi/v1.json` | Si. | No se mapea. |
| `/swagger/index.html` | Si. | No se agrega Swagger UI. |
| Migraciones al arrancar | Si, de forma sincronica. | No se invoca `ApplyMigration`. |
| Redireccion HTTP a HTTPS | No. | Si. |

El registro `AddOpenApi()` se ejecuta siempre; el endpoint solo se publica en Development. `MapOpenApi()` proporciona JSON y `UseSwaggerUI()` proporciona la interfaz. Esta ultima se configura con `SwaggerEndpoint("/openapi/v1.json", "Bookify API")`, por lo que no usa `/swagger/v1/swagger.json`.

No hay un endpoint para `/`, middleware propio de excepciones, autenticacion, autorizacion ni health checks configurados. Una respuesta 404 en la raiz no demuestra que la API este detenida.

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

Hereda de `ControllerBase`, lleva `[ApiController]` y usa la ruta `api/apartments`. Recibe `ISender` por constructor. Su namespace actual termina en `Controllers.Apartment` (singular), aunque la carpeta sea `Apartments`; la ruta HTTP viene del atributo.

`SearchApartments(DateOnly starDate, DateOnly endDate, CancellationToken cancellationToken)` responde a GET. Construye `SearchApartmentsQuery(starDate, endDate)`, espera `sender.Send` y devuelve `Ok(result.Value)`.

Ejemplo sobre el puerto publicado por Compose:

```http
GET http://localhost:5000/api/apartments?starDate=2026-10-01&endDate=2026-10-05
```

El nombre de entrada es literalmente `starDate`, sin la segunda `t` de `startDate`. No hay un alias configurado. Los parametros de fecha proceden de la query string y el token procede de la solicitud.

El resultado exitoso es un array de `ApartmentResponse`: id, nombre, descripcion, precio base, moneda y direccion. No es el precio total de la estancia. Application devuelve una lista vacia si inicio es posterior al fin. El controlador no comprueba `IsFailure` antes de leer `Value`, de modo que un fallo de resultado futuro lanzaria una excepcion. Las excepciones SQL se propagan.

## Controllers/Bookings/BookingsController.cs

Hereda de `ControllerBase`, lleva `[ApiController]`, usa `api/bookings` y recibe `ISender`. Tiene dos acciones:

### GetBooking

`GET /api/bookings/{id}` recibe un `Guid`, construye `GetBookingQuery(id)` y envia el token a MediatR. Devuelve `200` con `BookingResponse` si el resultado tiene exito o `404` sin cuerpo de error de dominio si falla.

El DTO incluye identificadores, estado numerico, importes, monedas, fechas de estancia y creacion. Ya estan corregidas las monedas como `string`, el importe `PriceAmount` y los nombres `DurationStart`/`DurationEnd`. Siguen pendientes diferencias del parametro SQL y del esquema; un error tecnico no se convierte en el `404` del resultado. Consultar [GetBooking en Application](../Bookify.Application/readme.md#bookingsgetbooking).

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

`[ApiController]` participa en el binding y la validacion de modelo HTTP. Eso es distinto de la excepcion `Bookify.Application.Exceptions.ValidationException` lanzada dentro de MediatR: actualmente no hay middleware que la transforme en un `400` estructurado. Tampoco se comprueba que el solicitante este autorizado a usar el `UserId` del cuerpo.

## Configuracion y perfiles

`appsettings.json` configura logging general en Information, ASP.NET Core en Warning y `AllowedHosts` como `*`. No incluye cadena de conexion. `appsettings.Development.json` agrega `ConnectionStrings:Database` con host `bookify-db`, puerto 5432, base `bookify` y credenciales locales `postgres`/`postgres`.

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

## Comprobaciones y limites actuales

`Bookify.Api.http` conserva una variable de host `http://localhost:5285` y un GET a `/weatherforecast/` con `Accept: application/json`. Puede abrirse en el cliente HTTP del IDE, pero esa ruta no existe en los controladores actuales: para probar Bookify hay que usar las rutas de apartamentos y reservas explicadas arriba.

`Dockerfile.original` conserva las etapas de la plantilla y solo copia el proyecto Api antes de `restore`. El Dockerfile activo copia tambien los proyectos referenciados antes de restaurar. Compose apunta expresamente a `Bookify.Api/Dockerfile`; la copia `.original` no interviene en esa construccion.

- Swagger y OpenAPI requieren que el host haya completado `ApplyMigration()` y que el entorno sea Development.
- La base debe estar lista al arrancar; Compose no espera su disponibilidad mediante healthcheck.
- HTTPS requiere configurar y confiar en el certificado local; montar la carpeta no resuelve automaticamente ambas cosas.
- La migracion genera tablas con mayusculas y Dapper consulta nombres sin comillas en minusculas. El parametro y las columnas de recargo de GetBooking tambien requieren alineacion.
- No hay middleware propio para excepciones de validacion o errores tecnicos, ni autenticacion o autorizacion.
- No existen endpoints de confirmacion, cancelacion, altas de usuarios/apartamentos ni resenas, ni datos iniciales para probar esos flujos.

Al agregar un endpoint, definir el contrato HTTP, enviar el mensaje apropiado por `ISender`, comprobar los resultados y concretar el tratamiento de excepciones. Mantener las reglas y el guardado en las capas que ya los poseen.
