# Pruebas de Bookify.Api

## Objetivo

El proyecto comprueba el contrato HTTP de la API, la traduccion entre peticiones y comandos/queries, el middleware de excepciones y las extensiones de arranque. Mantiene los casos existentes y amplia la bateria de 6 a 57 casos.

Utiliza MSTest 4 con VSTest, FluentAssertions y Moq. `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 permite ejecutar el Program.cs real mediante WebApplicationFactory y TestServer. No se ha cambiado codigo de produccion ni se ha hecho publico Program: la factoria usa UsersController como tipo marcador del ensamblado.

## Ejecutar

Desde la raiz del repositorio:

```powershell
dotnet test Tests/Bookify.Api.Tests/Bookify.Api.Tests.csproj
dotnet test Tests/Bookify.Api.Tests/Bookify.Api.Tests.csproj --filter "TestCategory=Http"
dotnet test Tests/Bookify.Api.Tests/Bookify.Api.Tests.csproj --collect "Code Coverage;Format=Cobertura"
```

Los tests no necesitan Docker, PostgreSQL ni Keycloak. Las categorias disponibles son Controller, Http, Middleware, Extensions y Authentication. La ultima conserva la prueba previa del adaptador de registro de Infrastructure, cuya bateria principal esta en Infrastructure.Tests.

## Ajuste a Outbox y Quartz

Solventados errores tras incorporar Quartz que procedian del arranque del planificador dentro de los hosts efimeros; aparecian errores del scheduler y LoggerFactory ya liberado.

ApiFactory conserva la configuracion Outbox de prueba y el constructor actualizado de ApplicationDbContext con IDateTimeProvider. En ConfigureTestServices retira exclusivamente el IHostedService cuya implementacion es Quartz.QuartzHostedService, sin eliminar los demas servicios alojados. Se identifica por nombre completo porque ese tipo es interno en el paquete actual.

Los tests HTTP no deben ejecutar el procesador SQL contra una base real ni depender de temporizadores. Este aislamiento no valida el funcionamiento del job: las rutas, autenticacion de prueba, middleware, migraciones simuladas y sembrado conservan sus comprobaciones anteriores.

## Tipos de pruebas

### Controladores

Los tests directos comprueban comandos y queries, mapeo de todos los campos, reenvio del CancellationToken y resultados MVC. Los mocks estrictos de ISender permiten verificar la llamada exacta y evitar interacciones inesperadas.

Los casos originales de apartamento y review tambien recorren MediatR, los validadores y handlers reales con repositorios simulados. El alta de apartamento comprueba ahora que el identificador devuelto sea el de la entidad entregada al repositorio, no solamente que sea un Guid no vacio.

Invocar directamente un controlador no ejecuta model binding, filtros ni autorizacion. Esas comprobaciones se realizan con peticiones HTTP en ApiHttpTests.

### HTTP y arranque

ApiFactory arranca Program.cs con configuracion ficticia, incluida BaseUrl, proporcionada antes de que Program registre Infrastructure. No modifica variables de entorno globales; el objetivo es evitar dependencia de secretos personales y servicios externos.

En el entorno Testing se ejecuta la rama no Development. Se sustituye ISender, se usa un esquema de autenticacion exclusivo de tests y se reemplaza la transformacion de claims para evitar consultas a PostgreSQL. La politica users:read exige un claim de prueba; los atributos de autorizacion de los controladores y el middleware ASP.NET Core siguen siendo reales.

Las cabeceras X-Test-User, X-Test-Role y X-Test-Permission solo existen en el host de tests. No se registra ese esquema en la aplicacion de produccion.

En Development se comprueban Swagger y OpenAPI. Antes del arranque se sustituyen ApplicationDbContext y el migrador, y se inyecta una conexion SQL simulada para SeedData. Asi se verifica que Program invoque las extensiones sin ejecutar DDL o INSERT contra una base real.

### Persistencia de arranque simulada

SeedTestContext deja que Dapper construya comandos y parametros reales sobre interfaces de conexion, comando y transaccion simuladas. Comprueba bloqueo antes de consultar existencia, 100 inserciones cuando no hay apartamentos, ausencia de nuevas inserciones cuando existen, commit y liberacion de recursos.

Las pruebas no ejecutan el SQL en PostgreSQL ni demuestran que el bloqueo evite carreras reales. La prueba de segundo arranque simula que la consulta de existencia ya encuentra datos; no es una base de datos en memoria.

## Archivos

| Archivo | Casos principales |
| --- | --- |
| `Controllers/Apartments/ApartmentControllerTests.cs` | Alta mediante el pipeline real, campos entregados al repositorio, identificador persistido y respuesta de busqueda. |
| `Controllers/Reviews/ReviewControllerTests.cs` | Reserva inexistente, reserva completada y reserva no elegible mediante el pipeline real. Contexto compartido por clase, independiente por caso. |
| `Controllers/Bookings/BookingsControllerTests.cs` | GetBooking 200/404, reserva 201/400, mapeo de campos y token, accion y route values de CreatedAtAction. |
| `Controllers/Users/UsersControllerTests.cs` | Registro 200/400, login 200/401, perfil, mapeo de comandos y token de cancelacion. |
| `Controllers/ControllerContractTests.cs` | Mapeo completo de CreateApartmentRequest, resultado fallido de apartamento, tres respuestas del alta de review y busqueda sin resultados. |
| `Http/ApiHttpTests.cs` | Rutas reales, JSON, fechas en query, Location navegable, 401 anonimo y 403 por falta de rol/permiso, acceso anonimo a login/registro, JSON y Guid invalidos, excepciones 400/500, Swagger/OpenAPI solo en Development. |
| `Middleware/ExceptionHandlingMiddlewareTests.cs` | Paso normal sin logs, captura y log de excepciones, ExceptionDetails y ProblemDetails, errores de validacion y ocultacion de detalles internos. Tambien UseCustomExceptionHandler en un pipeline real. |
| `Extensions/SeedDataExtensionsTests.cs` | Tabla vacia o poblada, orden de operaciones, contenido generado, transaccion comun, fallo de insercion y segundo arranque. |
| `Extensions/ApplicationBuilderExtensionsTests.cs` | ApplyMigration invoca IMigrator dentro de un scope y libera el contexto en exito o fallo. |
| `Authentication/AuthenticationServiceTests.cs` | Prueba original conservada: contrato JSON de registro a Keycloak y extraccion del identificador desde Location. |
| `Support/ApiTestData.cs` | Peticiones y valores comunes para escenarios, sin compartir listas mutables entre casos. |
| `Support/ApiFactory.cs` | Host HTTP de pruebas, configuracion temprana, autenticacion simulada y sustituciones de servicios externos. |
| `Support/SeedTestContext.cs` | Captura de SQL, parametros y transacciones para ejecutar Dapper sin servidor. |

Los records de peticion se prueban al construir comandos y al deserializar peticiones HTTP. RolesConstants y PermissionsConstants se ejercitan con las restricciones reales de UsersController.


## Limites y comportamientos actuales

- La autorizacion HTTP prueba que los atributos exijan autenticacion, rol y politica. No valida firmas JWT, discovery OIDC, roles de Keycloak ni la consulta real de permisos de Infrastructure.
- BookingsController y ReviewsController no tienen Authorize actualmente. Esta tarea no incorpora proteccion nueva ni afirma que esos endpoints esten protegidos.
- El DTO AccessTokenResponse expone AccessToke, por lo que el JSON actual es accessToke. El test hace visible ese contrato; no corrige el nombre a accessToken.
- SearchApartments y LogInUser acceden a Result.Value sin tratar IsFailure. Las pruebas de exito no implican que esos caminos gestionen correctamente todos los fallos posibles.
- El middleware actual transforma excepciones distintas de ValidationException en 500. No se ha cambiado su tratamiento de cancelacion ni su comportamiento cuando una respuesta ya ha empezado.
- La prueba de Email/Keycloak anterior permanece para no eliminar cobertura existente, aunque su responsabilidad principal pertenece a Infrastructure.Tests.
- Se comprueban migraciones y sembrado mediante dobles, no la validez del SQL, bloqueos o rollback reales. Esas garantias requieren pruebas de integracion con PostgreSQL.
