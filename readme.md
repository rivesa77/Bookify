# Bookify

Este repositorio contiene una solucion .NET organizada por capas. El objetivo es separar el codigo que expresa reglas de negocio del codigo que orquesta casos de uso y del codigo que habla con herramientas externas como bases de datos, reloj del sistema o servicios de email.

La solucion esta orientada a una arquitectura limpia con CQRS ligero:

- `Bookify.Domain`: reglas centrales del negocio.
- `Bookify.Application`: casos de uso, comandos, queries, validacion y contratos de servicios externos.
- `Bookify.Infrastructure`: implementaciones concretas con EF Core, PostgreSQL, Dapper, reloj y email.
- Proyecto raiz `Bookify`: punto de entrada actual de la aplicacion.

La solucion contiene bibliotecas con los casos de uso y sus implementaciones, pero todavia no un host conectado que pueda atender peticiones. Esta documentacion describe el codigo actual y distingue las funciones implementadas de las partes pendientes.

## Guias por capa

| Guia | Que explica |
| --- | --- |
| [Domain](Bookify.Domain/readme.md) | Cada entidad, objeto de valor, error, evento y contrato; reglas, transiciones y calculo de precios. |
| [Application](Bookify.Application/readme.md) | Cada mensaje, handler, validador, behavior, DTO y contrato externo; como invocarlos y ampliar los casos de uso. |
| [Infrastructure](Bookify.Infrastructure/readme.md) | Cada configuracion, repositorio y servicio; guardado, eventos, concurrencia y conexiones. |

Cada guia incluye un inventario enlazado de todos los archivos de codigo y configuracion de su capa. Las carpetas `bin/` y `obj/` son salidas generadas, no componentes que haya que mantener manualmente.

## Por que se ha organizado asi

La separacion por capas busca que cada parte tenga una responsabilidad clara:

- Domain no conoce la base de datos ni frameworks de infraestructura.
- Application conoce el dominio y define lo que necesita para ejecutar casos de uso.
- Infrastructure implementa esos contratos usando herramientas reales.
- El proyecto de entrada compone la aplicacion y deberia registrar las capas necesarias.

Esta organizacion permite cambiar detalles externos con menos impacto sobre las reglas del dominio. Por ejemplo, un cambio de proveedor de correo afecta principalmente a Infrastructure. Cambiar PostgreSQL requiere revisar tambien el SQL de Application, porque las lecturas conocen tablas, columnas y sintaxis del proveedor. Entidades como `Booking`, `Apartment` o `User` expresan las reglas de negocio independientemente de esas consultas.

## Estructura de proyectos

```text
Bookify
|-- Bookify.csproj
|-- Bookify.slnx
|-- Program.cs
|-- Bookify.Domain
|-- Bookify.Application
|-- Bookify.Infrastructure
```

## Direccion de dependencias

Las referencias de proyecto que existen en los archivos `.csproj` son:

```text
Bookify.Infrastructure -> Bookify.Application -> Bookify.Domain
Bookify (raiz): sin referencias de proyecto declaradas
```

Infrastructure utiliza Domain a traves de la referencia transitiva de Application. Domain no depende de ninguna otra capa, aunque si usa `MediatR.Contracts` para sus eventos. Application usa MediatR, FluentValidation, Dapper y contratos de logging. Infrastructure incorpora el proveedor EF de PostgreSQL.

Un host futuro necesita referenciar y registrar Application e Infrastructure. Esa es la composicion prevista, pero aun no es una referencia existente desde el proyecto raiz. Todas las bibliotecas apuntan a `net10.0`; Domain tiene nullable deshabilitado y las otras capas lo tienen habilitado.

## Proyecto raiz Bookify

El proyecto raiz contiene el punto de entrada actual.

### Bookify.csproj

Archivo: [Bookify.csproj](Bookify.csproj).

`Bookify.csproj` define el proyecto ejecutable:

