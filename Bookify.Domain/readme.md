# Bookify.Domain

Este proyecto contiene el nucleo del dominio de Bookify. Su responsabilidad es modelar las reglas principales del negocio: usuarios, apartamentos, reservas, resenas, dinero, errores y eventos de dominio.

La capa Domain no deberia depender de detalles externos como bases de datos, controladores HTTP, colas, archivos o frameworks de infraestructura. Aqui viven las entidades y objetos de valor que representan el lenguaje del negocio.

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

## Abstractions

La carpeta `Abstractions` contiene piezas compartidas por todo el dominio.

### Entity

`Entity` es la clase base para las entidades del dominio.

Una entidad tiene identidad propia mediante `Id`. Dos entidades pueden tener los mismos datos, pero si tienen distinto `Id`, representan objetos distintos del negocio.

Responsabilidades principales:

- Guardar el identificador de la entidad.
- Acumular eventos de dominio.
- Exponer los eventos mediante `GetDomainEvents`.
- Limpiar los eventos mediante `ClearDomainEvent`.
- Permitir que las entidades hijas levanten eventos con `RaiseDomainEvent`.

Ejemplo de uso:

```csharp
public sealed class Booking : Entity
{
    // Booking hereda Id y la capacidad de levantar eventos.
}
```

Cuando una reserva cambia de estado, puede levantar un evento:

```csharp
RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));
```

Normalmente la capa de infraestructura o un interceptor de persistencia lee esos eventos despues de guardar y los publica.

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
public static Error None = new(string.Empty, string.Empty);
public static Error NullValue = new("Error.NullValue", "Null value was provided.");
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
    return result.Error;
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

### IUnitOfWork

`IUnitOfWork` define una abstraccion para guardar cambios:

```csharp
Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
```

Su objetivo es que Application pueda confirmar una operacion sin conocer el detalle de persistencia.

En la practica, una implementacion con EF Core suele delegar en `DbContext.SaveChangesAsync`.

## Commons

La carpeta `Commons` contiene objetos de valor reutilizables.

### Currency

`Currency` representa una moneda.

Actualmente soporta:

- `USD`
- `EUR`
- `None`

Tiene una coleccion `All` con las monedas validas y el metodo `FromCode` para obtener una moneda por codigo.

Ejemplo:

```csharp
Currency currency = Currency.FromCode("EUR");
```

Si el codigo no existe, lanza una excepcion.

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

### Name

`Name` es un objeto de valor que representa el nombre de un apartamento.

```csharp
public record Name(string Value);
```

Aunque hoy solo envuelve un `string`, sirve para expresar intencion de dominio. No es cualquier texto: es el nombre de un apartamento.

### Description

`Description` representa la descripcion de un apartamento.

```csharp
public record Description(string Value);
```

Igual que `Name`, ayuda a evitar tipos primitivos dispersos por todo el dominio.

### Address

`Address` representa la direccion del apartamento.

Incluye:

- `Country`
- `State`
- `ZipCode`
- `City`
- `Street`

Es un objeto de valor porque no tiene identidad propia. Dos direcciones con los mismos datos representan la misma direccion.

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
- `MontainView`
- `GardenView`

Se usa en el calculo de precio para aplicar recargos segun ciertas comodidades.

### ApartmentErrors

`ApartmentErrors` centraliza errores del dominio relacionados con apartamentos.

Actualmente contiene:

```csharp
ApartmentErrors.NotFound
```

Este error se usa cuando se intenta operar sobre un apartamento que no existe.

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

Actualmente el codigo permite completar solo si esta en estado `Reserved`.

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

`LengthInDays` calcula la diferencia entre `End` y `Start`.

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

- `GardenView` o `MontainView`: 5%.
- `AirConditioning`: 2%.
- `Parking`: 1%.

### PricingDetails

`PricingDetails` es un record que agrupa el desglose de precio:

- `PriceForPeriod`
- `CleaningFee`
- `AmenitiesUpCharge`
- `TotalPrice`

Es el resultado del calculo realizado por `PricingServices`.

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

## Booking domain events

Los eventos de reservas comunican cambios importantes del ciclo de vida de una booking.

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

Esto evita que existan ratings invalidos dentro del dominio.

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

Booking booking = Booking.Reserve(
    apartment,
    user.Id,
    period,
    utcNow,
    pricingServices);

bookingRepository.Add(booking);

await unitOfWork.SaveChangesAsync(cancellationToken);

return booking.Id;
```

La idea es que Application coordina, pero las reglas importantes estan en Domain.

## Como usar Result

Cuando una operacion puede fallar por una regla esperada del negocio, conviene devolver `Result`.

Ejemplo:

```csharp
Result result = booking.Cancel(utcNow);

if (result.IsFailure)
{
    return result.Error;
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
    return reviewResult.Error;
}

Review review = reviewResult.Value;
```

No se debe acceder a `Value` si `IsFailure` es `true`, porque `Result<TValue>` lanza una excepcion en ese caso.

## Como usar eventos de dominio

Las entidades levantan eventos con `RaiseDomainEvent`.

Ejemplo:

```csharp
booking.RaiseDomainEvent(new BookingReservedDomainEvent(booking.Id));
```

Como `RaiseDomainEvent` es `protected`, solo la propia entidad o clases derivadas pueden levantar sus eventos.

El flujo habitual es:

1. Una entidad ejecuta una operacion de negocio.
2. La entidad agrega uno o mas eventos a su lista interna.
3. Application guarda cambios mediante `IUnitOfWork`.
4. Infrastructure obtiene los eventos con `GetDomainEvents`.
5. Infrastructure publica los eventos con MediatR.
6. Infrastructure limpia los eventos con `ClearDomainEvent`.

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
- Una reserva solo puede cancelarse si esta confirmada y no empezo.
- Una resena solo puede crearse si la reserva esta completada.
- Un rating solo es valido entre 1 y 5.
- Dos valores `Money` solo pueden sumarse si tienen la misma moneda.
- Los errores esperados se representan con `Error` y `Result`.
- Los cambios importantes se comunican con eventos de dominio.

## Resumen

`Bookify.Domain` contiene el modelo central del negocio. Sus entidades encapsulan identidad y comportamiento, sus objetos de valor expresan conceptos importantes, sus errores permiten fallos controlados, sus eventos comunican cambios relevantes y sus repositorios definen contratos sin acoplar el dominio a la base de datos.

La forma recomendada de usar esta capa es desde Application: Application recibe comandos o queries, obtiene entidades mediante repositorios, llama metodos del dominio, revisa `Result`, guarda con `IUnitOfWork` y deja que Infrastructure publique los eventos de dominio.
