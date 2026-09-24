# Pruebas de Bookify.Infrastructure

## Objetivo

El proyecto comprueba los adaptadores de Infrastructure: autenticacion HTTP, claims, politicas de permisos, configuracion de EF Core, repositorios, migraciones, preparacion/procesamiento de Outbox, configuracion Quartz y registro de dependencias.

Usa MSTest 4 con el runner VSTest (`Microsoft.NET.Test.Sdk` y `MSTest.TestAdapter`), FluentAssertions y Moq, con las versiones que ya usa Application.Tests. Referencia Infrastructure directamente, sin cargar la API. `InternalsVisibleTo` permite comprobar las clases internas sin hacerlas publicas.

## Estado actual

El 23/09/2026 la suite completa obtuvo 108 casos correctos, 0 fallidos y 26 omitidos por falta de BOOKIFY_TEST_POSTGRES. Incluye 20 casos unitarios Outbox y cuatro PostgreSQL Outbox, estos ultimos dentro de los omitidos. No se ha medido una nueva cobertura porcentual.

En la adaptacion anterior se corrigieron cinco tests que esperaban publicacion desde SaveChanges y dos que no aportaban IHostApplicationLifetime, necesario para validar el servicio alojado de Quartz.

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
| `Outbox/ProcessOutboxMessagesJobTests.cs` | Ocho casos sin servidor: lote vacio, publicacion con token, actualizacion posterior a publicar, transaccion compartida, error de publicacion/cancelacion, JSON invalido y fallos de lectura, actualizacion o commit. Comprueba liberacion de recursos. |
| `Outbox/OutboxMessageTests.cs` | Dos casos unitarios: conservacion de id, fecha UTC, tipo y contenido; estado inicial pendiente con ProcessedOnUtc y Error nulos. |
| `Outbox/OutboxMessageResponseTests.cs` | Cuatro casos unitarios: proyeccion de id/contenido y contrato de igualdad del record, incluyendo diferencias en cualquiera de sus campos. |
| `Outbox/OutboxOptionsTests.cs` | Tres casos unitarios: valores por defecto y conservacion del intervalo y tamano del lote configurados. No presupone validacion de opciones que la clase no implementa. |
| `Outbox/ProcessOutboxMessagesJobSetupTests.cs` | Tres intervalos: registra un unico job del tipo e identidad esperados, con ejecucion concurrente deshabilitada, y un trigger asociado con repeticion indefinida y sin fecha final. Inspecciona QuartzOptions sin arrancar el scheduler ni esperar tiempos reales. |
| `Outbox/PostgresOutboxTests.cs` | Cuatro casos con PostgreSQL: persistencia conjunta de usuario/evento pendiente, rollback de ambos, procesamiento ordenado por lotes sin volver a publicar mensajes procesados y almacenamiento del error sin reintento automatico. |
| `Support/OutboxJobTestContext.cs` | Ejecuta el job y Dapper reales sobre dobles ADO.NET. Proporciona filas mediante DataTableReader y captura parametros, transacciones y orden de operaciones. Permite simular errores de base de datos sin abrir conexiones. |
| `Persistence/ModelConfigurationTests.cs` | Las ocho configuraciones: tablas, claves, longitudes y conversiones de objetos de valor, owned types, relaciones, indices unicos, token xmin, tabla intermedia de permisos, datos iniciales y nulabilidad de ProcessedOnUtc/Error en Outbox. Evita la regresion de una FK RoleId dentro de permissions. |
| `Persistence/MigrationTests.cs` | Generacion SQL de avance y retroceso para las seis migraciones, incluida Add_OutBoxMessages. Comprueba las tablas de permisos/outbox y ausencia de diferencias pendientes con el Snapshot. No ejecuta ese SQL. |
| `Persistence/RepositoryTrackingTests.cs` | Add de los cuatro repositorios y la base Repository: estado Added de entidades, rol existente Unchanged y ausencia de guardado o limpieza de eventos al agregar. |
| `Persistence/PostgresRepositoryTests.cs` | Persistencia y lectura de los cuatro agregados, identificadores inexistentes, materializacion real, estados y limites inclusivos de solapamiento, xmin con dos contextos, SqlConnectionFactory, Dapper/DateOnly y migraciones completas con rollback y seeds. |
| `DependencyInjectionTests.cs` | AddInfrastructure, opciones de Keycloak/JWT, ambos overloads de JwtBearerOptionsSetup, resolucion de servicios, contexto y unidad de trabajo compartidos por scope, singleton SQL y fallo si falta la conexion. |
| `Data/AdapterTests.cs` | DateOnlyTypeHandler al leer y escribir, entrada de tipo incorrecto, DateTimeProvider y servicio de correo actual, que no realiza envio externo. |
| `Support/InfrastructureTestData.cs` | Factorias de entidades y opciones ficticias, fechas fijas y contextos Npgsql. Sustituye la referencia al rol estatico por una instancia local para evitar que el relationship fixup de EF contamine otras pruebas. |
| `Support/RecordingHttpHandler.cs` | Transporte HTTP en memoria que captura metodo, URI, contenido y cabeceras. Permite respuestas programadas y excepciones sin red. |
| `Support/PostgresTestBase.cs` | Creacion y limpieza de bases aisladas por caso, migraciones y datos iniciales de reservas usando operaciones de dominio. |

