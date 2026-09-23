# Pruebas de Bookify.Infrastructure

## Objetivo

El proyecto comprueba los adaptadores de Infrastructure: autenticacion HTTP, claims, politicas de permisos, configuracion de EF Core, repositorios, migraciones, preparacion de mensajes Outbox y registro de dependencias.

Usa MSTest 4 con el runner VSTest (`Microsoft.NET.Test.Sdk` y `MSTest.TestAdapter`), FluentAssertions y Moq, con las versiones que ya usa Application.Tests. Referencia Infrastructure directamente, sin cargar la API. `InternalsVisibleTo` permite comprobar las clases internas sin hacerlas publicas.

## Estado actual

Cinco esperaban el antiguo contrato de eventos y dos no aportaban IHostApplicationLifetime, ahora necesario para validar el servicio alojado de Quartz.

DependencyInjectionTests aporta un doble de IHostApplicationLifetime, configura IntervalInSeconds/BatchSize y mantiene ValidateScopes/ValidateOnBuild. Comprueba el registro de Quartz y sus opciones sin iniciar el scheduler. Se agregan tambien un caso de reintento sobre el mismo contexto y la generacion SQL de avance/retroceso de Add_OutBoxMessages.

CreateProvider define Keycloak:BaseUrl con https://identity.example. Esto resuelve los dos fallos de DependencyInjectionTests que se producian al construir la Uri del nuevo health check.

La URL ficticia permite registrar servicios sin conectarse a Keycloak. Queda pendiente probar expresamente la ejecucion de los nuevos checks: sus implementaciones Npgsql/URL no usan los mocks de ApplicationDbContext, ISqlConnectionFactory o JwtService y deben sustituirse especificamente para no abrir red.

Las correcciones de permisos se han contrastado con el codigo, no se presentan como una nueva validacion contra un servidor real.

La revision estatica detecta ademas una asercion desactualizada en AuthorizationService_MissingUser_Should_ReportCurrentFirstAsyncFailure: espera excepcion tanto de roles como de permisos. Ahora solo GetRolesForUserAsync lanza; GetPermissionsForUserAsync devuelve conjunto vacio. Al habilitar PostgreSQL, esa expectativa de permisos debe actualizarse. No se ha ejecutado ni corregido ese caso aqui.

## Ejecucion sin servicios externos

Desde la raiz del repositorio:

```powershell
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "TestCategory=Infrastructure"
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "TestCategory=Infrastructure" --collect "Code Coverage;Format=Cobertura"
```

Las peticiones de Keycloak terminan en un HttpMessageHandler local: no se conectan a un servidor real. Los tests de modelo usan el proveedor Npgsql real para generar metadatos y SQL, pero no abren conexiones. Los tests de SaveChanges emplean un interceptor para controlar el resultado de persistencia; no demuestran que se haya escrito nada en PostgreSQL.

## Integracion con PostgreSQL

Utilizar exclusivamente un servidor de pruebas, con un usuario que pueda crear bases de datos. No se lee appsettings ni se reutiliza la base de desarrollo.

```powershell
$env:BOOKIFY_TEST_POSTGRES = "Host=localhost;Port=5432;Database=postgres;Username=USUARIO_DE_TEST;Password=CLAVE_DE_TEST"
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "TestCategory=PostgreSQL"
```

La cadena anterior es una plantilla; no contiene credenciales reales. No debe guardarse la cadena real en el repositorio. Para ejecutar ambos grupos, omitir el filtro.

Cada caso crea una base `bookify_test_<guid>`, aplica las migraciones y usa solo esa base. Al terminar, libera el contexto y elimina unicamente la base cuyo nombre genero ese caso. El nombre nunca procede de la variable de entorno y se desactiva el pooling de las conexiones de prueba. Las pruebas de rollback y borrado de datos trabajan dentro de esa base temporal.

Sin `BOOKIFY_TEST_POSTGRES`, MSTest marca los casos como inconclusos/omitidos: no se cuentan como correctos. Si se configura una cadena invalida o el servidor no responde, el caso falla; no se oculta el error como una omision. Una terminacion forzada del proceso puede impedir la limpieza y dejar bases temporales que habra que revisar.

## Archivos y casos

