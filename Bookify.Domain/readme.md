# Bookify.Domain

Este proyecto contiene el nucleo del dominio de Bookify. Su responsabilidad es modelar las reglas principales del negocio: usuarios, apartamentos, reservas, resenas, dinero, errores y eventos de dominio.

La capa Domain no deberia depender de detalles externos como bases de datos, controladores HTTP, colas, archivos o frameworks de infraestructura. Aqui viven las entidades y objetos de valor que representan el lenguaje del negocio.

[Guia de la solucion](../readme.md) | [Application](../Bookify.Application/readme.md) | [Infrastructure](../Bookify.Infrastructure/readme.md)

## Indice

- [Inventario de archivos](#inventario-de-archivos)
- [Abstracciones compartidas](#abstractions)
- [Dinero y monedas](#commons)
- [Apartamentos](#apartments)
- [Usuarios](#users)
- [Reservas y precios](#bookings)
- [Eventos de reservas](#booking-domain-events)
- [Resenas](#reviews)
- [Uso desde Application](#como-usar-esta-capa-desde-application)
- [Validacion y limites actuales](#puntos-a-revisar-en-el-codigo-actual)

## Bookify.Domain.csproj

El proyecto Domain es una biblioteca .NET:

```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>disable</Nullable>
```

Referencia `MediatR.Contracts` porque `IDomainEvent` implementa `INotification`. Esto permite que los eventos del dominio puedan ser publicados por Infrastructure y manejados por Application sin que Domain conozca los handlers concretos.

La version declarada es `2.0.1`. No hay referencias a otros proyectos, EF Core ni FluentValidation. La dependencia de contratos de MediatR existe, aunque el dominio no dependa del despacho de mensajes ni de sus implementaciones.

## Objetivo de la capa Domain

La capa Domain responde preguntas como:

- Que es un apartamento.
- Que es una reserva.
- Cuando se puede confirmar, rechazar, completar o cancelar una reserva.
- Cuando una reserva puede recibir una resena.
- Como se calcula el precio de una reserva.
- Como se representan errores esperados del negocio.
- Que eventos importantes ocurrieron en el dominio.

En una arquitectura limpia, Application orquesta casos de uso, Infrastructure persiste datos, y Domain protege las reglas centrales.

## Estructura general

```text
Bookify.Domain
|-- Abstractions
|-- Apartments
|-- Bookings
|-- Commons
|-- Reviews
|-- Users
```

## Inventario de archivos

Todos los archivos de codigo de la capa estan relacionados aqui. Los apartados posteriores desarrollan sus propiedades, reglas y uso. `Result.cs` contiene dos clases; los demas nombres de archivo corresponden a su tipo principal.

| Archivo | Funcion |
| --- | --- |
| [Bookify.Domain.csproj](Bookify.Domain.csproj) | Framework, nulabilidad y contrato de eventos. |
| [Abstractions/Entity.cs](Abstractions/Entity.cs) | Identidad y acumulacion de eventos. |
| [Abstractions/IDomainEvent.cs](Abstractions/IDomainEvent.cs) | Marca de notificacion de dominio. |
| [Abstractions/Error.cs](Abstractions/Error.cs) | Codigo y descripcion de error. |
| [Abstractions/Result.cs](Abstractions/Result.cs) | `Result` y `Result<TValue>`, resultados sin valor y con valor. |
| [Abstractions/IUnitOfWork.cs](Abstractions/IUnitOfWork.cs) | Contrato de guardado de cambios. |
| [Commons/Currency.cs](Commons/Currency.cs) | Monedas admitidas y conversion por codigo. |
| [Commons/Money.cs](Commons/Money.cs) | Importe, moneda y suma monetaria. |
| [Apartments/Apartment.cs](Apartments/Apartment.cs) | Apartamento, precios, direccion y comodidades. |
| [Apartments/Name.cs](Apartments/Name.cs) | Nombre con fabrica de validacion. |
| [Apartments/NameErrors.cs](Apartments/NameErrors.cs) | Fallos del nombre. |
| [Apartments/Description.cs](Apartments/Description.cs) | Texto de descripcion. |
| [Apartments/Address.cs](Apartments/Address.cs) | Direccion como objeto de valor. |
| [Apartments/Amenity.cs](Apartments/Amenity.cs) | Catalogo numerico de comodidades. |
| [Apartments/ApartmentErrors.cs](Apartments/ApartmentErrors.cs) | Error de apartamento inexistente. |
| [Apartments/IApartmentRepository.cs](Apartments/IApartmentRepository.cs) | Consulta por identificador. |
| [Users/User.cs](Users/User.cs) | Creacion y datos de usuario. |
| [Users/FirstName.cs](Users/FirstName.cs) | Nombre del usuario. |
| [Users/LastName.cs](Users/LastName.cs) | Apellido del usuario. |
| [Users/Email.cs](Users/Email.cs) | Direccion de correo del usuario. |
| [Users/UserErrors.cs](Users/UserErrors.cs) | Usuario inexistente y credenciales invalidas. |
| [Users/IUserRepository.cs](Users/IUserRepository.cs) | Consulta y agregado de usuarios. |
| [Users/Events/UserCreatedDomainEvent.cs](Users/Events/UserCreatedDomainEvent.cs) | Notifica creacion mediante `UserId`. |
| [Bookings/Booking.cs](Bookings/Booking.cs) | Creacion, importes y ciclo de vida de reserva. |
| [Bookings/BookingStatus.cs](Bookings/BookingStatus.cs) | Estados numericos de reserva. |
| [Bookings/BookingErrors.cs](Bookings/BookingErrors.cs) | Fallos esperados de reservas. |
| [Bookings/DateRange.cs](Bookings/DateRange.cs) | Periodo y longitud en dias. |
| [Bookings/PricingServices.cs](Bookings/PricingServices.cs) | Calculo del precio de estancia. |
| [Bookings/PricingDetails.cs](Bookings/PricingDetails.cs) | Desglose del calculo. |
| [Bookings/IBookingRepository.cs](Bookings/IBookingRepository.cs) | Consulta, disponibilidad y agregado de reservas. |
| [Bookings/Events/BookingReservedDomainEvent.cs](Bookings/Events/BookingReservedDomainEvent.cs) | Notifica reserva mediante `BookingId`. |
| [Bookings/Events/BookingConfirmedDomainEvent.cs](Bookings/Events/BookingConfirmedDomainEvent.cs) | Notifica confirmacion mediante `BookingId`. |
| [Bookings/Events/BookingRejectedDomainEvent.cs](Bookings/Events/BookingRejectedDomainEvent.cs) | Notifica rechazo mediante `BookingId`. |
| [Bookings/Events/BookingCompletedDomainEvent.cs](Bookings/Events/BookingCompletedDomainEvent.cs) | Notifica finalizacion mediante `BookingId`. |
| [Bookings/Events/BookingCancelledDomainEvent.cs](Bookings/Events/BookingCancelledDomainEvent.cs) | Notifica cancelacion mediante `BookingId`. |
| [Reviews/Review.cs](Reviews/Review.cs) | Fabrica de resenas para reservas completadas. |
| [Reviews/Rating.cs](Reviews/Rating.cs) | Puntuacion con fabrica de validacion. |
| [Reviews/Comment.cs](Reviews/Comment.cs) | Texto de la resena. |
| [Reviews/ReviewErrors.cs](Reviews/ReviewErrors.cs) | Error de reserva no elegible para resena. |
| [Reviews/Events/ReviewCreatedDomainEvent.cs](Reviews/Events/ReviewCreatedDomainEvent.cs) | Notifica creacion mediante `ReviewId`. |

Una entidad expresa identidad y comportamiento; los records de valor se comparan por sus datos. Usar un record no agrega validaciones automaticamente. Los errores y los eventos son datos, los repositorios son contratos de acceso, y `PricingServices` calcula una regla que necesita tanto apartamento como periodo.

## Abstractions

La carpeta `Abstractions` contiene piezas compartidas por todo el dominio.

### Entity

`Entity` es la clase base para las entidades del dominio.

Una entidad tiene identidad propia mediante `Id`. Dos entidades pueden tener los mismos datos, pero si tienen distinto `Id`, representan objetos distintos del negocio. Esta clase no sobrescribe igualdad: dos instancias CLR con el mismo identificador no se consideran iguales automaticamente por heredar de `Entity`.

Responsabilidades principales:

- Guardar el identificador de la entidad.
- Acumular eventos de dominio.
- Exponer los eventos mediante `GetDomainEvents`.
- Limpiar los eventos mediante `ClearDomainEvent`.
- Permitir que las entidades hijas levanten eventos con `RaiseDomainEvent`.

Ejemplo de uso:

```csharp
Entity entity = booking;
Guid id = entity.Id;
IReadOnlyList<IDomainEvent> pendingEvents = entity.GetDomainEvents();
```

Cuando una reserva cambia de estado, puede levantar un evento:

```csharp
RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));
```

El constructor protegido recibe el identificador y `Id` tiene `init`. `GetDomainEvents` devuelve una copia de la lista interna; obtenerla no elimina los eventos. `ClearDomainEvent` vacia la lista, y `RaiseDomainEvent` es protegido, por lo que lo invoca el codigo de la propia entidad.

En esta solucion `ApplicationDbContext` lee esos eventos despues de guardar, limpia las listas y publica la copia con MediatR. El dominio solo acumula los eventos en memoria.

### IDomainEvent

`IDomainEvent` representa algo importante que ocurrio dentro del dominio.

Implementa `INotification` de MediatR, lo que permite publicar eventos y manejarlos con handlers.

Ejemplos:

- `UserCreatedDomainEvent`
- `BookingReservedDomainEvent`
- `BookingConfirmedDomainEvent`
- `ReviewCreatedDomainEvent`

Un evento no deberia contener logica compleja. Solo comunica que algo paso y transporta los datos minimos necesarios.

### Error

`Error` representa un error esperado del negocio.

Tiene:

- `Code`: identificador tecnico del error.
- `Name`: descripcion legible del error.

Ejemplos:

```csharp
public static readonly Error None = new(string.Empty, string.Empty);
public static readonly Error NullValue = new("Error.NullValue", "Null value was provided.");
```

El objetivo es evitar depender de excepciones para reglas esperadas del negocio. Por ejemplo, si una reserva se solapa con otra, eso es un fallo de negocio controlado, no necesariamente un error inesperado del sistema.

### Result

`Result` representa el resultado de una operacion.

Puede ser:

- Exitoso: `Result.Success()`.
- Fallido: `Result.Failure(error)`.

Tambien existe `Result<TValue>`, que permite devolver un valor cuando la operacion fue exitosa.

Ejemplo:

```csharp
Result result = booking.Confirm(utcNow);

if (result.IsFailure)
{
    return Result.Failure(result.Error);
}
```

Ejemplo con valor:

```csharp
Result<Rating> ratingResult = Rating.Create(5);

if (ratingResult.IsSuccess)
{
    Rating rating = ratingResult.Value;
}
```

El constructor de `Result` protege la consistencia:

- Un resultado exitoso no puede tener error.
- Un resultado fallido no puede tener `Error.None`.

`Result<TValue>` tambien tiene una conversion implicita. Esto permite devolver directamente un valor y que se convierta en un resultado exitoso:

```csharp
return booking.Id;
```

Ese retorno se transforma en:

```csharp
Result.Success<Guid>(booking.Id);
```

Las dos clases siguen estando en `Abstractions/Result.cs`. `Result<TValue>` hereda de `Result`, recibe el valor en su constructor y lo expone mediante `Value`. No se permite leer `Value` si `IsFailure` es `true`: se lanza `InvalidOperationException`.

| Metodo o conversion | Comportamiento |
| --- | --- |
| `Success()` | Exito sin valor y con `Error.None`. |
| `Failure(error)` | Fallo sin valor; rechaza `Error.None`. |
| `Success<T>(value)` | Exito con el valor recibido. No comprueba expresamente que no sea nulo. |
| `Failure<T>(error)` | Fallo generico; conserva `default` internamente y bloquea leerlo. |
| `Create<T>(value)` | Exito si no es nulo; si es nulo, devuelve `Error.NullValue`. |
| Conversion implicita de `T` a `Result<T>` | Delega en `Create`, incluida su regla para nulos. |

`[NotNull]` sobre `Value` comunica una poscondicion a los analizadores de nulabilidad; no ejecuta una comprobacion en tiempo de ejecucion. Lo mismo ocurre con el operador de supresion `!` en `field!`. La proteccion frente a nulos depende del camino de construccion usado; `Success<string>(null)` no pasa por `Create`.

No existe conversion implicita de `Error` a `Result`. Un metodo que devuelve `Result<T>` propaga un error con `Result.Failure<T>(error)`, no con `return error;`.

### IUnitOfWork

`IUnitOfWork` define una abstraccion para guardar cambios:

```csharp
Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
```

Su objetivo es que Application pueda confirmar una operacion sin conocer el detalle de persistencia.

La implementacion actual es `ApplicationDbContext`. Guarda los cambios pendientes del mismo contexto que usan los repositorios, publica los eventos y devuelve el entero producido por EF Core. Application conoce el contrato, no el mecanismo de almacenamiento.

## Commons

La carpeta `Commons` contiene objetos de valor reutilizables.

### Currency

`Currency` representa una moneda.

Expone `Currency.Usd` para `USD` y `Currency.Eur` para `EUR`. Ademas tiene `Currency.None`, un valor interno con codigo vacio usado por `Money.Zero()`; no es una moneda publica admitida por `FromCode`.

Tiene una coleccion `All` con las monedas validas y el metodo `FromCode` para obtener una moneda por codigo.

Ejemplo:

```csharp
Currency currency = Currency.FromCode("EUR");
```

`All` contiene solamente USD y EUR. `FromCode` busca coincidencia exacta, sin normalizar mayusculas ni espacios; un codigo desconocido, vacio o como `eur` lanza `ApplicationException`. `Code` tiene `init`: la fabrica expresa la via prevista de obtencion, pero el record permite crear copias modificadas con `with`.

### Money

`Money` representa una cantidad monetaria con moneda.

Propiedades:

- `Amount`
- `Currency`

Incluye un operador `+` para sumar dinero:

```csharp
Money total = first + second;
```

La suma solo es valida cuando ambas cantidades usan la misma moneda. Si las monedas son distintas, lanza `InvalidOperationException`.

Tambien ofrece:

```csharp
Money.Zero()
Money.Zero(currency)
money.IsZero()
```

Este objeto evita tratar importes como simples `decimal`, porque en el dominio el importe y la moneda forman una sola idea.

`Money` es un record con constructor publico. No impide importes negativos, no convierte divisas y no redondea importes. `IsZero` compara con cero en la misma moneda. Incluso una suma con importe cero exige monedas iguales; `Money.Zero()` usa la moneda interna vacia, mientras `Money.Zero(Currency.Eur)` si es compatible con importes EUR.

## Apartments

La carpeta `Apartments` modela los apartamentos disponibles para reservar.

### Apartment

`Apartment` es una entidad del dominio.

Propiedades principales:

- `Id`
- `Name`
- `Description`
- `Address`
- `Price`
- `CleaningFeeAmount`
- `LastBookedOnUTC`
- `Amenities`

Representa una propiedad que puede ser reservada por usuarios.

`LastBookedOnUTC` tiene setter `internal`, por lo que puede ser modificado desde el mismo ensamblado Domain, pero no libremente desde fuera. Actualmente se actualiza cuando se crea una reserva:

```csharp
apartment.LastBookedOnUTC = utcNow;
```

Esto permite que el dominio deje constancia de la ultima vez que el apartamento fue reservado.

Tambien permite que Infrastructure detecte un cambio del apartamento al guardar una reserva. El token de concurrencia se configura como propiedad sombra de EF; no existe una propiedad `Version` en esta clase. La proteccion requiere que el apartamento este seguido y que se emita su actualizacion, como se explica en [Infrastructure](../Bookify.Infrastructure/readme.md#concurrencia).

`Apartment` tiene constructor publico y recibe todos sus datos, incluida la lista de comodidades. El setter privado de `Amenities` impide sustituirla desde fuera, pero la lista sigue siendo mutable: se pueden agregar elementos y repetir comodidades. El constructor no comprueba precios negativos ni monedas compatibles entre precio y limpieza.

### Name

`Name` es un objeto de valor que representa el nombre de un apartamento.

```csharp
public sealed record Name
```

A diferencia de otros objetos de valor simples, `Name` protege una regla propia del dominio. No se crea directamente con constructor publico, sino con:

```csharp
Result<Name> result = Name.Create(value);
```

Reglas actuales:

- El valor no puede estar vacio ni contener solo espacios.
- El valor debe tener exactamente `Name.ExactLength` caracteres.

Actualmente:

```csharp
public const int ExactLength = 200;
```

Si el nombre es valido, devuelve un `Result<Name>` exitoso. Si no lo es, devuelve un error de `NameErrors`.

La comprobacion usa `string.Length` y no recorta espacios. En .NET esa longitud cuenta unidades UTF-16, que no siempre equivalen a caracteres visuales. El valor debe tener longitud 200 y no ser solo espacios; no se trata de una longitud maxima de 200.

Ejemplo deliberadamente artificial para mostrar la regla exacta:

```csharp
Result<Name> nameResult = Name.Create(new string('A', Name.ExactLength));
Result<Name> invalidName = Name.Create("Apartamento centro");
// nameResult es exitoso; invalidName devuelve NameErrors.InvalidLength.
```

### Description

`Description` representa la descripcion de un apartamento.

```csharp
public record Description(string Value);
```

Igual que `Name`, ayuda a evitar tipos primitivos dispersos por todo el dominio.

Su constructor es publico y no valida longitud ni contenido. El limite de 2000 de Infrastructure es una configuracion de persistencia, no una regla ejecutada por este record.

### Address

`Address` representa la direccion del apartamento.

Incluye:

- `Country`
- `State`
- `ZipCode`
- `City`
- `Street`

Es un objeto de valor porque no tiene identidad propia. Dos direcciones con los mismos datos representan la misma direccion.

Los cinco campos son cadenas y no se validan en este archivo. La igualdad del record compara sus datos, pero no normaliza direcciones ni comprueba que existan geograficamente.

### Amenity

`Amenity` es un enum con las comodidades del apartamento.

Valores actuales:

- `Wifi`
- `AirConditioning`
- `Parking`
- `PetFriendly`
- `SwimmingPool`
- `Gym`
- `Spa`
- `Terrace`
- `MountainView`
- `GardenView`

Se usa en el calculo de precio para aplicar recargos segun ciertas comodidades.

Los valores numericos son del 1 al 10 en el orden listado. Solo vistas a jardin/montana, aire acondicionado y aparcamiento tienen recargo en `PricingServices`; el resto aporta cero al calculo actual.

### ApartmentErrors

`ApartmentErrors` centraliza errores del dominio relacionados con apartamentos.

Actualmente contiene:

```csharp
ApartmentErrors.NotFound
```

Este error se usa cuando se intenta operar sobre un apartamento que no existe.

### NameErrors

`NameErrors` centraliza errores del objeto de valor `Name`.

Errores actuales:

- `Empty`: el nombre es obligatorio.
- `InvalidLength`: el nombre no cumple la longitud esperada.

Se usan desde `Name.Create` para evitar que se construyan nombres invalidos.

### IApartmentRepository

`IApartmentRepository` es el contrato para obtener apartamentos.

```csharp
Task<Apartment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

Domain define la necesidad, pero no implementa el acceso a datos. La implementacion concreta deberia vivir en Infrastructure.

## Users

La carpeta `Users` modela los usuarios de la plataforma.

### User

`User` es una entidad del dominio.

Propiedades:

- `Id`
- `FirstName`
- `LastName`
- `Email`

No se crea directamente con `new` desde fuera porque su constructor es privado. Se crea mediante el metodo de fabrica:

```csharp
User user = User.Create(firstName, lastName, email);
```

Al crear un usuario, la entidad levanta un evento:

```csharp
UserCreatedDomainEvent
```

Esto permite que otras partes del sistema reaccionen, por ejemplo enviando un correo de bienvenida o registrando auditoria.

Esos efectos son posibilidades, no implementaciones actuales: no hay handler de `UserCreatedDomainEvent` ni comando de alta de usuarios en Application. `User.Create` genera un `Guid`, asigna los objetos de valor y acumula el evento, pero no guarda ni verifica unicidad o formato del email.

### FirstName

`FirstName` representa el nombre del usuario.

```csharp
public record FirstName(string Value);
```

### LastName

`LastName` representa el apellido del usuario.

```csharp
public record LastName(string Value);
```

Tanto `FirstName` como `LastName` tienen constructor publico y no validan obligatoriedad ni longitud. El limite de 200 caracteres configurado por EF no se aplica al construir estos records en memoria.

### Email

`Email` representa el correo electronico del usuario.

```csharp
public record Email(string Value);
```

Actualmente no valida formato, pero encapsula el concepto para poder agregar validaciones despues sin cambiar todo el codigo que lo usa.

### UserErrors

`UserErrors` centraliza errores del dominio relacionados con usuarios.

Errores actuales:

- `NotFound`
- `InvalidCredentials`

Se usan para devolver fallos controlados desde casos de uso de Application.

`NotFound` se utiliza al reservar. `InvalidCredentials` esta declarado, pero no hay un caso de uso de autenticacion ni contrasenas en el modelo de usuario actual.

### IUserRepository

`IUserRepository` define el contrato de persistencia para usuarios.

```csharp
Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

void Add(User user);
```

Permite buscar usuarios y agregar usuarios nuevos sin que Domain conozca la base de datos.

## Bookings

La carpeta `Bookings` contiene la parte mas rica del dominio actual: la reserva de apartamentos.

### Booking

`Booking` es una entidad del dominio y representa una reserva.

Propiedades principales:

- `Id`
- `ApartmentId`
- `UserId`
- `Duration`
- `PriceForPeriod`
- `CleaningFee`
- `AmenitiesUpChange`
- `TotalPrice`
- `Status`
- `CreatedOnUtc`
- `ConfirmedOnUtc`
- `RejectedOnUtc`
- `CompletedOnUtc`
- `CancelledOnUtc`

No se crea directamente con `new` desde fuera. Se crea mediante:

```csharp
Booking booking = Booking.Reserve(
    apartment,
    userId,
    duration,
    utcNow,
    pricingServices);
```

`Reserve` hace varias cosas:

- Calcula el precio usando `PricingServices`.
- Crea la reserva con estado `Reserved`.
- Levanta `BookingReservedDomainEvent`.
- Actualiza `apartment.LastBookedOnUTC`.

`Reserve` no consulta otras reservas ni valida que el usuario exista: esas comprobaciones las coordina `ReserveBookingCommandHandler`. No guarda datos ni publica el evento inmediatamente. `AmenitiesUpChange` es el nombre literal de la propiedad de `Booking`; `PricingDetails` usa `AmenitiesUpCharge`, una diferencia que tambien debe considerarse en el mapeo SQL.

Las fechas de confirmacion, rechazo, finalizacion y cancelacion son `DateTime?` y empiezan sin valor. Los metodos reciben `utcNow` desde fuera; no obtienen el reloj por si mismos.

Despues de reservada, la booking puede cambiar de estado mediante metodos de dominio.

#### Confirm

Confirma una reserva.

```csharp
Result result = booking.Confirm(utcNow);
```

Solo permite confirmar si la reserva esta en estado `Reserved`. Si no lo esta, devuelve:

```csharp
BookingErrors.NotReserved
```

Si la operacion es valida:

- Cambia `Status` a `Confirmed`.
- Asigna `ConfirmedOnUtc`.
- Levanta `BookingConfirmedDomainEvent`.
- Devuelve `Result.Success()`.

#### Reject

Rechaza una reserva.

```csharp
Result result = booking.Reject(utcNow);
```

Solo permite rechazar si esta en estado `Reserved`.

Si la operacion es valida:

- Cambia `Status` a `Rejected`.
- Asigna `RejectedOnUtc`.
- Levanta `BookingRejectedDomainEvent`.

#### Complete

Completa una reserva.

```csharp
Result result = booking.Complete(utcNow);
```

Actualmente el codigo permite completar solo si esta en estado `Confirmed`.

No comprueba que la fecha actual haya alcanzado `Duration.End`. Una reserva confirmada puede completarse antes del final si se llama a este metodo con ese estado.

Si la operacion es valida:

- Cambia `Status` a `Completed`.
- Asigna `CompletedOnUtc`.
- Levanta `BookingCompletedDomainEvent`.

#### Cancel

Cancela una reserva.

```csharp
Result result = booking.Cancel(utcNow);
```

Reglas actuales:

- Solo permite cancelar si la reserva esta `Confirmed`.
- No permite cancelar si la fecha actual ya es posterior al inicio de la reserva.

La comparacion exacta es `DateOnly.FromDateTime(utcNow) > Duration.Start`: el mismo dia de inicio si se permite cancelar. No se compara la hora dentro de ese dia.

Si la operacion es valida:

- Cambia `Status` a `Cancelled`.
- Asigna `CancelledOnUtc`.
- Levanta `BookingCancelledDomainEvent`.

### BookingStatus

`BookingStatus` es un enum con los estados posibles de una reserva:

- `Reserved`
- `Confirmed`
- `Rejected`
- `Cancelled`
- `Completed`

Este enum evita trabajar con strings sueltos para representar estados.

Los valores persistidos son Reserved=1, Confirmed=2, Rejected=3, Cancelled=4 y Completed=5. Application utiliza esos mismos valores en las consultas de disponibilidad.

| Operacion | Estado requerido | Estado final | Error si no se cumple |
| --- | --- | --- | --- |
| `Reserve` | Crea una entidad nueva. | `Reserved` | No devuelve `Result`; puede propagar excepciones del calculo. |
| `Confirm` | `Reserved` | `Confirmed` | `NotReserved`. |
| `Reject` | `Reserved` | `Rejected` | `NotReserved`. |
| `Complete` | `Confirmed` | `Completed` | `NotConfirmed`. |
| `Cancel` | `Confirmed` y fecha actual menor o igual al inicio. | `Cancelled` | `NotConfirmed` o `AlreadyStarted`. |

Cada transicion valida asigna su fecha, acumula su evento y devuelve exito. Si falla una comprobacion, el metodo termina antes de cambiar el estado. No son operaciones idempotentes: por ejemplo, confirmar de nuevo una reserva ya confirmada devuelve `NotReserved`.

### DateRange

`DateRange` representa el rango de fechas de una reserva.

Propiedades:

- `Start`
- `End`
- `LengthInDays`

Se crea con:

```csharp
DateRange period = DateRange.Create(startDate, endDate);
```

`DateRange.Create` lanza una excepcion si `Start` es posterior a `End`. Si el rango es valido, crea el objeto de valor.

`LengthInDays` calcula la diferencia entre `End` y `Start`.

La formula es `End.DayNumber - Start.DayNumber`. Un periodo del dia 10 al 13 tiene longitud 3; inicio y fin iguales producen cero. La excepcion de fechas invertidas es `ApplicationException`.

El comando de reserva exige `StartDate < EndDate` mediante FluentValidation, mientras esta fabrica y la query de disponibilidad permiten igualdad. Las consultas de solapamiento consideran ambos extremos inclusivos. Son reglas distintas que deben conocerse al definir que representa la fecha de salida.

Aunque el constructor es privado, `Start` y `End` tienen `init` y el tipo es un record: una copia `with` puede modificar las fechas sin pasar por `Create`. La fabrica no garantiza por si sola que todas las copias posibles mantengan el orden.

### PricingServices

`PricingServices` calcula el precio de una reserva.

Entrada:

- Apartamento.
- Periodo de reserva.

Salida:

```csharp
PricingDetails
```

El calculo considera:

- Precio base por noche.
- Cantidad de dias.
- Moneda del apartamento.
- Tarifa de limpieza.
- Recargos por comodidades.

Comodidades con recargo:

- `GardenView` o `MountainView`: 5%.
- `AirConditioning`: 2%.
- `Parking`: 1%.

El total actual se calcula sumando:

- precio del periodo;
- recargo por comodidades;
- tarifa de limpieza, si no es cero.

Los porcentajes se suman por cada elemento de `Amenities` y se aplican una sola vez sobre el precio del periodo. Si aparecen ambas vistas, aportan 10%; si se repite una comodidad, tambien se repite su contribucion. No se deduplican comodidades ni se redondea el resultado.

Ejemplo: precio diario 100 EUR, tres dias, `GardenView` y `Parking`, limpieza 20 EUR. El periodo cuesta 300 EUR, el recargo es `300 * (0.05 + 0.01) = 18 EUR` y el total es 338 EUR. La limpieza se suma una vez por estancia. Si tiene importe distinto de cero y moneda diferente al precio, `Money.operator +` lanza `InvalidOperationException`.

Application registra este servicio sin estado como transient y se lo entrega al handler, que a su vez lo pasa a `Booking.Reserve`.

### PricingDetails

`PricingDetails` es un record que agrupa el desglose de precio:

- `PriceForPeriod`
- `CleaningFee`
- `AmenitiesUpCharge`
- `TotalPrice`

Es el resultado del calculo realizado por `PricingServices`.

Es un record de datos, no una entidad persistida por separado. `Booking.Reserve` copia sus cuatro objetos `Money` a la reserva para conservar el desglose calculado en ese momento.

### BookingErrors

`BookingErrors` centraliza errores de reservas.

Errores actuales:

- `NotFound`: no existe la reserva.
- `Overlap`: la reserva se solapa con otra existente.
- `NotReserved`: la reserva no esta en estado reservado.
- `NotConfirmed`: la reserva no esta confirmada.
- `AlreadyStarted`: la reserva ya comenzo.

Estos errores se usan con `Result` para expresar fallos esperados.

### IBookingRepository

`IBookingRepository` define el contrato de persistencia para reservas.

```csharp
Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

Task<bool> IsOverlappingAsync(
    Apartment apartment,
    DateRange duration,
    CancellationToken cancellationToken = default);

void Add(Booking booking);
```

La parte mas importante es `IsOverlappingAsync`, que permite consultar si ya existe una reserva para el mismo apartamento en fechas que se cruzan.

El handler de Application usa este contrato antes de crear una reserva.

La implementacion actual esta en Infrastructure. La interfaz no toma bloqueos, no guarda y no garantiza que el resultado de disponibilidad siga siendo valido despues de la consulta. Esa coordinacion se completa con `SaveChangesAsync` y la configuracion de concurrencia del apartamento.

## Booking domain events

Los eventos de reservas comunican cambios importantes del ciclo de vida de una booking.

Los cinco archivos de `Bookings/Events` contienen records `sealed` que implementan `IDomainEvent` y transportan solo un `Guid BookingId`. No contienen la entidad completa, logica de envio ni una fecha del evento. Actualmente solo el evento reservado tiene consumidor en Application; los demas pueden publicarse sin un handler registrado que produzca efectos adicionales.

### BookingReservedDomainEvent

Se levanta cuando se crea una nueva reserva.

Dato principal:

- `BookingId`

### BookingConfirmedDomainEvent

Se levanta cuando una reserva pasa a estado `Confirmed`.

Dato principal:

- `BookingId`

### BookingRejectedDomainEvent

Se levanta cuando una reserva pasa a estado `Rejected`.

Dato principal:

- `BookingId`

### BookingCompletedDomainEvent

Se levanta cuando una reserva pasa a estado `Completed`.

Dato principal:

- `BookingId`

### BookingCancelledDomainEvent

Se levanta cuando una reserva pasa a estado `Cancelled`.

Dato principal:

- `BookingId`

## Reviews

La carpeta `Reviews` modela las resenas que los usuarios pueden dejar despues de una reserva.

### Review

`Review` es una entidad del dominio.

Propiedades:

- `Id`
- `ApartmentId`
- `BookingId`
- `UserId`
- `Rating`
- `Comment`
- `CreatedOnUtc`

Se crea con:

```csharp
Result<Review> result = Review.Create(
    booking,
    rating,
    comment,
    createdOnUtc);
```

Regla principal:

- Solo se puede crear una resena si la reserva esta en estado `Completed`.

Si la reserva no esta completada, devuelve:

```csharp
ReviewErrors.NotEligible
```

Si la resena se crea correctamente:

- Copia `ApartmentId`, `BookingId` y `UserId` desde la reserva.
- Asigna rating, comentario y fecha de creacion.
- Levanta `ReviewCreatedDomainEvent`.
- Devuelve un `Result<Review>` exitoso.

El autor se copia de `booking.UserId`; no se recibe como parametro independiente. El metodo no comprueba si ya existe otra resena para la reserva ni autentica al llamador. No hay contrato de repositorio de resenas ni caso de uso de Application para guardarlas actualmente.

### Rating

`Rating` representa la puntuacion de una resena.

Se crea con:

```csharp
Result<Rating> ratingResult = Rating.Create(5);
```

Regla:

- El valor debe estar entre 1 y 5.

Si el valor esta fuera de rango, devuelve:

```csharp
Rating.Invalid
```

La fabrica rechaza esas entradas y centraliza `Rating.Invalid`, cuyo codigo es `Rating.Invalid`. Sin embargo, `Value` tiene `init` y el tipo es un record: una copia `with { Value = 10 }` de un rating existente evita la fabrica. La regla describe la creacion mediante `Create`, no una garantia absoluta sobre cualquier copia del record.

### Comment

`Comment` representa el comentario escrito en una resena.

```csharp
public record Comment(string Value);
```

Actualmente no tiene validaciones, pero encapsula el concepto para poder agregarlas luego.

### ReviewErrors

`ReviewErrors` centraliza errores relacionados con resenas.

Actualmente contiene:

```csharp
ReviewErrors.NotEligible
```

Se usa cuando se intenta crear una resena para una reserva que todavia no fue completada.

### ReviewCreatedDomainEvent

Se levanta cuando una resena se crea correctamente.

Dato principal:

- `ReviewId`

## User domain events

### UserCreatedDomainEvent

Se levanta cuando se crea un usuario con `User.Create`.

Dato principal:

- `UserId`

Este evento permite que otros componentes reaccionen a la creacion del usuario sin acoplar esa logica directamente dentro de `User`.

## Como usar esta capa desde Application

Application deberia ser quien orquesta los casos de uso.

Ejemplo simplificado para reservar:

El fragmento presupone un metodo de Application que devuelve `Task<Result<Guid>>`, las dependencias recibidas por constructor y la validacion de entrada ya ejecutada. Utiliza tambien `Bookify.Application.Exceptions.ConcurrencyException` en el limite de guardado.

```csharp
User? user = await userRepository.GetByIdAsync(userId, cancellationToken);

if (user is null)
{
    return Result.Failure<Guid>(UserErrors.NotFound);
}

Apartment? apartment = await apartmentRepository.GetByIdAsync(apartmentId, cancellationToken);

if (apartment is null)
{
    return Result.Failure<Guid>(ApartmentErrors.NotFound);
}

DateRange period = DateRange.Create(startDate, endDate);

bool isOverlapping = await bookingRepository.IsOverlappingAsync(
    apartment,
    period,
    cancellationToken);

if (isOverlapping)
{
    return Result.Failure<Guid>(BookingErrors.Overlap);
}

try
{
    Booking booking = Booking.Reserve(
        apartment,
        user.Id,
        period,
        utcNow,
        pricingServices);

    bookingRepository.Add(booking);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return booking.Id;
}
catch (Bookify.Application.Exceptions.ConcurrencyException)
{
    return Result.Failure<Guid>(BookingErrors.Overlap);
}
```

La idea es que Application coordina, pero las reglas importantes estan en Domain.

`ApplicationDbContext` convierte actualmente `DbUpdateConcurrencyException` en `ConcurrencyException`, y el handler la transforma en el fallo de negocio. Domain no necesita conocer ninguna de esas excepciones tecnicas para calcular precios o cambiar estados.

## Como usar Result

Cuando una operacion puede fallar por una regla esperada del negocio, conviene devolver `Result`.

Ejemplo:

```csharp
Result result = booking.Cancel(utcNow);

if (result.IsFailure)
{
    return Result.Failure(result.Error);
}
```

Cuando la operacion devuelve un valor:

```csharp
Result<Review> reviewResult = Review.Create(
    booking,
    rating,
    comment,
    utcNow);

if (reviewResult.IsFailure)
{
    return Result.Failure<Review>(reviewResult.Error);
}

Review review = reviewResult.Value;
```

No se debe acceder a `Value` si `IsFailure` es `true`, porque `Result<TValue>` lanza una excepcion en ese caso.

## Como usar eventos de dominio

Las entidades levantan eventos con `RaiseDomainEvent`.

Por ejemplo, desde el cuerpo de un metodo de instancia de `Booking`:

```csharp
RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));
```

Como `RaiseDomainEvent` es `protected`, el consumidor externo no puede llamar a `booking.RaiseDomainEvent(...)`. Invoca `booking.Confirm(utcNow)` y la entidad acumula el evento cuando la regla permite la transicion.

El flujo habitual es:

1. Una entidad ejecuta una operacion de negocio.
2. La entidad agrega uno o mas eventos a su lista interna.
3. Application guarda cambios mediante `IUnitOfWork`.
4. Infrastructure obtiene los eventos con `GetDomainEvents`.
5. Infrastructure limpia las listas de las entidades con `ClearDomainEvent`.
6. Infrastructure publica la copia de eventos uno a uno con MediatR y espera a sus handlers.

El orden importa: la limpieza sucede antes de publicar. Si un handler falla, el guardado ya termino en el flujo actual y no hay una cola persistida que permita recuperar automaticamente los eventos. El handler de reserva solicita un correo, pero la implementacion actual del envio es vacia.

## Como usar repositorios

Los repositorios en Domain son interfaces. Definen lo que el dominio necesita, no como se accede a los datos.

Ejemplo:

```csharp
public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsOverlappingAsync(
        Apartment apartment,
        DateRange duration,
        CancellationToken cancellationToken = default);

    void Add(Booking booking);
}
```

La implementacion concreta deberia estar en Infrastructure, por ejemplo usando EF Core.

Esto permite que Domain no dependa de la base de datos.

## Reglas importantes del dominio actual

- Un usuario se crea con `User.Create` y levanta `UserCreatedDomainEvent`.
- Una reserva se crea con `Booking.Reserve` y empieza en estado `Reserved`.
- Una reserva no deberia confirmarse, rechazarse o completarse si no esta en el estado requerido por el metodo.
- Una reserva solo puede completarse si esta confirmada.
- Una reserva solo puede cancelarse si esta confirmada y la fecha actual no es posterior al dia de inicio.
- Una resena solo puede crearse si la reserva esta completada.
- Un rating solo es valido entre 1 y 5.
- El nombre de un apartamento se crea con `Name.Create` y debe cumplir su longitud exacta.
- Dos valores `Money` solo pueden sumarse si tienen la misma moneda.
- Los errores esperados se representan con `Error` y `Result`.
- Los cambios importantes se comunican con eventos de dominio.

## Puntos a revisar en el codigo actual

Estos puntos no impiden entender la capa, pero conviene tenerlos presentes:

- `Name.ExactLength` exige exactamente 200 caracteres, mientras `NameErrors.InvalidLength` dice `"The length max 75."`. El mensaje parece no coincidir con la regla real.
- `DateRange.Create` permite `Start == End`, lo que produce `LengthInDays == 0`. Si el negocio no permite reservas de cero dias, habria que reforzar esa regla.
- Domain tiene `<Nullable>disable</Nullable>`, aunque Application e Infrastructure tienen nullable habilitado. Conviene decidir si el dominio tambien deberia activar nulabilidad.
- `Name` y `Rating` validan sus fabricas, pero no todos los objetos de valor validan contenido. `FirstName`, `LastName`, `Email`, `Description`, `Comment` y `Address` aceptan sus datos por constructor sin esas comprobaciones.
- Los limites de longitud de EF son de almacenamiento; no hacen que el constructor de un record valide automaticamente.
- Los records con propiedades `init` como `Rating`, `Currency` y `DateRange` permiten copias modificadas que no pasan por sus fabricas.
- `Complete` no comprueba el final de la estancia y `Confirm` no aplica el plazo de diez minutos mencionado en el correo de Application.
- La lista de comodidades es mutable y acepta repetidos; los repetidos tambien afectan al calculo del recargo.
- `BookingErrors.NotFound` esta declarado, pero la query actual `GetBooking` convierte un resultado nulo en `Error.NullValue`.
- El token de concurrencia pertenece al mapeo de `Apartment` en Infrastructure. Los metodos del dominio por si solos no consultan disponibilidad ni garantizan que no se inserten reservas solapadas desde otros caminos.

## Resumen

`Bookify.Domain` contiene el modelo central del negocio. Sus entidades encapsulan identidad y comportamiento, sus objetos de valor expresan conceptos importantes, sus errores permiten fallos controlados, sus eventos comunican cambios relevantes y sus repositorios definen contratos sin acoplar el dominio a la base de datos.

La forma recomendada de usar esta capa es desde Application: Application recibe comandos o queries, obtiene entidades mediante repositorios, llama metodos del dominio, revisa `Result`, guarda con `IUnitOfWork` y deja que Infrastructure publique los eventos de dominio.