```xml
<OutputType>Exe</OutputType>
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

Actualmente es un proyecto de consola sencillo. No tiene referencias declaradas a `Bookify.Application` ni a `Bookify.Infrastructure`.

Cuando este proyecto se convierta en API o host real, normalmente deberia:

- Referenciar `Bookify.Application`.
- Referenciar `Bookify.Infrastructure`.
- Registrar servicios de Application e Infrastructure.
- Exponer endpoints, controllers o Minimal APIs.

Ejemplo esperado en un host web futuro:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

### Program.cs

Archivo: [Program.cs](Program.cs).

`Program.cs` actualmente contiene:

```csharp
Console.WriteLine("Hello, World!");
```

Esto significa que el punto de entrada todavia es un placeholder. No esta arrancando una API, no registra inyeccion de dependencias y no ejecuta casos de uso.

Cuando el proyecto crezca, este archivo deberia convertirse en el lugar donde se crea el host y se conectan las capas.

### Bookify.slnx

Archivo: [Bookify.slnx](Bookify.slnx).

`Bookify.slnx` agrupa los proyectos de la solucion.

Actualmente contiene:

- `Bookify.Application`
- `Bookify.Domain`
- `Bookify.Infrastructure`

Tambien declara una carpeta `/Tests/`, aunque todavia no hay proyectos de prueba listados.

El ejecutable `Bookify.csproj` de la raiz no aparece en esta solucion. Compilar `Bookify.slnx` compila las tres bibliotecas, no valida el proyecto de consola como host.

La carpeta de solucion aparece como `/Scr/`. Por el nombre, probablemente intenta representar `src`, pero no afecta al funcionamiento del codigo.

## Resumen de cada capa

### Domain

Domain contiene el modelo de negocio. Aqui viven:

- entidades como `Booking`, `Apartment`, `User` y `Review`;
- objetos de valor como `Money`, `Currency`, `DateRange`, `Rating`, `Name`, `Email`;
- errores de negocio como `BookingErrors`, `UserErrors`, `ApartmentErrors`;
- eventos de dominio como `BookingReservedDomainEvent`;
- contratos de repositorio como `IBookingRepository`;
- abstracciones transversales como `Result`, `Entity` e `IUnitOfWork`.

Domain es la capa mas importante desde el punto de vista de reglas de negocio.

Mas detalle: [Bookify.Domain/readme.md](Bookify.Domain/readme.md)

### Application

Application contiene los casos de uso. Aqui se decide que pasos se ejecutan para cumplir una accion del sistema.

Contiene:

- comandos como `ReserveBookingCommand`;
- queries como `GetBookingQuery` y `SearchApartmentsQuery`;
- handlers de MediatR;
- validadores con FluentValidation;
- pipeline behaviors de logging y validacion;
- contratos para servicios externos, como `IEmailService`, `IDateTimeProvider` e `ISqlConnectionFactory`;
- excepciones propias de la capa de aplicacion.

Application no configura EF Core ni construye Npgsql. Define contratos y coordina, pero contiene el SQL de las queries y por ello debe mantenerse alineada con el esquema persistido.

Mas detalle: [Bookify.Application/readme.md](Bookify.Application/readme.md)

### Infrastructure

Infrastructure contiene detalles tecnicos.

Contiene:

- `ApplicationDbContext` de EF Core;
- configuraciones de entidades;
- repositorios concretos;
- conexion Dapper con PostgreSQL;
- type handler para `DateOnly`;
- implementacion del reloj del sistema;
- implementacion actual del servicio de email;
- registro de dependencias de infraestructura.

Infrastructure existe para que Domain y Application no queden acopladas a herramientas concretas.

Mas detalle: [Bookify.Infrastructure/readme.md](Bookify.Infrastructure/readme.md)

## Flujo de uso esperado

Un flujo tipico de escritura seria:

```text
Host futuro
  -> MediatR Send(command)
  -> LoggingBehavior
  -> ValidationBehavior
  -> CommandHandler en Application
  -> Repositorios definidos en Domain
  -> Implementaciones en Infrastructure
  -> Entidades de Domain ejecutan reglas
  -> IUnitOfWork guarda cambios
  -> ApplicationDbContext publica eventos de dominio
```

Ejemplo conceptual:

```csharp
Result<Guid> result = await sender.Send(
    new ReserveBookingCommand(apartmentId, userId, startDate, endDate),
    cancellationToken);
```

Un flujo tipico de lectura seria:

```text
Host futuro
  -> MediatR Send(query)
  -> QueryHandler en Application
  -> ISqlConnectionFactory
  -> Dapper ejecuta SQL
  -> DTO de respuesta
```

Ejemplo conceptual:

```csharp
Result<BookingResponse> result = await sender.Send(
    new GetBookingQuery(bookingId),
    cancellationToken);