Los archivos generados Designer y Snapshot se ejercitan al descubrir migraciones, construir modelos y generar scripts; no se editan ni se duplican como tests de cada linea generada.

## Pruebas Outbox

```powershell
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ProcessOutboxMessagesJobTests"
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "FullyQualifiedName~PostgresOutboxTests"
```

El segundo comando requiere `BOOKIFY_TEST_POSTGRES` con la configuracion indicada arriba. La ultima ejecucion completa obtuvo 108 casos correctos, 0 fallidos y 26 omitidos por ausencia de esa variable. Los veinte casos unitarios de la carpeta Outbox se ejecutaron correctamente; los cuatro de PostgreSQL compilaron y se descubrieron, pero no se ejecutaron contra un servidor.

Para ejecutar los veinte casos unitarios de todas las clases Outbox, sin servicios externos:

```powershell
dotnet test Tests/Bookify.Infrastructure.Tests/Bookify.Infrastructure.Tests.csproj --filter "TestCategory=Infrastructure&FullyQualifiedName~Bookify.Infrastructure.Tests.Outbox"
```

Los tests sin servidor verifican las llamadas, los parametros de actualizacion y el texto de seleccion (`processed_on_utc IS NULL`, orden, limite y `FOR UPDATE`). No interpretan SQL ni simulan el motor transaccional: no demuestran que PostgreSQL aplique el limite, bloquee filas o revierta escrituras. Los casos PostgreSQL comprueban el estado persistido; para el rollback utilizan una transaccion explicita y otro contexto que verifica que no queda ni usuario ni mensaje.

Los tests reflejan el contrato actual, incluidos sus limites: una excepcion al publicar, incluso OperationCanceledException, se captura y el mensaje se marca procesado con Error. Un JSON no deserializable recibe el mismo tratamiento. Un fallo de lectura, actualizacion o commit se propaga. Publicar antes de confirmar la transaccion permite que una ejecucion posterior repita efectos ya realizados si la confirmacion falla; estas pruebas no garantizan entrega exactamente una vez ni cubren varios procesadores concurrentes. Los consumidores necesitan considerar la idempotencia.


## Problemas detectados y limites


- PermissionAuthorizationHandler ya usa Contains(requirement.Permission), no Count > 0. Tener un permiso diferente no satisface el requisito.
- GetPermissionsForUserAsync ya aplana todos los roles y permisos, aplica Distinct, materializa con ToListAsync y construye HashSet. La regresion con varios roles comprueba esa union sin depender del orden.
- GetRolesForUserAsync conserva FirstAsync sobre el usuario y lanza si no existe; devuelve todos los roles de ese usuario, no solo uno. La consulta de permisos, en cambio, devuelve un conjunto vacio sin usuario/roles/permisos. La transformacion de claims puede fallar antes de llegar al handler de permisos.
- SaveChanges convierte los eventos en Outbox antes de llamar a EF y vacia la lista de dominio. Si el guardado falla, los mensajes siguen Added en el contexto, pero no se demuestra que esten persistidos. ApplicationDbContextTests usa un interceptor que suprime la escritura; no prueba atomicidad ni entrega. PostgresOutboxTests comprueba persistencia y rollback solo cuando se habilita PostgreSQL. IPublisher ya no se invoca desde SaveChanges.
- ProcessedOnUtc y Error son ahora anulables y los mensajes nuevos empiezan con ambos valores null. La migracion regenerada 20260923111807_Add_OutBoxMessages admite NULL en ambas columnas. Los tests comprueban esos valores iniciales y la nulabilidad del modelo EF. El job dispone de pruebas propias, pero no se inicia el scheduler Quartz y las pruebas contra PostgreSQL requieren el servidor de pruebas descrito arriba.
- La simulacion HTTP comprueba el contrato del cliente, no la configuracion de un realm real. No valida credenciales reales, TLS, descubrimiento OIDC ni firmas JWT. Un token con access_token vacio tampoco se rechaza expresamente en el servicio actual.
- AuthenticationService confia en su delegating handler para rechazar respuestas HTTP de error. La extraccion de Location no valida todos los formatos malformados; los tests no afirman que lo haga.
- No se usa EF InMemory o SQLite como sustituto de PostgreSQL: arrays, xmin, conversiones, SQL y concurrencia deben comprobarse con el proveedor real.
