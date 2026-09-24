# Pruebas de Bookify.Application

El proyecto utiliza MSTest 4 con `Microsoft.NET.Test.Sdk` y el adaptador VSTest, FluentAssertions y Moq. Conserva el runner existente: los comandos siguientes no requieren Microsoft.Testing.Platform ni servicios externos. Las pruebas usan `// Arrange`, `// Act` y `// Assert`.

## Ejecutar

Desde la raiz del repositorio:

```powershell
dotnet test Tests/Bookify.Application.Tests/Bookify.Application.Tests.csproj
dotnet test Tests/Bookify.Application.Tests/Bookify.Application.Tests.csproj --collect "Code Coverage;Format=Cobertura"
```

El segundo comando genera un informe en `TestResults/<ejecucion>/*.cobertura.xml`. El paquete que debe consultarse para medir la capa es `Bookify.Application`, no `Bookify.Application.Tests` ni el conjunto de ensamblados cargados.


## Cobertura por funcionalidad

| Pruebas | Clases y comportamiento ejercitado |
| --- | --- |
| `Apartments/CreateApartment/CreateApartmentTests` | Comando, handler y validador de alta: campos persistidos, identificador, guardado unico, error de dominio si se invoca sin pipeline, aislamiento de la lista de amenities y fallos de guardado. |
| `Apartments/SearchApartmentsTests` | Query, handler, `ApartmentResponse` y `AddressResponse`: fechas invertidas sin abrir conexion, fechas iguales, resultados vacios y multiples, mapeo de direccion, parametros de fechas y estados, excepciones y cierre de conexion. |
| `Reviews/CreateReview/CreateReviewTests` | Comando, handler y validador de review: reserva completada, estados no elegibles, reserva inexistente, rating invalido sin pipeline, campos del recurso, evento acumulado y errores de persistencia. |
| `Bookings/ReserveBookingTests` | Comando y handler de reserva: usuario y apartamento inexistentes, solapamiento, precio, fecha del reloj, evento acumulado, guardado y traduccion exclusiva de `ConcurrencyException` a `BookingErrors.Overlap`. |
| `Bookings/BookingReservedDomainEventHandlerTests` | Notificacion de reserva: correo al destinatario correcto, salidas sin reserva o usuario, token en consultas y propagacion de fallo del correo. |
| `Bookings/GetBookingTests` | Query, handler y `BookingResponse`: acceso del propietario, rechazo de otro usuario, recurso inexistente, todos los campos del DTO, parametro id, contexto de usuario ausente y errores de datos. |
| `Users/UserCommandTests` | Alta y login: registro externo antes de guardar, identidad persistida, roles iniciales, credenciales, token de acceso, traduccion de fallo de JWT, cancelacion y validacion antes de tocar dependencias. |
| `Users/GetLoggedInUserTests` | Query, handler y `UserResponse`: busqueda por identificador externo, perfil completo, cero o multiples filas y cierre de conexion. |
| `Validation/CommandValidatorTests` | Los cinco validadores: valores nulos o vacios, formato de email, limites de nombres, password, comentario y rating, fechas, identificadores, monedas e importes. Los casos de amenities se completan en `CreateApartmentTests`. |
| `Behaviors/PipelineBehaviorTests` | `ValidationBehavior`, `LoggingBehavior`, `ValidationError`, `ValidationException` y `ConcurrencyException`: flujo de `next`, token, agregacion de errores, logs, errores de negocio y excepciones. |
| `DependencyInjectionTests` | `AddApplication`: resolucion de todos los handlers y validadores descubiertos, `ISender`, `IPublisher`, `PricingServices`, orden del pipeline de comandos y ausencia de esos behaviors en queries. |

El handler del alta de usuario conserva el nombre actual `CreateApartmentCommandHandler` en el namespace `Users.CreateUser`; lo prueba `UserCommandTests` mediante MediatR. No debe confundirse con el handler homonimo de apartamentos.

Los records de comandos y queries se ejercitan al enviarlos; los DTO se comprueban al mapear o devolver resultados. Las interfaces de repositorios, autenticacion, datos, reloj y correo se verifican mediante sus interacciones. Las interfaces de mensajeria no tienen implementacion ejecutable propia: se ejercitan a traves de MediatR y el registro de dependencias.

## Contextos y aislamiento

`Support/ApplicationTestContext` hereda del `ContextTestsBase` existente. Registra mocks estrictos para dependencias externas y fija el reloj. Los tests de apartamentos usan `ApartmentTestContext`; los de reviews usan `ApplicationTestContext` para acceder tambien al reloj en las pruebas directas del handler. Las clases centralizan su contexto en un campo de instancia y lo liberan en `[TestCleanup]`. MSTest crea una instancia independiente para cada caso, incluidos los datos parametrizados; no se comparten mocks estaticos entre pruebas ni se usan la base de datos de desarrollo o Keycloak.

La mayoria de casos envia comandos y queries por `ISender` para recorrer el registro y pipeline reales. Las pruebas directas de handlers cubren defensas de dominio que la validacion de entrada impediria alcanzar. `InternalsVisibleTo` en el proyecto Application permite ese acceso sin hacer publicos los handlers.

`Support/SqlQueryStub` entrega a Dapper una conexion y comando simulados con un `DataTableReader`. Dapper ejecuta realmente el enlace de parametros y el mapeo de columnas. Las colecciones de parametros usan tipos Npgsql ya disponibles por las referencias existentes; no se abre ninguna conexion. Un adaptador de fecha del test cumple el contrato que el host debe registrar para `DateOnly`.

La conexion simulada no es el proveedor PostgreSQL real. En particular, Dapper expande los arrays en parametros individuales sobre este doble; la prueba de estados comprueba sus valores, no la ejecucion de `ANY` contra PostgreSQL.

## Regresion encontrada y corregida

La prueba con dos validadores encontro que `ValidationBehavior` reutilizaba un `ValidationContext` mutable. El segundo validador heredaba los fallos del primero y la agregacion los duplicaba. El behavior ahora crea un contexto por validador; la prueba exige exactamente un error por fallo y que `next` no se invoque.

## Limites de estas pruebas

El cambio a Outbox no convierte estas pruebas en tests del procesador: IUnitOfWork sigue simulado, y comprobar el evento acumulado o invocar directamente BookingReservedDomainEventHandler no verifica su persistencia ni entrega diferida. Esa cobertura esta en [Infrastructure.Tests](../Bookify.Infrastructure.Tests/readme.md#pruebas-outbox).

- No se ejecutan sentencias SQL contra PostgreSQL. Hay que complementar estas pruebas con integracion para esquema, alias, sintaxis, filtros de disponibilidad y concurrencia real.
- Se caracteriza el comportamiento actual: `GetLoggedInUserQueryHandler` lanza si `QuerySingleAsync` recibe cero o multiples filas; `LoggingBehavior` registra finalizacion exitosa incluso para un `Result` fallido sin excepcion.
- Los tests del recurso ejercitan la comparacion de propietario en Application. No ejecutan validacion JWT, transformacion de claims ni atributos HTTP de autorizacion.
- Las pruebas de concurrencia simulan `ConcurrencyException`; no producen carreras entre transacciones ni verifican el token de EF Core.