```

Los ejemplos muestran los contratos de invocacion. `GetBookingQueryHandler` tiene discrepancias actuales entre sus parametros SQL, columnas y DTO; estan detalladas en la guia de Application. Registrar o compilar el proyecto no valida automaticamente esa consulta.

## Que esta implementado

| Capacidad | Domain | Application / Infrastructure |
| --- | --- | --- |
| Reservar | `Booking.Reserve`, precios y evento. | Comando, validador, handler, repositorios y traduccion de conflictos implementados; falta integracion real del host y la base de datos. |
| Buscar disponibilidad | Contrato de repositorio y estados. | Query Dapper con filtro por reservas coincidentes. |
| Consultar una reserva | Entidad y error `BookingErrors.NotFound`. | Query y DTO existentes con incoherencias de parametros y mapeo pendientes. |
| Confirmar, rechazar, completar y cancelar | Metodos de `Booking` con sus reglas y eventos. | No hay comandos para estas operaciones. |
| Crear usuarios | `User.Create` y evento. | Repositorio y mapeo existentes; no hay comando de alta ni handler de ese evento. |
| Dejar resenas | `Rating.Create`, `Review.Create` y evento. | Mapeo existente; no hay repositorio ni caso de uso de resenas. |
| Enviar correo de reserva | Evento reservado. | Handler existente; `EmailService` es vacio y no envia correo real. |
| Caducidad de diez minutos | No hay comprobacion de ese plazo. | El plazo solo aparece en el texto del correo; no hay una tarea de expiracion. |

## Flujo de errores y concurrencia

Los fallos esperados de la reserva se devuelven como `Result<Guid>`: usuario inexistente, apartamento inexistente o solapamiento. La entrada invalida de FluentValidation lanza `ValidationException`. Otros errores tecnicos pueden propagarse; `Result` no convierte todas las excepciones por si mismo.

El manejo de concurrencia esta conectado entre capas:

```text
ApartmentConfiguration configura una Version sombra
Booking.Reserve cambia Apartment.LastBookedOnUTC
EF Core detecta un conflicto de version al actualizar
  -> DbUpdateConcurrencyException
ApplicationDbContext la traduce
  -> ConcurrencyException de Application
ReserveBookingCommandHandler la captura
  -> Result.Failure<Guid>(BookingErrors.Overlap)
```

El token es optimista y esta configurado en `Apartment`, no en todas las entidades. Requiere una actualizacion efectiva del apartamento seguido por EF. No es una restriccion de intervalos que proteja cualquier insercion realizada por otro camino. Los conflictos no se reintentan automaticamente. La [guia de Infrastructure](Bookify.Infrastructure/readme.md#concurrencia) explica sus condiciones y alcance.

Los eventos se acumulan en Domain y se publican despues de guardar. El contexto copia y limpia las listas antes de publicarlos uno a uno, esperando su finalizacion. Si un handler falla en ese punto, los datos ya estan guardados en el flujo actual; no existe outbox ni entrega duradera de eventos.

## Preparacion y comprobacion

Con el SDK de .NET 10 instalado, la compilacion de las bibliotecas se comprueba desde la raiz con:

```powershell
dotnet build Bookify.slnx
```

Para ejecutar los casos de uso todavia hace falta componer un host, registrar ambas capas y logging, configurar `ConnectionStrings:DataBase` y preparar el esquema PostgreSQL. No hay migraciones, configuracion de despliegue ni endpoints implementados en el codigo revisado. `AddInfrastructure` no crea tablas automaticamente.

La compilacion verifica tipos y referencias. No comprueba que el modelo EF pueda materializar las entidades, que SQL coincida con las tablas desplegadas, que el correo se envie ni que dos reservas concurrentes se resuelvan correctamente. Esas comprobaciones requieren integracion; no hay proyectos de pruebas en la solucion.

## Puntos actuales a tener en cuenta

- El proyecto raiz aun es un ejecutable de consola con `Hello, World!`.
- No hay endpoints HTTP en el estado actual del repositorio.
- No hay proyectos de tests en la solucion, aunque existe la carpeta logica `/Tests/`.
- Infrastructure espera una cadena de conexion llamada `DataBase`.
- Application ya esta preparada para MediatR, validacion y queries Dapper.
- `ApplicationDbContext` convierte los conflictos de EF Core en `ConcurrencyException`, que el handler de reserva traduce a `BookingErrors.Overlap`.
- `GetBookingQueryHandler` usa `@BookingId` con un objeto que tiene `Id`, y presenta diferencias de alias, tipos de monedas y nombre del recargo respecto al modelo.
- El nombre de apartamento exige exactamente 200 caracteres, aunque su mensaje de error todavia menciona 75. La validacion del dominio y los limites de almacenamiento no son intercambiables.
- El pipeline de logging y validacion solo se aplica a comandos, no a queries ni notificaciones. Logging no distingue un resultado de negocio fallido de uno exitoso.

## Como leer esta documentacion

Para entender el proyecto sin abrir cada archivo:

1. Lee este README para entender la estructura general.
2. Lee `Bookify.Domain/readme.md` para entender las reglas del negocio.
3. Lee `Bookify.Application/readme.md` para entender los casos de uso.
4. Lee `Bookify.Infrastructure/readme.md` para entender como se persiste y se conectan servicios externos.

Cada README explica que contiene la capa, como se usa, que papel cumple cada archivo y por que se ha separado asi.

Al agregar un caso de uso, revisar su entrada, handler, validador, DTO y servicios en Application; las invariantes en Domain; y el mapeo, consultas y guardado en Infrastructure. Las tres guias deben conservar la misma descripcion de estados, fechas, errores y eventos.