| Archivo | Funcionamiento comprobado |
| --- | --- |
| `Authentication/JwtServiceTests.cs` | Formulario de login, codificacion de caracteres especiales, client id/secret, scope, grant type y token. HTTP 400/401/500, fallo de red, JSON nulo o invalido y cancelacion. |
| `Authentication/AdminAuthorizationDelegatingHandlerTests.cs` | Obtencion del token de administrador con client_credentials, orden de las dos peticiones, sustitucion del header Authorization y errores en cada etapa. |
| `Authentication/AuthenticationServiceTests.cs` | JSON exacto de registro, credencial no temporal, extraccion de identidad desde Location, ausencia del header y error de red. Tambien valores iniciales de los DTO y nombre JSON access_token. |
| `Authentication/ClaimsTests.cs` | ClaimsPrincipalExtensions y UserContext: distincion entre identidad externa y Guid local, claims ausentes, Guid invalido y HttpContext inexistente. |
| `Authorization/PolicyTests.cs` | HasPermissionAttribute, PermissionRequirement y provider: creacion y reutilizacion de politicas, respeto de politicas configuradas/default/fallback. Salida temprana de usuarios anonimos y de claims ya enriquecidos. |
| `Authorization/PostgresAuthorizationTests.cs` | AuthorizationService y UserRolesResponse con datos reales, ausencia de usuario, transformacion de claims e idempotencia, permiso requerido, usuario sin permisos y union de permisos de varios roles. El codigo ya corrige las dos regresiones de permisos descritas abajo. |
| `Persistence/ApplicationDbContextTests.cs` | SaveChangesAsync: prepara Outbox antes de guardar, comprueba id/tipo/fecha/contenido deserializable y ausencia de llamadas a IPublisher. Verifica resultado, token, traduccion de concurrencia, propagacion de errores/cancelacion, mensajes pendientes en el seguimiento y ausencia de duplicados al volver a guardar o reintentar en el mismo contexto. |
| `Persistence/ModelConfigurationTests.cs` | Las siete configuraciones: tablas, claves, longitudes y conversiones de objetos de valor, owned types, relaciones, indices unicos, token xmin, tabla intermedia de permisos y datos iniciales. Evita la regresion de una FK RoleId dentro de permissions. |
| `Persistence/MigrationTests.cs` | Generacion SQL de avance y retroceso para las seis migraciones, incluida Add_OutBoxMessages. Comprueba las tablas de permisos/outbox y ausencia de diferencias pendientes con el Snapshot. No ejecuta ese SQL. |
| `Persistence/RepositoryTrackingTests.cs` | Add de los cuatro repositorios y la base Repository: estado Added de entidades, rol existente Unchanged y ausencia de guardado o limpieza de eventos al agregar. |
| `Persistence/PostgresRepositoryTests.cs` | Persistencia y lectura de los cuatro agregados, identificadores inexistentes, materializacion real, estados y limites inclusivos de solapamiento, xmin con dos contextos, SqlConnectionFactory, Dapper/DateOnly y migraciones completas con rollback y seeds. |
| `DependencyInjectionTests.cs` | AddInfrastructure, opciones de Keycloak/JWT, ambos overloads de JwtBearerOptionsSetup, resolucion de servicios, contexto y unidad de trabajo compartidos por scope, singleton SQL y fallo si falta la conexion. |
| `Data/AdapterTests.cs` | DateOnlyTypeHandler al leer y escribir, entrada de tipo incorrecto, DateTimeProvider y servicio de correo actual, que no realiza envio externo. |
| `Support/InfrastructureTestData.cs` | Factorias de entidades y opciones ficticias, fechas fijas y contextos Npgsql. Sustituye la referencia al rol estatico por una instancia local para evitar que el relationship fixup de EF contamine otras pruebas. |
| `Support/RecordingHttpHandler.cs` | Transporte HTTP en memoria que captura metodo, URI, contenido y cabeceras. Permite respuestas programadas y excepciones sin red. |
| `Support/PostgresTestBase.cs` | Creacion y limpieza de bases aisladas por caso, migraciones y datos iniciales de reservas usando operaciones de dominio. |

Los archivos generados Designer y Snapshot se ejercitan al descubrir migraciones, construir modelos y generar scripts; no se editan ni se duplican como tests de cada linea generada.


## Problemas detectados y limites


- PermissionAuthorizationHandler ya usa Contains(requirement.Permission), no Count > 0. Tener un permiso diferente no satisface el requisito.
- GetPermissionsForUserAsync ya aplana todos los roles y permisos, aplica Distinct, materializa con ToListAsync y construye HashSet. La regresion con varios roles comprueba esa union sin depender del orden.
- GetRolesForUserAsync conserva FirstAsync sobre el usuario y lanza si no existe; devuelve todos los roles de ese usuario, no solo uno. La consulta de permisos, en cambio, devuelve un conjunto vacio sin usuario/roles/permisos. La transformacion de claims puede fallar antes de llegar al handler de permisos.
- SaveChanges ahora convierte los eventos en Outbox antes de llamar a EF y vacia la lista de dominio. Si el guardado falla, los mensajes siguen Added en el contexto, pero no se demuestra que esten persistidos. Los tests usan un interceptor que suprime la escritura; no prueban atomicidad ni entrega. IPublisher ya no se invoca desde SaveChanges.
- ProcessedOnUtc y Error son ahora anulables y los mensajes nuevos empiezan con ambos valores null. La migracion regenerada 20260923111807_Add_OutBoxMessages admite NULL en ambas columnas. Los tests comprueban esos valores iniciales y la nulabilidad del modelo EF. El procesador Quartz sigue requiriendo pruebas propias; generar SQL no equivale a ejecutar el job contra PostgreSQL.
- La simulacion HTTP comprueba el contrato del cliente, no la configuracion de un realm real. No valida credenciales reales, TLS, descubrimiento OIDC ni firmas JWT. Un token con access_token vacio tampoco se rechaza expresamente en el servicio actual.
- AuthenticationService confia en su delegating handler para rechazar respuestas HTTP de error. La extraccion de Location no valida todos los formatos malformados; los tests no afirman que lo haga.
- No se usa EF InMemory o SQLite como sustituto de PostgreSQL: arrays, xmin, conversiones, SQL y concurrencia deben comprobarse con el proveedor real.
