# Pruebas de Bookify.Infrastructure

## Objetivo

El proyecto comprueba los adaptadores de Infrastructure: autenticacion HTTP, claims, politicas de permisos, configuracion de EF Core, repositorios, migraciones, publicacion de eventos y registro de dependencias.

Usa MSTest 4 con el runner VSTest (`Microsoft.NET.Test.Sdk` y `MSTest.TestAdapter`), FluentAssertions y Moq, con las versiones que ya usa Application.Tests. Referencia Infrastructure directamente, sin cargar la API. `InternalsVisibleTo` permite comprobar las clases internas sin hacerlas publicas. Es el unico ajuste del proyecto de produccion; no se cambia su comportamiento.

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
| `Authorization/PostgresAuthorizationTests.cs` | AuthorizationService y UserRolesResponse con datos reales, ausencia de usuario, transformacion de claims e idempotencia, permiso requerido, usuario sin permisos y union de permisos de varios roles. Incluye regresiones de seguridad pendientes indicadas abajo. |
| `Persistence/ApplicationDbContextTests.cs` | SaveChangesAsync: publica despues del guardado, vacia eventos una sola vez, devuelve el resultado, reenvia el token a EF, traduce DbUpdateConcurrencyException y propaga otros fallos. Caracteriza la perdida de eventos pendientes si falla el publicador. |
| `Persistence/ModelConfigurationTests.cs` | Las siete configuraciones: tablas, claves, longitudes y conversiones de objetos de valor, owned types, relaciones, indices unicos, token xmin, tabla intermedia de permisos y datos iniciales. Evita la regresion de una FK RoleId dentro de permissions. |
| `Persistence/MigrationTests.cs` | Generacion SQL de avance y retroceso para las cinco migraciones. Verifica tambien que el snapshot no tenga diferencias pendientes con el modelo actual. No ejecuta ese SQL. |
| `Persistence/RepositoryTrackingTests.cs` | Add de los cuatro repositorios y la base Repository: estado Added de entidades, rol existente Unchanged y ausencia de guardado o limpieza de eventos al agregar. |
| `Persistence/PostgresRepositoryTests.cs` | Persistencia y lectura de los cuatro agregados, identificadores inexistentes, materializacion real, estados y limites inclusivos de solapamiento, xmin con dos contextos, SqlConnectionFactory, Dapper/DateOnly y migraciones completas con rollback y seeds. |
| `DependencyInjectionTests.cs` | AddInfrastructure, opciones de Keycloak/JWT, ambos overloads de JwtBearerOptionsSetup, resolucion de servicios, contexto y unidad de trabajo compartidos por scope, singleton SQL y fallo si falta la conexion. |
| `Data/AdapterTests.cs` | DateOnlyTypeHandler al leer y escribir, entrada de tipo incorrecto, DateTimeProvider y servicio de correo actual, que no realiza envio externo. |
| `Support/InfrastructureTestData.cs` | Factorias de entidades y opciones ficticias, fechas fijas y contextos Npgsql. Sustituye la referencia al rol estatico por una instancia local para evitar que el relationship fixup de EF contamine otras pruebas. |
| `Support/RecordingHttpHandler.cs` | Transporte HTTP en memoria que captura metodo, URI, contenido y cabeceras. Permite respuestas programadas y excepciones sin red. |
| `Support/PostgresTestBase.cs` | Creacion y limpieza de bases aisladas por caso, migraciones y datos iniciales de reservas usando operaciones de dominio. |

Los archivos generados Designer y Snapshot se ejercitan al descubrir migraciones, construir modelos y generar scripts; no se editan ni se duplican como tests de cada linea generada.


## Problemas detectados y limites


- `AuthorizationService.GetPermissionsForUserAsync` toma la primera coleccion de permisos con FirstAsync, no la union de todos los roles. La regresion usa dos roles con permisos distintos para que el fallo no dependa del orden devuelto por PostgreSQL. No se ha cambiado la consulta en esta tarea.
- Los servicios de autorizacion actuales lanzan al no encontrar un usuario; las pruebas caracterizan esa excepcion, no una politica nueva de tratamiento del usuario ausente.
- Los eventos se limpian antes de publicarlos y despues de guardar. Si el publicador falla, los cambios ya estan persistidos y los eventos dejan de estar pendientes. Las pruebas lo hacen visible; no existe garantia de entrega ni outbox en esta implementacion.
- La simulacion HTTP comprueba el contrato del cliente, no la configuracion de un realm real. No valida credenciales reales, TLS, descubrimiento OIDC ni firmas JWT. Un token con access_token vacio tampoco se rechaza expresamente en el servicio actual.
- AuthenticationService confia en su delegating handler para rechazar respuestas HTTP de error. La extraccion de Location no valida todos los formatos malformados; los tests no afirman que lo haga.
- No se usa EF InMemory o SQLite como sustituto de PostgreSQL: arrays, xmin, conversiones, SQL y concurrencia deben comprobarse con el proveedor real.
