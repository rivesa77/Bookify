# Bookify.Application

Este proyecto contiene la capa de aplicacion de Bookify. Su responsabilidad es ejecutar los casos de uso del sistema coordinando entidades de dominio, repositorios, validadores, servicios externos y persistencia.

Application no configura EF Core, no construye conexiones Npgsql ni implementa el envio de correo. Define contratos que Infrastructure implementa. Sin embargo, sus queries contienen SQL, nombres de tablas y columnas, e incluso sintaxis de PostgreSQL como `ANY`. La construccion de la conexion esta abstraida, pero el esquema y el dialecto de las lecturas siguen siendo dependencias de esta capa.

Esta guia distingue el comportamiento implementado de las limitaciones pendientes. `Bookify.Api/Program.cs` ya registra Application e Infrastructure, y sus controladores invocan los casos de uso con `ISender`. En Development, la API aplica las migraciones al arrancar. Las lecturas conservan discrepancias SQL documentadas mas abajo.

[Guia de la solucion](../readme.md) | [Domain](../Bookify.Domain/readme.md) | [Infrastructure](../Bookify.Infrastructure/readme.md) | [Api](../Bookify.Api/readme.md)

## Indice

- [Objetivo y decisiones](#objetivo-de-la-capa-application)
- [Inventario de archivos](#inventario-de-archivos)
- [Registro de servicios](#dependencyinjection)
- [Mensajes y handlers](#abstractionsmessaging)
- [Pipeline de logging y validacion](#abstractionsbehaviors)
- [Errores y excepciones](#exceptions)
- [Reserva de apartamentos](#bookingsreservebooking)
- [Consulta de una reserva](#bookingsgetbooking)
- [Busqueda de disponibilidad](#apartmentssearchapartments)
- [Uso desde el host](#como-usar-application-desde-el-host)
- [Como ampliar Application](#como-ampliar-application)
- [Limitaciones actuales](#puntos-a-revisar)

## Objetivo de la capa Application

Application responde preguntas como:

- Que pasos hay que seguir para reservar una booking.
- Como se valida un comando antes de llegar al dominio.
- Que query se ejecuta para buscar apartamentos disponibles.
- Que DTO devuelve una lectura.
- Que servicios externos necesita un caso de uso.
- Que se hace cuando ocurre un evento de dominio.

La idea es que Domain tenga las reglas puras del negocio, mientras Application orquesta esas reglas dentro de un caso de uso.

## Por que se ha realizado asi

Esta capa usa un estilo CQRS con MediatR:

- Los comandos representan acciones que cambian estado.
- Las queries representan consultas de solo lectura.
- Cada comando o query tiene su handler.
- Los handlers devuelven `Result` o `Result<T>`.
- La validacion y el logging se aplican como pipeline behaviors.

Esto evita que la logica quede mezclada en controllers, endpoints o clases grandes de servicios. Cada caso de uso queda aislado en su propia carpeta.

CQRS aqui separa codigo de escritura y lectura dentro del mismo proceso y sobre la misma base de datos. No implica un bus externo, una cola, dos bases de datos ni ejecucion en segundo plano. El comando usa entidades y repositorios; la query devuelve DTOs sin cargar entidades con seguimiento de EF Core.

## Dependencias principales

El proyecto referencia:

- `Bookify.Domain`: para usar entidades, errores, repositorios y objetos de valor.
- `MediatR`: para enviar comandos, queries y manejar eventos.
- `FluentValidation.DependencyInjectionExtensions`: para registrar validadores.
- `Dapper`: para queries SQL de lectura.
- `Microsoft.Extensions.Logging.Abstractions`: para logging sin acoplarse a una implementacion concreta.

## Estructura

```text
Bookify.Application
|-- Abstractions
|   |-- Behaviors
|   |-- Data
|   |-- DateTimeProvider
|   |-- Email
|   |-- Messaging
|-- Apartments
|   |-- SearchApartments
|-- Bookings
|   |-- GetBooking
|   |-- ReserveBooking
|-- Exceptions
|-- DependencyInjection.cs
|-- Bookify.Application.csproj
```

## Bookify.Application.csproj

Define la capa como biblioteca .NET:

```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

Tambien declara los paquetes usados por la capa:

- `Dapper`
- `FluentValidation.DependencyInjectionExtensions`
- `MediatR`
- `Microsoft.Extensions.Logging.Abstractions`

Y referencia `Bookify.Domain`.

Versiones declaradas en el archivo de proyecto: Dapper `2.1.79`, FluentValidation.DependencyInjectionExtensions `12.1.1`, MediatR `14.2.0` y Microsoft.Extensions.Logging.Abstractions `10.0.11`. Son las versiones del repositorio, no una recomendacion de actualizacion.

## Inventario de archivos

Esta tabla cubre todos los archivos de codigo y configuracion de la capa. Los apartados siguientes explican su funcionamiento; `bin/` y `obj/` contienen salidas generadas.

| Archivo | Responsabilidad |
| --- | --- |
| [Bookify.Application.csproj](Bookify.Application.csproj) | Framework, paquetes y referencia a Domain. |
| [DependencyInjection.cs](DependencyInjection.cs) | Registro publico `AddApplication`. |
| [Abstractions/Messaging/IBaseCommand.cs](Abstractions/Messaging/IBaseCommand.cs) | Marca comun para identificar comandos. |
| [Abstractions/Messaging/ICommand.cs](Abstractions/Messaging/ICommand.cs) | Contiene `ICommand` e `ICommand<TResponse>`. |
| [Abstractions/Messaging/ICommandHandler.cs](Abstractions/Messaging/ICommandHandler.cs) | Contiene las dos variantes de handler de comandos. |
| [Abstractions/Messaging/IQuery.cs](Abstractions/Messaging/IQuery.cs) | Contrato generico de consulta. |
| [Abstractions/Messaging/IQueryHandler.cs](Abstractions/Messaging/IQueryHandler.cs) | Contrato del handler de una consulta. |
| [Abstractions/Behaviors/LoggingBehavior.cs](Abstractions/Behaviors/LoggingBehavior.cs) | Logs alrededor de la ejecucion del comando. |
| [Abstractions/Behaviors/ValidationBehavior.cs](Abstractions/Behaviors/ValidationBehavior.cs) | Validacion previa al handler. |
| [Abstractions/Data/ISqlConnectionFactory.cs](Abstractions/Data/ISqlConnectionFactory.cs) | Contrato de conexiones para Dapper. |
| [Abstractions/DateTimeProvider/IDateTimeProvider.cs](Abstractions/DateTimeProvider/IDateTimeProvider.cs) | Contrato del reloj UTC. |
| [Abstractions/Email/IEmailService.cs](Abstractions/Email/IEmailService.cs) | Contrato para solicitar correos. |
| [Exceptions/ValidationError.cs](Exceptions/ValidationError.cs) | Propiedad y mensaje de un fallo de validacion. |
| [Exceptions/ValidationException.cs](Exceptions/ValidationException.cs) | Agrupa fallos de entrada del comando. |
| [Exceptions/ConcurrencyException.cs](Exceptions/ConcurrencyException.cs) | Expresa conflictos de escritura sin depender de EF Core. |
| [Bookings/ReserveBooking/ReserveBookingCommand.cs](Bookings/ReserveBooking/ReserveBookingCommand.cs) | Datos de la solicitud de reserva. |
| [Bookings/ReserveBooking/ReserveBookingCommandValidator.cs](Bookings/ReserveBooking/ReserveBookingCommandValidator.cs) | Reglas sobre identificadores y fechas. |
| [Bookings/ReserveBooking/ReserveBookingCommandHandler.cs](Bookings/ReserveBooking/ReserveBookingCommandHandler.cs) | Orquesta la reserva y el guardado. |
| [Bookings/ReserveBooking/BookingReservedDomainEventHandler.cs](Bookings/ReserveBooking/BookingReservedDomainEventHandler.cs) | Solicita correo tras publicarse el evento reservado. |
| [Bookings/GetBooking/GetBookingQuery.cs](Bookings/GetBooking/GetBookingQuery.cs) | Mensaje de consulta por identificador. |
| [Bookings/GetBooking/GetBookingQueryHandler.cs](Bookings/GetBooking/GetBookingQueryHandler.cs) | SQL de lectura del detalle. |
| [Bookings/GetBooking/BookingResponse.cs](Bookings/GetBooking/BookingResponse.cs) | DTO de detalle de reserva. |
| [Apartments/SearchApartments/SearchApartmentsQuery.cs](Apartments/SearchApartments/SearchApartmentsQuery.cs) | Mensaje de consulta de disponibilidad. |
| [Apartments/SearchApartments/SearchApartmentsQueryHandler.cs](Apartments/SearchApartments/SearchApartmentsQueryHandler.cs) | SQL que descarta apartamentos ocupados. |
| [Apartments/SearchApartments/ApartmentResponse.cs](Apartments/SearchApartments/ApartmentResponse.cs) | DTO de apartamento disponible. |
| [Apartments/SearchApartments/AddressResponse.cs](Apartments/SearchApartments/AddressResponse.cs) | Direccion anidada en el DTO de apartamento. |

## DependencyInjection

`DependencyInjection.cs` contiene el metodo de extension:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
```

Su objetivo es registrar todo lo necesario para que Application funcione.

Actualmente registra:

- MediatR y todos los handlers del assembly.
- `LoggingBehavior<,>` como behavior abierto.
- `ValidationBehavior<,>` como behavior abierto.
- Validadores de FluentValidation del assembly.
- `PricingServices` como servicio transient.

Uso esperado desde el host:

```csharp
builder.Services.AddApplication();
```

El registro de MediatR escanea el assembly donde esta `DependencyInjection`, por lo que encuentra handlers como:

- `ReserveBookingCommandHandler`
- `GetBookingQueryHandler`
- `SearchApartmentsQueryHandler`
- `BookingReservedDomainEventHandler`

Los handlers de casos de uso son `internal sealed`: el consumidor utiliza los mensajes publicos y `ISender`, y el escaneo localiza las implementaciones dentro del assembly. No es necesario construir los handlers manualmente.

El orden de los behaviors importa: logging envuelve validacion, y validacion envuelve el handler. Un fallo de validacion tambien atraviesa el bloque de captura de logging. `PricingServices` es transient y no conserva estado entre operaciones.

`AddApplication()` no registra repositorios, reloj, email ni conexiones concretas. Para ejecutar estos casos de uso con las implementaciones existentes se necesita tambien `AddInfrastructure(configuration)`, un scope de servicios y logging proporcionado por el host.

## Abstractions/Messaging

Esta carpeta define las convenciones de mensajes de la aplicacion.

### IBaseCommand

`IBaseCommand` es una marker interface. No contiene metodos.

Su funcion es marcar un request como comando. Esto permite aplicar behaviors solo a comandos.

Ejemplo:

```csharp
public interface IBaseCommand
{
}
```

Actualmente `LoggingBehavior` y `ValidationBehavior` usan esta restriccion:

```csharp
where TRequest : IBaseCommand
```

Eso significa que esos behaviors se aplican a comandos, no a queries.

### ICommand

`ICommand` representa un comando que no devuelve un dato de negocio, solo un `Result`.

```csharp
public interface ICommand : IRequest<Result>, IBaseCommand
{
}
```

Se usaria para acciones como cancelar una reserva, si el caso de uso no necesita devolver ningun valor.

### ICommand<TResponse>

`ICommand<TResponse>` representa un comando que devuelve un valor cuando termina correctamente.

```csharp
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
{
}
```

Ejemplo real:

```csharp
public record ReserveBookingCommand(...) : ICommand<Guid>;
```

La reserva cambia estado del sistema y devuelve el `Guid` de la nueva booking.

### ICommandHandler<TCommand>

`ICommandHandler<TCommand>` representa el handler de un comando sin respuesta de negocio.

Internamente hereda de MediatR:

```csharp
IRequestHandler<TCommand, Result>
```

### ICommandHandler<TCommand, TResponse>

`ICommandHandler<TCommand, TResponse>` representa el handler de un comando con respuesta.

Internamente hereda de:

```csharp
IRequestHandler<TCommand, Result<TResponse>>
```

Ejemplo real:

```csharp
internal sealed class ReserveBookingCommandHandler
    : ICommandHandler<ReserveBookingCommand, Guid>
```

### IQuery<TResponse>

`IQuery<TResponse>` representa una consulta de solo lectura.

```csharp
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
```

A diferencia de los comandos, las queries no implementan `IBaseCommand`, por lo que no pasan por los behaviors que estan restringidos a comandos.

### IQueryHandler<TQuery, TResponse>

`IQueryHandler<TQuery, TResponse>` representa el handler de una query.

Internamente hereda de:

```csharp
IRequestHandler<TQuery, Result<TResponse>>
```

Ejemplo real:

```csharp
internal sealed class GetBookingQueryHandler
    : IQueryHandler<GetBookingQuery, BookingResponse>
```

En `ICommand<Guid>`, `Guid` es el dato de negocio, mientras que la respuesta completa es `Result<Guid>`. No se debe declarar `ICommand<Result<Guid>>`, porque eso anidaria resultados. Lo mismo se aplica a `IQuery<TResponse>`.

Las restricciones de los handlers relacionan el tipo de mensaje con su respuesta: `TCommand` debe implementar el `ICommand` correspondiente y `TQuery` el `IQuery` correspondiente. Estas interfaces expresan convenciones; no impiden tecnicamente escribir datos desde una query.

### Send y Publish

| Operacion | Uso actual | Resultado |
| --- | --- | --- |
| `ISender.Send(request, token)` | Enviar uno de los tres comandos/queries a su handler. | Espera `Result` o `Result<T>`. |
| `IPublisher.Publish(domainEvent)` | Distribuir una notificacion de dominio a sus handlers registrados. | Espera su finalizacion, sin devolver un resultado de negocio. |

Los eventos implementan `IDomainEvent`, que hereda de `INotification`. Solo `BookingReservedDomainEvent` tiene un handler en Application actualmente. Los behaviors de requests no envuelven los handlers de notificaciones. MediatR trabaja dentro del proceso; `async` no significa publicacion duradera ni trabajo independiente de la solicitud.

## Abstractions/Behaviors

Los behaviors de MediatR permiten ejecutar logica transversal antes y despues de un handler.

En este proyecto se usan para no repetir logging y validacion dentro de cada handler.

### LoggingBehavior

`LoggingBehavior<TRequest, TResponse>` registra informacion cuando se ejecuta un comando.

Flujo:

1. Obtiene el nombre del request.
2. Loguea que el comando se esta ejecutando.
3. Ejecuta el siguiente paso del pipeline con `next`.
4. Loguea que el comando se proceso correctamente.
5. Si ocurre una excepcion, loguea el fallo y vuelve a lanzar la excepcion.

Como tiene:

```csharp
where TRequest : IBaseCommand
```

solo aplica a comandos. Las queries no pasan por este behavior.

El mensaje de exito significa que no se lanzo una excepcion: no se inspecciona `Result.IsFailure`. Por eso una reserva que devuelve `BookingErrors.Overlap` tambien puede aparecer como procesada correctamente. En el `catch` se usa `LogInformation` sin pasar el objeto excepcion, por lo que ese mensaje no incluye su traza ni sus detalles.

Uso automatico:

```csharp
configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
```

### ValidationBehavior

`ValidationBehavior<TRequest, TResponse>` ejecuta validadores de FluentValidation antes del handler.

Flujo:

1. Recibe todos los `IValidator<TRequest>` registrados.
2. Si no hay validadores, llama a `next`.
3. Si hay validadores, crea un `ValidationContext<TRequest>`.
4. Ejecuta los validadores.
5. Convierte los errores de FluentValidation a `ValidationError`.
6. Si hay errores, lanza `Bookify.Application.Exceptions.ValidationException`.
7. Si no hay errores, permite continuar al handler.

Tambien esta limitado a comandos mediante:

```csharp
where TRequest : IBaseCommand
```

Eso explica por que `ReserveBookingCommandValidator` se ejecuta automaticamente antes de `ReserveBookingCommandHandler`.

Nota: actualmente llama a `validator.Validate(context)`, que es validacion sincronica. Si en el futuro se usan reglas asincronas, habria que cambiarlo a `ValidateAsync`.

Llamar directamente a `Handle` omite estos behaviors. Un test o servicio que haga esa llamada no ejecuta automaticamente FluentValidation. La validacion fallida lanza una excepcion; no se convierte en `Result.Failure` por el simple hecho de usar `Result` como respuesta del comando.

## Abstractions/Data

### ISqlConnectionFactory

`ISqlConnectionFactory` define como obtener una conexion SQL para queries de lectura:

```csharp
IDbConnection CreateConnection();
```

Application no construye el proveedor concreto. La implementacion actual devuelve una conexion PostgreSQL abierta; el handler que la solicita es responsable de liberarla con `using`. El contrato no elimina la dependencia del SQL escrito en Application respecto al esquema y al proveedor.

La implementacion concreta esta en Infrastructure con `SqlConnectionFactory`.

Se usa especialmente en handlers que ejecutan SQL con Dapper:

- `GetBookingQueryHandler`
- `SearchApartmentsQueryHandler`

Cada llamada abre una conexion independiente. Las consultas actuales no comparten automaticamente la transaccion del `ApplicationDbContext`. Aunque los handlers reciben `CancellationToken`, no lo pasan a las llamadas Dapper actuales.

## Abstractions/DateTimeProvider

### IDateTimeProvider

`IDateTimeProvider` abstrae el reloj del sistema:

```csharp
DateTime UtcNow { get; }
```

Se usa para que Application no llame directamente a `DateTime.UtcNow`.

Ventajas:

- Facilita tests.
- Centraliza el uso de fecha/hora actual.
- Evita acoplar casos de uso al reloj del sistema.

Ejemplo real en `ReserveBookingCommandHandler`:

```csharp
dateTimeProvider.UtcNow
```

## Abstractions/Email

### IEmailService

`IEmailService` define el envio de emails:

```csharp
Task SendAsync(
    Email recipient,
    string subject,
    string body);
```

Application puede pedir que se envie un email sin saber si se usa SMTP, SendGrid, Mailgun u otro proveedor.

La implementacion concreta actual esta en Infrastructure con `EmailService`.

El contrato no recibe `CancellationToken`. `EmailService` devuelve `Task.CompletedTask`, por lo que la llamada actual termina sin enviar un correo real.

## Exceptions

Esta carpeta contiene excepciones propias de Application.

### ValidationError

`ValidationError` es un record simple con:

- `PropertyName`
- `ErrorMessage`

Representa un error individual de validacion.

Lo crea `ValidationBehavior` a partir de los errores producidos por FluentValidation.

### ValidationException

`ValidationException` agrupa una coleccion de `ValidationError`.

Se lanza cuando un comando no pasa las reglas de FluentValidation.

Uso interno:

```csharp
throw new ValidationException(validatorErrors);
```

En un host web, una capa de manejo de errores deberia traducir esta excepcion a una respuesta HTTP clara, por ejemplo `400 Bad Request`.

### ConcurrencyException

`ConcurrencyException` es una excepcion publica y sellada que representa un conflicto de escritura. Mantiene el mensaje y la excepcion tecnica original en `InnerException`:

```csharp
public ConcurrencyException(string message, Exception innerException)
    : base(message, innerException)
```

El flujo actual conecta las dos capas:

```text
EF Core detecta un conflicto al guardar
  -> DbUpdateConcurrencyException
  -> ApplicationDbContext captura y lanza ConcurrencyException
  -> ReserveBookingCommandHandler captura ConcurrencyException
  -> Result.Failure<Guid>(BookingErrors.Overlap)
```

Esta traduccion permite que Application maneje concurrencia sin referenciar EF Core. La clase no detecta conflictos por si misma: la deteccion depende de la configuracion del token y de la actualizacion del apartamento. El detalle esta en [Infrastructure: concurrencia](../Bookify.Infrastructure/readme.md#concurrencia).

Esta excepcion trata conflictos de escritura. Los errores de suma de monedas de `Money` son `InvalidOperationException` y no entran en este `catch`.

### Que recibe el consumidor

| Situacion | Representacion | Tratamiento |
| --- | --- | --- |
| Usuario o apartamento inexistente, solapamiento o conflicto convertido | `Result<Guid>` fallido. | Comprobar `IsFailure` y consultar `Error`. |
| Comando que no cumple FluentValidation | `ValidationException`. | Capturarla en el limite de entrada y consultar `Errors`. |
| Error SQL, conversion, conexion u otra excepcion no convertida | Excepcion propagada. | Aplicar la politica de errores del host. |

No hay middleware HTTP ni una traduccion automatica a respuestas API en esta capa.

## Bookings/ReserveBooking

Esta carpeta implementa el caso de uso de reservar una booking.

### ReserveBookingCommand

Representa la intencion de reservar un apartamento.

Contiene:

- `ApartmentId`
- `UserId`
- `StartDate`
- `EndDate`

Implementa:

```csharp
ICommand<Guid>
```

Eso significa que:

- Es un comando.
- Cambia estado del sistema.
- Si sale bien, devuelve el `Guid` de la reserva creada.
- La respuesta real del handler es `Result<Guid>`.

### ReserveBookingCommandValidator

Valida datos basicos del comando antes de ejecutar el caso de uso.

Reglas actuales:

- `UserId` no puede estar vacio.
- `ApartmentId` no puede estar vacio.
- `StartDate` debe ser menor que `EndDate`.

Ejemplo:

```csharp
RuleFor(c => c.UserId).NotEmpty();
RuleFor(c => c.ApartmentId).NotEmpty();
RuleFor(c => c.StartDate).LessThan(c => c.EndDate);
```

Este validador se registra automaticamente con:

```csharp
services.AddValidatorsFromAssembly(...)
```

Y se ejecuta automaticamente por `ValidationBehavior`.

`NotEmpty()` sobre estos identificadores rechaza `Guid.Empty`. El validador no consulta existencia, disponibilidad ni permisos, y tampoco exige fechas futuras. El orden estricto `StartDate < EndDate` es mas restrictivo que `DateRange.Create`, que admite igualdad.

### ReserveBookingCommandHandler

Orquesta la reserva.

Dependencias:

- `IUserRepository`
- `IApartmentRepository`
- `IBookingRepository`
- `IUnitOfWork`
- `PricingServices`
- `IDateTimeProvider`

Flujo:

1. Busca el usuario por `UserId`.
2. Si el usuario no existe, devuelve `UserErrors.NotFound`.
3. Busca el apartamento por `ApartmentId`.
4. Si el apartamento no existe, devuelve `ApartmentErrors.NotFound`.
5. Crea el `DateRange`.
6. Consulta si hay una reserva solapada con `IsOverlappingAsync`.
7. Si hay solapamiento, devuelve `BookingErrors.Overlap`.
8. Crea la reserva con `Booking.Reserve`.
9. Agrega la reserva con `bookingRepository.Add`.
10. Guarda con `unitOfWork.SaveChangesAsync`.
11. Devuelve el `booking.Id`.
12. Si el bloque de creacion y guardado lanza `ConcurrencyException`, devuelve `BookingErrors.Overlap`.

El metodo devuelve:

```csharp
Task<Result<Guid>>
```

Gracias a la conversion implicita de `Result<T>`, este retorno:

```csharp
return booking.Id;
```

se convierte en un `Result<Guid>` exitoso.

`Booking.Reserve` tambien modifica `Apartment.LastBookedOnUTC` y acumula un evento. Los repositorios EF cargan el apartamento con seguimiento y comparten el contexto de `IUnitOfWork`, de modo que el guardado puede persistir tanto la reserva como el cambio del apartamento. `Add` por si solo no guarda.

La consulta de solapamiento no es atomica con la insercion. El token optimista del apartamento permite detectar una escritura concurrente cuando se emite su actualizacion. El handler no toma bloqueos pesimistas ni reintenta automaticamente. Ademas, convierte cualquier `ConcurrencyException` del bloque en `Overlap`, aunque un conflicto sobre el mismo apartamento no demuestre necesariamente que las fechas se solapen.

Las busquedas y `DateRange.Create` estan fuera del `try`. El `catch` solo contempla `ConcurrencyException`; no absorbe todos los errores tecnicos. Tampoco se comprueba que quien envia el comando corresponda a `UserId`: no hay autorizacion implementada en estos archivos.

### BookingReservedDomainEventHandler

Maneja el evento `BookingReservedDomainEvent`.

Se ejecuta cuando una reserva fue creada y el evento fue publicado por Infrastructure.

Dependencias:

- `IBookingRepository`
- `IUserRepository`
- `IEmailService`

Flujo:

1. Recibe el evento con `BookingId`.
2. Busca la reserva.
3. Si no existe, termina sin hacer nada.
4. Busca el usuario de la reserva.
5. Si no existe, termina sin hacer nada.
6. Envia un email al usuario indicando que tiene 10 minutos para confirmar.

Este handler no se llama directamente. MediatR lo ejecuta cuando se publica `BookingReservedDomainEvent`.

La publicacion se espera dentro de `ApplicationDbContext.SaveChangesAsync`, despues de guardar los datos. Por eso el tiempo del handler del evento forma parte de la respuesta del comando. Si un servicio de correo real fallara, el guardado ya habria terminado en el flujo actual; no hay outbox ni recuperacion duradera de eventos.

El texto del correo dice que hay diez minutos para confirmar, pero no existe una tarea de caducidad ni una comprobacion de ese plazo en `Booking.Confirm`. Actualmente `EmailService` tampoco envia correos reales. Si faltan reserva o usuario, este handler simplemente termina sin registrar ni devolver un error.

## Bookings/GetBooking

Esta carpeta implementa la lectura de una reserva por id.

### GetBookingQuery

Representa una consulta de reserva.

Contiene:

- `BookingId`

Implementa:

```csharp
IQuery<BookingResponse>
```

Eso significa que es una query de solo lectura y devuelve `Result<BookingResponse>`.

### BookingResponse

DTO de salida para mostrar una booking.

Contiene datos planos:

- `Id`
- `UserId`
- `ApartmentId`
- `Status`
- importes y monedas de precio;
- `DurationStart`
- `DurationEnd`
- `CreatedOnUtc`

Este DTO no es una entidad de dominio. Es una forma optimizada de devolver informacion hacia afuera.

Tipos y contenido exactos:

| Propiedades | Tipo actual | Observacion |
| --- | --- | --- |
| `Id`, `UserId`, `ApartmentId` | `Guid` | Identificadores. |
| `Status` | `int` | Valor de `BookingStatus`: Reserved=1, Confirmed=2, Rejected=3, Cancelled=4, Completed=5. |
| `PriceAmount`, `CleaningFeeAmount`, `AmenitiesUpChargeAmount`, `TotalPriceAmount` | `decimal` | Importes del periodo, limpieza, recargo y total. |
| `PriceCurrency`, `CleaningFeeCurrency`, `AmenitiesUpChargeCurrency`, `TotalPriceCurrency` | `string` | Codigos de moneda, inicializados como `string.Empty`. |
| `DurationStart`, `DurationEnd` | `DateOnly` | Fechas de estancia, alineadas con los alias del SELECT. |
| `CreatedOnUtc` | `DateTime` | Instante de creacion. |

Las propiedades tienen `init`. Se han corregido tres diferencias del DTO anterior: ahora existe `PriceAmount`, todas las monedas son `string` y las fechas se llaman `DurationStart`/`DurationEnd`, como sus alias SQL. No se exponen las fechas posteriores de confirmacion, rechazo, finalizacion o cancelacion. El controlador devuelve este DTO como JSON al consultar una reserva con exito.

### GetBookingQueryHandler

Ejecuta la query con Dapper.

Dependencia:

- `ISqlConnectionFactory`

Flujo:

1. Crea una conexion SQL.
2. Prepara un `BookingResponse` con `Id = request.BookingId` como objeto de parametros.
3. Ejecuta SQL contra la tabla `bookings`.
4. Mapea el resultado a `BookingResponse`.
5. Devuelve la respuesta como `Result<BookingResponse>`.

Si Dapper devuelve `null`, la conversion implicita de `Result<T>` transforma ese valor nulo en un fallo con `Error.NullValue`.

Ese comportamiento presupone que SQL y mapeo consiguen ejecutarse. La implementacion actual presenta estas diferencias, verificadas contra los archivos del proyecto:

| Parte | Diferencia actual | Consecuencia |
| --- | --- | --- |
| Filtro | SQL usa `@BookingId`, pero el objeto de parametros solo aporta `Id`, no `BookingId`. | No se proporciona el parametro que solicita el filtro. |
| Tabla | SQL consulta `bookings` sin comillas; la migracion crea `Bookings` con mayuscula. | El identificador no coincide con la tabla entrecomillada que genera EF en PostgreSQL. |
| Recargo | SQL usa `amenities_up_charge_*`, pero la migracion crea `amenities_up_change_*` desde `AmenitiesUpChange`. | Las columnas solicitadas no existen con ese nombre en el esquema de la migracion inicial. |
| Cancelacion | El token de `Handle` no se pasa a Dapper. | La llamada SQL no recibe esa cancelacion. |

Por tanto, esta query expresa el contrato de lectura, pero necesita resolver esas incoherencias antes de considerarse validada contra una base de datos. No utiliza `BookingErrors.NotFound` cuando no obtiene una fila.

Las correcciones de `BookingResponse` no modifican los parametros ni los nombres de tabla y columnas del SQL. La referencia para comprobar el esquema es [la migracion inicial](../Bookify.Infrastructure/Migrations/20260909102321_Initial_Database.cs), no solo las convenciones de nombres configuradas en EF.

## Apartments/SearchApartments

Esta carpeta implementa la busqueda de apartamentos disponibles.

### SearchApartmentsQuery

Representa la consulta de disponibilidad.

Contiene:

- `StartDate`
- `EndDate`

Implementa:

```csharp
IQuery<IReadOnlyList<ApartmentResponse>>
```

Devuelve una lista de apartamentos disponibles.

### ApartmentResponse

DTO de salida de apartamento.

Contiene:

- `Id`
- `Name`
- `Description`
- `Price`
- `Currency`
- `Address`

No representa la entidad `Apartment`; representa los datos que necesita devolver la query.

`Id` es `Guid`, `Price` es `decimal` y `Name`, `Description` y `Currency` son `string`. `Address` es `AddressResponse`. Las propiedades tienen `init`, excepto `Address`, que tiene `set` para el callback de Dapper. Las cadenas empiezan vacias y la direccion tiene una instancia inicial.

`Price` procede de `apartments.price_amount`: es el precio base del apartamento, no el total de la estancia solicitada. Esta query no ejecuta `PricingServices` ni incorpora limpieza o recargos.

### AddressResponse

DTO usado dentro de `ApartmentResponse`.

Contiene:

- `Country`
- `State`
- `ZipCode`
- `City`
- `Street`

Se usa para mapear columnas de direccion que vienen desde SQL.

Todas sus propiedades son `string`, tienen `init` y empiezan con `string.Empty`. Es un DTO de lectura distinto del objeto de valor `Address` del dominio.

### SearchApartmentsQueryHandler

Busca apartamentos que no tengan reservas solapadas en el periodo pedido.

Dependencia:

- `ISqlConnectionFactory`

Flujo:

1. Si `StartDate > EndDate`, devuelve una lista vacia.
2. Define los estados de booking considerados activos: `Reserved` (1), `Confirmed` (2) y `Completed` (5).
3. Ejecuta SQL contra `apartments`.
4. Usa `NOT EXISTS` contra `bookings` para descartar apartamentos ocupados.
5. Usa Dapper multi-mapping para llenar `ApartmentResponse` y `AddressResponse`.
6. Devuelve la lista como `Result<IReadOnlyList<ApartmentResponse>>`.

La condicion de solapamiento usada es:

```sql
b.duration_start <= @EndDate
AND b.duration_end >= @StartDate
```

Esto detecta reservas que cruzan total o parcialmente el periodo solicitado.

Los extremos son inclusivos: una reserva que termina el dia 12 bloquea una que empieza el dia 12. `Rejected` y `Cancelled` no bloquean fechas. Los estados activos estan duplicados en este handler y en `BookingRepository`, por lo que ambos deben seguir la misma regla.

Si `StartDate > EndDate`, la lista vacia se devuelve como resultado exitoso, sin abrir conexion. La igualdad si se admite. No hay validador de esta query ni behaviors que la validen.

El multi-mapping usa `splitOn: "Country"`: las columnas anteriores rellenan `ApartmentResponse` y desde `Country` se construye `AddressResponse`. El callback asigna `apartment.Address = address`. El orden de columnas y sus alias forman parte del contrato del mapeo.

El SQL usa `NOT EXISTS` y `ANY(@ActiveBookingStatuses)` con parametros. No ordena, pagina ni limita los resultados. El handler no propaga el token a Dapper. Que un apartamento aparezca disponible no garantiza que siga libre al reservar; el comando debe comprobarlo de nuevo.

El controlador actual expone esta query como `GET /api/apartments?starDate=...&endDate=...`; el parametro HTTP se llama `starDate`, mientras el mensaje interno usa `StartDate`. Ademas, SQL consulta `apartments` y `bookings` sin comillas, pero la migracion inicial crea `Apartments` y `Bookings`. Esa discrepancia afecta tambien a esta lectura, independientemente del multi-mapping del DTO.

## Como usar Application desde el host

En un host que ya disponga de un `builder`, se registran ambas capas:

```csharp
using Bookify.Application;
using Bookify.Infrastructure;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

Este registro ya existe en `Bookify.Api/Program.cs`. El host proporciona logging y scopes por solicitud; los controladores reciben `ISender`. Configura `ConnectionStrings:Database` (equivalente a `DataBase` en la configuracion de .NET) y en Development llama despues a `ApplyMigration()`. El registro de servicios por si solo no crea ni migra tablas.

La [guia de Api](../Bookify.Api/readme.md) explica las rutas y respuestas: GET de reserva devuelve `200` o `404` segun el resultado; POST devuelve `201` con el id y `Location`, o `400` con `Error`. La API no tiene un manejador propio para convertir `ValidationException` de Application en una respuesta estructurada.

Despues, desde un endpoint, controller o servicio que reciba `ISender` por constructor, se usa MediatR:

```csharp
Result<Guid> result = await sender.Send(
    new ReserveBookingCommand(apartmentId, userId, startDate, endDate),
    cancellationToken);
```

Para una query:

```csharp
Result<BookingResponse> result = await sender.Send(
    new GetBookingQuery(bookingId),
    cancellationToken);
```

Estos fragmentos requieren los namespaces de sus casos de uso y los argumentos de entrada correspondientes. La query `GetBookingQuery` conserva las limitaciones descritas arriba.

Un metodo que devuelve `Task<Result<Guid>>` puede propagar el resultado de la reserva de esta forma:

```csharp
if (result.IsFailure)
{
    return Result.Failure<Guid>(result.Error);
}

Guid bookingId = result.Value;
return Result.Success(bookingId);
```

No se accede a `Value` en un resultado fallido, porque lanza `InvalidOperationException`. Ademas, el limite de entrada debe tratar por separado `Bookify.Application.Exceptions.ValidationException` y su propiedad `Errors`; esa excepcion no aparece dentro de `result.Error`. Las excepciones tecnicas no convertidas tambien pueden propagarse.

## Flujo completo de un comando

```text
sender.Send(command)
  -> LoggingBehavior
  -> ValidationBehavior
  -> CommandHandler
  -> Domain
  -> Repositories
  -> UnitOfWork
  -> Result
```

Este flujo centraliza validacion y logging para que los handlers se concentren en el caso de uso.

## Flujo completo de una query

```text
sender.Send(query)
  -> QueryHandler
  -> ISqlConnectionFactory
  -> Dapper
  -> DTO
  -> Result<T>
```

Las queries actuales no pasan por `LoggingBehavior` ni `ValidationBehavior` porque esos behaviors estan restringidos a `IBaseCommand`.

## Como ampliar Application

Para una escritura nueva, como confirmar una reserva:

1. Crear una carpeta del caso de uso y un record que implemente `ICommand` o `ICommand<T>`.
2. Agregar un `AbstractValidator<TCommand>` publico con las reglas de entrada necesarias. El registro por assembly lo descubrira.
3. Implementar el `ICommandHandler` correspondiente, solicitando interfaces por constructor.
4. Obtener la entidad, llamar a su metodo de dominio, comprobar el `Result` y guardar con `IUnitOfWork` cuando proceda.
5. Enviar el comando mediante `ISender.Send` para que se ejecute el pipeline.

`Booking.Confirm` ya existe en Domain, pero todavia no hay un comando de confirmacion en Application. Incorporarlo requiere comprobar tambien persistencia y concurrencia: el token de `Apartment` no protege automaticamente una modificacion aislada de `Booking`.

Para una lectura, crear `IQuery<TDto>`, el DTO y su `IQueryHandler`. Verificar que cada parametro existe en el objeto enviado a Dapper y que nombres y tipos de columnas coinciden con el DTO. Agregar un validador de query por si solo no basta para ejecutarlo: el behavior actual esta restringido a comandos.

Para reaccionar a un evento, implementar `INotificationHandler<TDomainEvent>`. El escaneo de MediatR lo registra, pero la entidad debe acumular el evento y el contexto publicarlo. Las operaciones externas de ese handler se ejecutan despues del guardado; no se dispone de outbox ni de reintentos duraderos.

## Puntos a revisar

- La traduccion de `DbUpdateConcurrencyException` a `ConcurrencyException` ya esta conectada en `ApplicationDbContext`. Debe comprobarse con solicitudes concurrentes contra la base de datos; definir la excepcion no demuestra por si mismo la proteccion de todas las escrituras.
- `BookingResponse` ya tiene corregidos los importes, monedas y alias de fechas. `GetBookingQueryHandler` mantiene discrepancias de parametro, tabla y columnas de recargo. Ambas queries consultan tablas en minusculas que no coinciden con las de la migracion inicial. Compilar no valida esas consultas SQL.
- `ValidationBehavior` usa validacion sincronica. Si se agregan validadores asincronos, deberia adaptarse.
- Logging no distingue un resultado fallido de negocio de uno exitoso y no registra el objeto excepcion.
- Las llamadas Dapper y la publicacion actual de eventos no reciben el token del request; `IEmailService` tampoco lo admite.
- No hay autorizacion ni reglas de fechas futuras en los casos de uso actuales.
- El correo es una implementacion vacia y el plazo de diez minutos solo aparece en el texto del mensaje.
- No existen comandos para confirmar, cancelar, rechazar, completar reservas, crear usuarios o dejar resenas. Algunas operaciones estan implementadas solo en Domain.
- El host Api ya esta conectado y existe la migracion inicial. No hay proyectos de pruebas en la solucion; la generacion del esquema no verifica las consultas Dapper, los datos materializados ni los conflictos reales.

## Resumen

`Bookify.Application` es la capa que ejecuta casos de uso. Usa MediatR para separar mensajes y handlers, FluentValidation para validar comandos, `Result` para devolver exito o fallo de negocio, repositorios para trabajar con entidades y Dapper para lecturas optimizadas.

Esta capa existe para que el host no tenga logica de negocio y para que Domain no tenga que conocer detalles de infraestructura.
