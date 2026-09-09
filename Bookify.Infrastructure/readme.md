# Bookify.Infrastructure

Este proyecto contiene la capa de infraestructura de Bookify. Su responsabilidad es implementar detalles tecnicos que las capas internas necesitan, pero no deben conocer directamente.

Aqui viven las implementaciones concretas de persistencia, acceso SQL, fecha/hora del sistema y envio de email.

[Guia de la solucion](../readme.md) | [Domain](../Bookify.Domain/readme.md) | [Application](../Bookify.Application/readme.md) | [Api](../Bookify.Api/readme.md)

## Indice

- [Inventario de archivos](#inventario-de-archivos)
- [Registro y tiempos de vida](#dependencyinjection)
- [Contexto y eventos](#applicationdbcontext)
- [Mapeo de entidades](#configurations)
- [Migraciones y esquema](#migrations)
- [Repositorios](#repositories)
- [Conexiones y fechas de Dapper](#data)
- [Reloj](#clock)
- [Correo](#email)
- [Concurrencia](#concurrencia)
- [Limitaciones actuales](#puntos-a-revisar)

## Objetivo de la capa Infrastructure

Infrastructure responde preguntas como:

- Como se guarda una entidad en la base de datos.
- Como se configura EF Core.
- Como se mapean objetos de valor como `Money`, `Name`, `Rating` o `DateRange`.
- Como se abre una conexion SQL para Dapper.
- Como se obtiene la fecha UTC actual.
- Como se envia un email.
- Como se publican eventos de dominio despues de guardar cambios.

Application define contratos; Infrastructure los implementa.

## Por que se ha realizado asi

Esta capa existe para proteger el dominio y los casos de uso de detalles externos.

Por ejemplo:

- Domain define `IBookingRepository`, pero no sabe nada de EF Core.
- Application usa `ISqlConnectionFactory`, pero no sabe que se implementa con `NpgsqlConnection`.
- Application usa `IDateTimeProvider`, pero no llama directamente a `DateTime.UtcNow`.
- Application usa `IEmailService`, pero no conoce el proveedor real de email.

Esto permite cambiar herramientas externas con menor impacto sobre las reglas de negocio.

La separacion no elimina todas las dependencias tecnicas: Application contiene el SQL de las lecturas, y ese SQL depende del esquema y, en la busqueda, de sintaxis de PostgreSQL. Un cambio de proveedor o de columnas requiere revisar tambien los query handlers.

## Dependencias principales

El proyecto referencia:

- `Bookify.Application`: para implementar contratos definidos por Application.
- `Bookify.Domain`: a traves de Application y para mapear entidades del dominio.
- `Npgsql.EntityFrameworkCore.PostgreSQL`: proveedor EF Core para PostgreSQL.
- `EFCore.NamingConventions`: convenciones de nombres, especialmente `snake_case`.
- `Microsoft.Extensions.Configuration.Abstractions`: lectura de configuracion.

## Estructura

```text
Bookify.Infrastructure
|-- ApplicationDbContext.cs
|-- DependencyInjection.cs
|-- Clock
|-- Configurations
|-- Data
|-- Email
|-- Migrations
|-- Repositories
|-- Bookify.Infrastructure.csproj
```

## Bookify.Infrastructure.csproj

Define la capa como biblioteca .NET:

```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

Paquetes principales:

- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `EFCore.NamingConventions`
- `Microsoft.Extensions.Configuration.Abstractions`

Referencia:

```xml
<ProjectReference Include="..\Bookify.Application\Bookify.Application.csproj" />
```

Al depender de Application, Infrastructure puede implementar interfaces como:

- `IDateTimeProvider`
- `IEmailService`
- `ISqlConnectionFactory`

Y tambien puede registrar repositorios definidos en Domain.

Las versiones declaradas son `EFCore.NamingConventions` 10.0.1, `Microsoft.Extensions.Configuration.Abstractions` 10.0.11 y `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. Domain se alcanza de forma transitiva a traves de Application; no hay una segunda referencia directa de proyecto a Domain en este archivo.

## Inventario de archivos

| Archivo | Responsabilidad |
| --- | --- |
| [Bookify.Infrastructure.csproj](Bookify.Infrastructure.csproj) | Dependencias de infraestructura y framework. |
| [DependencyInjection.cs](DependencyInjection.cs) | Registro de implementaciones y configuracion del proveedor. |
| [ApplicationDbContext.cs](ApplicationDbContext.cs) | Unidad de trabajo, modelo EF, guardado, eventos y traduccion de concurrencia. |
| [Configurations/ApartmentConfiguration.cs](Configurations/ApartmentConfiguration.cs) | Mapeo de apartamento y token optimista. |
| [Configurations/BookingConfiguration.cs](Configurations/BookingConfiguration.cs) | Mapeo de reserva, importes, periodo y relaciones. |
| [Configurations/ReviewConfiguration.cs](Configurations/ReviewConfiguration.cs) | Mapeo de resena, puntuacion y relaciones. |
| [Configurations/UserConfiguration.cs](Configurations/UserConfiguration.cs) | Mapeo de usuario e indice unico de email. |
| [Repositories/Repository.cs](Repositories/Repository.cs) | Base generica para consultar y agregar entidades. |
| [Repositories/ApartmentRepository.cs](Repositories/ApartmentRepository.cs) | Implementacion de `IApartmentRepository`. |
| [Repositories/BookingRepository.cs](Repositories/BookingRepository.cs) | Implementacion de `IBookingRepository` y consulta de solapamiento. |
| [Repositories/UserRepository.cs](Repositories/UserRepository.cs) | Implementacion de `IUserRepository`. |
| [Data/SqlConnectionFactory.cs](Data/SqlConnectionFactory.cs) | Construye y abre conexiones Npgsql para lecturas. |
| [Data/DateOnlyTypeHandler.cs](Data/DateOnlyTypeHandler.cs) | Adaptacion entre fechas SQL y `DateOnly` para Dapper. |
| [Clock/DateTimeProvider.cs](Clock/DateTimeProvider.cs) | Acceso al reloj UTC del sistema. |
| [Email/EmailService.cs](Email/EmailService.cs) | Implementacion provisional sin envio real de correo. |
| [Migrations/20260909102321_Initial_Database.cs](Migrations/20260909102321_Initial_Database.cs) | Operaciones Up/Down para crear o retirar el esquema inicial. |
| [Migrations/20260909102321_Initial_Database.Designer.cs](Migrations/20260909102321_Initial_Database.Designer.cs) | Identifica la migracion y describe el modelo destino de ese cambio. |
| [Migrations/ApplicationDbContextModelSnapshot.cs](Migrations/ApplicationDbContextModelSnapshot.cs) | Modelo de referencia para calcular diferencias al generar la siguiente migracion. |

Las implementaciones de repositorios, configuraciones, reloj, conexion y correo son `internal`. El host accede a ellas a traves de los registros publicos de `DependencyInjection`, en vez de construirlas directamente. `ApplicationDbContext` si es publico.

## DependencyInjection

`DependencyInjection.cs` contiene:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
```

Este metodo registra los servicios concretos de infraestructura.

### Servicios registrados

Servicios transitorios:

- `IDateTimeProvider -> DateTimeProvider`
- `IEmailService -> EmailService`

Repositorios scoped:

- `IApartmentRepository -> ApartmentRepository`
- `IBookingRepository -> BookingRepository`
- `IUserRepository -> UserRepository`

Unit of Work:

```csharp
services.AddScoped<IUnitOfWork>(
    sp => sp.GetRequiredService<ApplicationDbContext>());
```

Esto significa que el mismo `ApplicationDbContext` registrado en la peticion actua como `IUnitOfWork`.

Base de datos:

```csharp
options.UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention();
```

La cadena de conexion se obtiene con:

```csharp
configuration.GetConnectionString("DataBase")
```

Tambien registra:

- `ISqlConnectionFactory` como singleton.
- `DateOnlyTypeHandler` en Dapper.

### Tiempos de vida y dependencias compartidas

| Registro | Tiempo de vida | Consecuencia |
| --- | --- | --- |
| `ApplicationDbContext` | Scoped mediante `AddDbContext`. | Una sesion EF por scope. |
| `IUnitOfWork` | Scoped, resuelve el contexto existente. | Guarda los cambios del mismo contexto que usan los repositorios. |
| Los tres repositorios | Scoped. | Comparten seguimiento y cambios pendientes dentro de la operacion. |
| Reloj y correo | Transient. | Se crea una instancia por resolucion. |
| `ISqlConnectionFactory` | Singleton. | Comparte la fabrica y la cadena, no una conexion abierta. |
| `DateOnlyTypeHandler` | Registro estatico de Dapper. | La conversion queda disponible para Dapper en el proceso. |

El scope debe crearlo el host. En una API suele corresponder a una peticion; en un proceso de consola o trabajo en segundo plano hay que crear el scope para la operacion. No se deben ejecutar operaciones EF simultaneas sobre la misma instancia de contexto.

### Uso esperado

Desde el host:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Y en configuracion debe existir una cadena:

```json
{
  "ConnectionStrings": {
    "DataBase": "..."
  }
}
```

`AddInfrastructure` lanza `ArgumentNullException(nameof(configuration))` si falta `ConnectionStrings:DataBase`. La comprobacion actual detecta ausencia (`null`), pero no valida que una cadena vacia o mal formada pueda abrirse. Registrar los servicios tampoco crea tablas ni aplica migraciones.

Para resolver `ApplicationDbContext` hace falta el `IPublisher` que registra MediatR a traves de `AddApplication`. Un host completo registra ambas capas:

```csharp
using Bookify.Application;
using Bookify.Infrastructure;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

Este registro ya se ejecuta en `Bookify.Api/Program.cs`. La API resuelve las implementaciones desde sus controladores a traves de MediatR. En Development, ademas, aplica las migraciones con `ApplyMigration()` despues de construir el host.

La clave `Database` del archivo `Bookify.Api/appsettings.Development.json` coincide con `DataBase`, porque la configuracion de .NET no distingue mayusculas en las claves. Su host `bookify-db` es valido dentro de la red Compose. Desde Windows, con el puerto 5432 publicado, debe sobrescribirse la cadena con host `localhost`, por ejemplo mediante `ConnectionStrings__Database`.

## ApplicationDbContext

`ApplicationDbContext` es el `DbContext` de EF Core y tambien implementa `IUnitOfWork`.

```csharp
public sealed class ApplicationDbContext : DbContext, IUnitOfWork
```

Responsabilidades:

- Representar la sesion de trabajo con la base de datos.
- Aplicar configuraciones de entidades.
- Guardar cambios.
- Publicar eventos de dominio despues de guardar.

El constructor recibe `DbContextOptions` y `IPublisher`. No declara propiedades `DbSet<T>`: los repositorios acceden mediante `Set<TEntity>()` y las configuraciones incorporan las entidades al modelo.

### OnModelCreating

Aplica automaticamente todas las configuraciones del assembly:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(
    typeof(ApplicationDbContext).Assembly);
```

Esto detecta clases como:

- `ApartmentConfiguration`
- `BookingConfiguration`
- `ReviewConfiguration`
- `UserConfiguration`

### SaveChangesAsync

Sobrescribe `SaveChangesAsync` para agregar comportamiento despues del guardado.

Flujo:

1. Ejecuta `base.SaveChangesAsync`.
2. Si el guardado es exitoso, publica eventos de dominio.
3. Devuelve el entero de `base.SaveChangesAsync`, que representa las entradas de estado escritas por EF, no un identificador de reserva.
4. Si se recibe `DbUpdateConcurrencyException`, la convierte en `Bookify.Application.Exceptions.ConcurrencyException`, conservandola como `InnerException`.

La conversion ya esta implementada:

```csharp
catch (DbUpdateConcurrencyException ex)
{
    throw new ConcurrencyException("Concurrency exception occurred.", ex);
}
```

Application puede capturar su propio contrato de excepcion sin conocer EF Core. Los errores que no son de ese tipo se propagan. El `try` incluye tanto el guardado como la publicacion de eventos.

El comportamiento adicional esta en la sobrecarga `SaveChangesAsync(CancellationToken)` utilizada por `IUnitOfWork`. No hay una sobrescritura de `SaveChanges()` ni de `SaveChangesAsync(bool, CancellationToken)` en esta clase; un consumidor que use esas otras entradas no debe dar por aplicada esta publicacion personalizada.

### Publicacion de eventos de dominio

`PublishDomainEventAsync`:

1. Busca todas las entradas del ChangeTracker que son `Entity`.
2. Obtiene sus eventos con `GetDomainEvents`.
3. Limpia los eventos con `ClearDomainEvent`.
4. Publica cada evento con `IPublisher` de MediatR.

Esto conecta el dominio con handlers de eventos ubicados en Application.

Ejemplo:

```text
Booking.Reserve levanta BookingReservedDomainEvent
SaveChangesAsync guarda en base de datos
ApplicationDbContext publica el evento
BookingReservedDomainEventHandler envia email
```

Los eventos se copian y se limpian de **todas** las entidades seguidas antes de empezar el bucle de publicacion. Se publican uno a uno y cada llamada a `Publish` se espera. Esto no es una cola ni un proceso independiente. Los eventos que aparecieran durante los handlers no se agregan a la instantanea que ya se esta recorriendo.

Si falla un handler despues de guardar, en el flujo actual los datos ya estan persistidos y las listas de eventos ya fueron limpiadas. Los eventos posteriores del bucle no se publican, y no hay almacenamiento duradero ni reintentos. Un outbox seria una ampliacion para resolver esa entrega, no una funcionalidad existente.

El token del request se pasa a `base.SaveChangesAsync`, pero `PublishDomainEventAsync` no recibe token y llama a `publisher.Publish(domainEvent)` sin proporcionarlo.

## Configurations

Esta carpeta contiene configuraciones de EF Core por entidad. Se usa Fluent API para mantener el dominio libre de atributos de persistencia.

`HasConversion` transforma un objeto de valor de una propiedad en un valor persistible y lo reconstruye al leer. `OwnsOne` modela un objeto dependiente del propietario, como `Money` o `Address`; no crea un repositorio independiente para ese objeto. `HasOne<T>().WithMany().HasForeignKey(...)` establece relaciones por identificador sin exigir propiedades de navegacion en las entidades.

Las configuraciones llaman a `ToTable` con `Apartments`, `Bookings`, `Reviews` y `Users`. Aunque el registro global agrega snake_case, la migracion inicial conserva esos nombres explicitos de tabla con mayusculas y genera las columnas en snake_case. Las consultas Dapper usan `apartments` y `bookings` sin comillas; no coinciden con esas tablas entrecomilladas de PostgreSQL. La discrepancia sigue pendiente en el codigo actual.

### ApartmentConfiguration

Configura la entidad `Apartment`.

Responsabilidades:

- Mapear a tabla `Apartments`.
- Definir `Id` como clave primaria.
- Mapear `Address` como owned type.
- Convertir `Name` a string y reconstruirlo con `Name.Create(value).Value`.
- Limitar `Name` a 200 caracteres.
- Convertir `Description` a string.
- Limitar `Description` a 2000 caracteres.
- Mapear `Price` como owned type.
- Mapear `CleaningFeeAmount` como owned type.
- Convertir `Currency` usando `Currency.Code` y `Currency.FromCode`.
- Configurar una propiedad sombra `Version` como row version para concurrencia optimista.

La configuracion de `Version`:

```csharp
builder.Property<uint>("Version").IsRowVersion();
```

sirve para detectar modificaciones concurrentes sobre el mismo apartamento.

Es una propiedad sombra: no hay una propiedad CLR `Apartment.Version` en Domain. EF conserva su valor y la utiliza para concurrencia en la persistencia. No debe interpretarse como un bloqueo pesimista ni como un token presente en todas las entidades.

`HasMaxLength(200)` limita la capacidad de almacenamiento, mientras `Name.Create` exige exactamente 200 caracteres. Son comprobaciones distintas: la configuracion EF no ejecuta validacion de longitud exacta sobre cualquier entrada. Al leer, `Name.Create(value).Value` falla si la cadena almacenada no cumple la fabrica.

### BookingConfiguration

Configura la entidad `Booking`.

Responsabilidades:

- Mapear a tabla `Bookings`.
- Definir `Id` como clave primaria.
- Mapear como owned types los objetos `Money`: `PriceForPeriod`, `CleaningFee`, `AmenitiesUpChange` y `TotalPrice`.
- Convertir monedas mediante `Currency.Code` y `Currency.FromCode`.
- Mapear `Duration` como owned type.
- Definir relacion con `Apartment` mediante `ApartmentId`.
- Definir relacion con `User` mediante `UserId`.

Este mapeo permite persistir objetos ricos del dominio sin cambiar sus tipos a modelos anemicos.

`Booking` no tiene token de concurrencia configurado en este archivo. La proteccion del flujo de reserva procede de actualizar `Apartment`. Un futuro comando que solo modifique el estado de una reserva no obtiene automaticamente esa proteccion.

El nombre de la propiedad es literalmente `AmenitiesUpChange`, mientras `PricingDetails` y las consultas usan `AmenitiesUpCharge`. No hay un `HasColumnName` que unifique esa diferencia. Los campos `amenities_up_charge_*` solicitados por `GetBookingQueryHandler` deben comprobarse frente al esquema generado.

### ReviewConfiguration

Configura la entidad `Review`.

Responsabilidades:

- Mapear a tabla `Reviews`.
- Definir `Id` como clave primaria.
- Convertir `Rating` a entero y reconstruirlo con `Rating.Create(value).Value`.
- Convertir `Comment` a string.
- Limitar `Comment` a 200 caracteres.
- Definir relacion con `Apartment`.
- Definir relacion con `Booking`.
- Definir relacion con `User`.

La conversion de `Rating` garantiza que al materializar desde base de datos se use el mismo objeto de valor del dominio.

Si la puntuacion almacenada no esta entre 1 y 5, la fabrica devuelve fallo y `.Value` lanza una excepcion. No hay en esta configuracion una restriccion explicita de rango ni un indice unico por `BookingId`. Tampoco existe `IReviewRepository`, su implementacion o un caso de uso de resenas en Application actualmente.

### UserConfiguration

Configura la entidad `User`.

Responsabilidades:

- Mapear a tabla `Users`.
- Definir `Id` como clave primaria.
- Convertir `FirstName` a string.
- Convertir `LastName` a string.
- Convertir `Email` a string.
- Limitar `FirstName` y `LastName` a 200 caracteres.
- Limitar `Email` a 400 caracteres.
- Crear indice unico sobre `Email`.

El indice unico protege que no existan dos usuarios con el mismo email en la base de datos.

Esa proteccion requiere que el indice se haya creado realmente en la base de datos. La configuracion no normaliza mayusculas ni espacios, y el objeto `Email` tampoco valida formato. La unicidad depende de los valores y de las reglas de comparacion de la columna; no equivale a un sistema de autenticacion.

## Migrations

Infrastructure contiene la migracion `20260909102321_Initial_Database`. Las herramientas de EF construyen el modelo a partir de `ApplicationDbContext` y sus configuraciones, y generan cambios de esquema. La creacion de archivos y su aplicacion a PostgreSQL son pasos distintos.

### Constructores y creacion del modelo

Las entidades `Apartment`, `Booking`, `Review` y `User` tienen ahora constructores privados vacios, y `Entity` ofrece un constructor protegido vacio para sus derivadas. Esto resuelve el error de enlace del constructor de `Apartment` con `Address`, `Price` y `CleaningFeeAmount`, configurados como owned. `Booking` tambien necesita una via compatible para sus navegaciones owned.

`HasConversion` convierte valores escalares y permite enlazarlos como propiedades; `OwnsOne` configura dependientes que EF debe materializar como parte del grafo. El constructor privado vacio no obliga a hacer publicos los setters ni a invocar las fabricas de creacion al leer. `Amenities` es una coleccion primitiva, no uno de los parametros owned rechazados en aquel error. Ver [Domain: materializacion](../Bookify.Domain/readme.md#constructores-y-materializacion).

No hay una fabrica `IDesignTimeDbContextFactory` en el proyecto. Las herramientas usan Api como proyecto de inicio para obtener el proveedor de servicios, con el contexto y `IPublisher` registrados. Un aviso de licencia emitido por MediatR al resolverlo es distinto de un error de validacion del modelo de EF.

### Initial_Database.cs

`Up(MigrationBuilder)` crea las tablas en orden de dependencias: primero `Apartments` y `Users`, despues `Bookings` y finalmente `Reviews`.

| Tabla | Columnas y relaciones principales |
| --- | --- |
| `Apartments` | `id` UUID; nombre de hasta 200, descripcion de hasta 2000; cinco columnas `address_*`; `price_amount`/`price_currency`; `cleaning_fee_amount_amount`/`cleaning_fee_amount_currency`; `last_booked_on_utc`; `amenities` como `integer[]`; token `xmin` de tipo `xid`. |
| `Users` | `id` UUID, nombre y apellido de hasta 200 y email de hasta 400. Indice unico `ix_users_email`. |
| `Bookings` | Id y referencias a apartamento y usuario, `duration_start`/`duration_end`, importes y monedas del periodo, limpieza, recargo `amenities_up_change_*` y total; estado y fechas del ciclo de vida. |
| `Reviews` | Id y referencias a apartamento, reserva y usuario, puntuacion, comentario de hasta 200 y fecha de creacion. |

Los importes son `numeric`, las fechas de estancia `date` y los instantes `timestamp with time zone`. Hay indices sobre las claves externas de reservas y resenas. Todas las claves externas de esta migracion usan borrado en cascada; no se ha configurado un indice unico que limite las resenas a una por reserva.

`Version` es una propiedad sombra del modelo que el proveedor mapea a la columna de sistema `xmin`. Su presencia en la migracion como `xid` con `rowVersion: true` refleja ese mapeo; no es una propiedad publica del dominio ni una columna de version que Application incremente manualmente.

`Down(MigrationBuilder)` elimina primero `Reviews`, despues `Bookings` y finalmente `Apartments` y `Users`, respetando sus dependencias. Revertir hasta antes de la migracion inicial elimina esas tablas y sus datos. No hay `InsertData`, datos de ejemplo ni comandos de SQL de carga inicial.

### Initial_Database.Designer.cs

Es la otra parte de la clase parcial `Initial_Database`. Los atributos `DbContext` y `Migration` vinculan el contexto con el identificador `20260909102321_Initial_Database`. `BuildTargetModel` conserva el modelo destino de esa migracion: tipos, conversiones persistidas, columnas, relaciones, indices y navegaciones owned.

Este archivo permite a las herramientas conocer el estado del modelo asociado al cambio. No es un segundo `Up` ni otra migracion independiente. La anotacion `ProductVersion` del modelo generado es `10.0.4`; las versiones declaradas de paquetes se documentan por separado en el apartado del proyecto.

### ApplicationDbContextModelSnapshot.cs

Hereda de `ModelSnapshot` y reconstruye el ultimo modelo versionado mediante `BuildModel`. Al agregar una nueva migracion, EF compara ese modelo con el actual para calcular las diferencias. La instantanea no inspecciona las tablas reales de cada equipo ni prueba que la migracion haya sido aplicada.

Se versionan juntos el archivo principal, su Designer y la instantanea. Un cambio del modelo se expresa en entidades/configuraciones y despues se genera la migracion correspondiente; mantener los tres archivos coherentes permite seguir evolucionando el esquema.

### Generar y aplicar desde Visual Studio

La consola de paquetes utiliza `Bookify.Infrastructure` como destino y `Bookify.Api` como proyecto de inicio. Ya existe `Initial_Database`; para un cambio posterior, usar un nombre nuevo y representativo:

```powershell
Add-Migration NombreDelCambio -Project Bookify.Infrastructure -StartupProject Bookify.Api
```

Este comando genera archivos y actualiza el Snapshot. No aplica el esquema. Para aplicar las migraciones existentes a PostgreSQL desde Windows, primero configurar la cadena con el host publicado:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=bookify;Username=postgres;Password=postgres"
Update-Database -Project Bookify.Infrastructure -StartupProject Bookify.Api
```

Las credenciales corresponden al Compose de desarrollo actual. La variable afecta a los procesos iniciados desde esa sesion; no cambia la cadena guardada para ejecutar dentro del contenedor.

### Uso de la CLI

Con la herramienta `dotnet ef` instalada, ejecutar desde la carpeta `Bookify.Infrastructure`:

```powershell
dotnet ef migrations list --startup-project ../Bookify.Api/Bookify.Api.csproj --no-connect
dotnet ef migrations add NombreDelCambio --startup-project ../Bookify.Api/Bookify.Api.csproj --output-dir Migrations
dotnet ef migrations script --startup-project ../Bookify.Api/Bookify.Api.csproj
```

`list --no-connect` enumera las migraciones del proyecto sin consultar cuales estan aplicadas en la base. `add` genera codigo, y `script` muestra el SQL para su revision. Para aplicar desde Windows, con PostgreSQL disponible y la cadena local configurada:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=bookify;Username=postgres;Password=postgres"
dotnet ef database update --startup-project ../Bookify.Api/Bookify.Api.csproj
```

Al generar migraciones, las herramientas validan el modelo de EF; eso no ejecuta las consultas de Dapper ni comprueba las reservas concurrentes. No es necesario volver a generar `Initial_Database` para aplicarla.

### Aplicacion automatica y limitaciones del esquema

`Bookify.Api` llama a `ApplyMigration()` exclusivamente desde su rama Development. Crea un scope, resuelve `ApplicationDbContext` y ejecuta `Database.Migrate()` antes de atender HTTP. Requiere una conexion utilizable y no tiene reintentos propios. En otros entornos el host no aplica migraciones automaticamente. El metodo no agrega datos iniciales ni llama a las fabricas de dominio.

`Address` sigue siendo un dependiente opcional con sus cinco columnas nullable. Si todas son `NULL`, EF puede no crear una instancia al leer, lo que explica el aviso de dependiente opcional con tabla compartida. Marcar la navegacion con `IsRequired()` y exigir datos en cada campo son decisiones distintas; ninguna de esas nuevas restricciones se ha agregado en esta migracion.

Ademas de la direccion, muchos campos de referencia e importes owned aceptan `NULL` porque Domain tiene nullable deshabilitado y faltan restricciones explicitas. Los constructores privados solucionan la construccion del modelo, pero no corrigen esas reglas de obligatoriedad. Tampoco hay restricciones de base de datos de longitud exacta del nombre, rango de puntuacion o exclusion de reservas solapadas.

El esquema inicial confirma diferencias con Application: las tablas mantienen mayusculas y el recargo usa `amenities_up_change_*`. Las queries usan tablas en minusculas sin comillas y GetBooking usa `amenities_up_charge_*`. Las monedas, fechas e importe del DTO `BookingResponse` ya estan corregidos; permanecen las diferencias del SQL y del parametro `@BookingId`.

## Repositories

Esta carpeta contiene implementaciones EF Core de repositorios definidos en Domain.

### Repository<TEntity>

Clase base generica para repositorios de entidades.

Restriccion:

```csharp
where TEntity : Entity
```

Responsabilidades:

- Guardar el `ApplicationDbContext`.
- Buscar entidades por `Id`.
- Agregar entidades al contexto.

Metodo de busqueda:

```csharp
GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
```

Internamente usa:

```csharp
applicationDbContext
    .Set<TEntity>()
    .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
```

Metodo de agregado:

```csharp
Add(TEntity entity)
```

Este metodo marca la entidad para ser insertada cuando se llame a `SaveChangesAsync`.

`GetByIdAsync` usa una consulta con seguimiento por defecto: no incluye `AsNoTracking`. Si se cambia una entidad cargada y se guarda con la misma unidad de trabajo, EF puede detectar los cambios sin un metodo `Update` del repositorio. Un identificador ausente produce `null`; el repositorio no decide el error de negocio.

`Add` no devuelve un resultado ni confirma una transaccion. La decision de cuando guardar corresponde al caso de uso de Application.

### ApartmentRepository

Implementa `IApartmentRepository`.

Hereda de:

```csharp
Repository<Apartment>
```

No agrega metodos propios porque actualmente solo necesita `GetByIdAsync`.

### UserRepository

Implementa `IUserRepository`.

Hereda de:

```csharp
Repository<User>
```

No agrega metodos propios porque actualmente necesita:

- `GetByIdAsync`
- `Add`

Ambos vienen de `Repository<TEntity>`.

### BookingRepository

Implementa `IBookingRepository`.

Hereda de:

```csharp
Repository<Booking>
```

Ademas implementa:

```csharp
IsOverlappingAsync(Apartment apartment, DateRange duration, CancellationToken cancellationToken = default)
```

Este metodo consulta si ya existe una reserva para el mismo apartamento y con fechas solapadas.

Estados considerados activos:

- `Reserved`
- `Confirmed`
- `Completed`

Condicion de solapamiento:

```csharp
booking.Duration.Start <= duration.End
&& booking.Duration.End >= duration.Start
```

Si existe al menos una booking que cumple esa condicion, devuelve `true`.

Este metodo lo usa `ReserveBookingCommandHandler` antes de crear una nueva reserva.

Se ejecuta con `AnyAsync`, que busca existencia sin necesitar materializar una lista de reservas. Se filtra por apartamento, fechas y estados antes de obtener el booleano, y se propaga el token de cancelacion.

Los extremos son inclusivos: si una reserva termina en la fecha de inicio de otra, se considera solapamiento. `Rejected` y `Cancelled` no bloquean; `Completed` si cuenta en el periodo consultado. La regla se repite en `SearchApartmentsQueryHandler`, por lo que cambiar fechas o estados requiere revisar ambas implementaciones.

## Data

Esta carpeta contiene piezas para acceso SQL directo con Dapper.

### SqlConnectionFactory

Implementa `ISqlConnectionFactory`.

Responsabilidades:

- Guardar la cadena de conexion.
- Crear una `NpgsqlConnection`.
- Abrir la conexion.
- Devolverla como `IDbConnection`.

Uso:

```csharp
using IDbConnection connection = sqlConnectionFactory.CreateConnection();
```

Los query handlers de Application reciben `ISqlConnectionFactory` y no dependen directamente de `NpgsqlConnection`.

Aunque la fabrica es singleton, cada `CreateConnection` construye una conexion distinta. La apertura con `Open()` es sincronica. El `using` del consumidor dispone esa conexion al terminar, tambien si la query falla. No se comparte automaticamente ni conexion ni transaccion con EF Core.

### DateOnlyTypeHandler

`DateOnlyTypeHandler` permite a Dapper convertir entre `DateOnly` y tipos de base de datos.

Hereda de:

```csharp
SqlMapper.TypeHandler<DateOnly>
```

Metodos:

- `Parse`: convierte un `DateTime` recibido desde la base de datos a `DateOnly`.
- `SetValue`: configura parametros SQL como `DbType.Date`.

Se registra en `DependencyInjection`:

```csharp
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
```

La implementacion concreta de `Parse` hace un cast a `DateTime` y despues llama a `DateOnly.FromDateTime`. No contempla que `value` sea ya un `DateOnly` u otro tipo; la compatibilidad debe comprobarse con los valores que entregue el proveedor usado. `SetValue` establece `DbType.Date` y asigna el propio `DateOnly` al parametro. El registro del handler no verifica por si solo todas las conversiones de fechas de las queries.

## Clock

### DateTimeProvider

Implementa `IDateTimeProvider`.

```csharp
public DateTime UtcNow => DateTime.UtcNow;
```

Su funcion es aislar el uso del reloj del sistema.

Application lo usa para obtener la fecha actual sin depender directamente de `DateTime.UtcNow`.

Esto facilita pruebas porque se puede reemplazar por una implementacion falsa o controlada.

## Email

### EmailService

Implementa `IEmailService`.

Metodo:

```csharp
Task SendAsync(Email recipient, string subject, string body)
```

Actualmente es una implementacion vacia:

```csharp
return Task.CompletedTask;
```

Sirve como placeholder. El contrato ya existe y Application ya puede depender de el, pero todavia no envia emails reales.

En el futuro podria reemplazarse por:

- SMTP.
- SendGrid.
- Mailgun.
- Amazon SES.
- Otro proveedor.

## Flujo de escritura con EF Core

Ejemplo con reserva:

```text
ReserveBookingCommandHandler
  -> userRepository.GetByIdAsync
  -> apartmentRepository.GetByIdAsync
  -> bookingRepository.IsOverlappingAsync
  -> Booking.Reserve
  -> bookingRepository.Add
  -> unitOfWork.SaveChangesAsync
  -> ApplicationDbContext.SaveChangesAsync
  -> PublishDomainEventAsync
```

EF Core se usa para trabajar con entidades completas del dominio y guardar cambios.

## Flujo de lectura con Dapper

Ejemplo con busqueda de apartamentos:

```text
SearchApartmentsQueryHandler
  -> ISqlConnectionFactory.CreateConnection
  -> SqlConnectionFactory crea NpgsqlConnection
  -> Dapper ejecuta SQL
  -> Dapper mapea DTOs
  -> Result<IReadOnlyList<ApartmentResponse>>
```

Dapper se usa en lecturas para ejecutar SQL directo y devolver DTOs sin cargar agregados completos.

## Concurrencia

La configuracion de `Apartment` incluye una propiedad sombra `Version` como row version:

```csharp
builder.Property<uint>("Version").IsRowVersion();
```

Esto apunta a concurrencia optimista. Si dos procesos intentan modificar el mismo apartamento con una version antigua, EF Core puede detectar el conflicto al guardar.

En este dominio es relevante porque `Booking.Reserve` modifica:

```csharp
apartment.LastBookedOnUTC = utcNow;
```

Por tanto, al reservar, no solo se inserta una booking; tambien se actualiza el apartamento.

### Flujo de dos solicitudes

1. Dos scopes cargan el mismo apartamento con su version original.
2. Ambos pueden consultar disponibilidad antes de que el otro guarde y obtener `false` para solapamiento.
3. Cada uno crea una reserva y cambia `LastBookedOnUTC` en su apartamento seguido.
4. Al guardar la primera actualizacion se modifica la version persistida del apartamento.
5. Si la segunda escritura intenta actualizar con la version anterior, EF detecta que no se ha actualizado la fila esperada y lanza `DbUpdateConcurrencyException`.
6. `ApplicationDbContext` la traduce a `ConcurrencyException`.
7. `ReserveBookingCommandHandler` la captura y devuelve `Result.Failure<Guid>(BookingErrors.Overlap)`.

La insercion y actualizacion pendientes se guardan en una misma llamada a `SaveChangesAsync`. En la transaccion habitual de ese guardado relacional, un fallo revierte los cambios de esa llamada. Los eventos se publican despues y no participan de esa transaccion de base de datos.

### Alcance de la proteccion

- Requiere el esquema correcto, seguimiento del apartamento y una actualizacion efectiva sobre el. Si `LastBookedOnUTC` queda igual que su valor original, no debe darse por hecho que EF emitira un `UPDATE` ni verificara ese token.
- El token protege una fila de apartamento, no calcula si dos intervalos se solapan. Dos escrituras para fechas diferentes del mismo apartamento tambien pueden entrar en conflicto.
- Un camino que solo inserte `Booking`, por ejemplo SQL directo, no queda protegido por el token de `Apartment`.
- No existe aqui una restriccion de exclusion de intervalos ni una politica de reintentos. La consulta previa por si sola sigue sin ser una garantia atomica.
- Despues de un conflicto no se implementa recarga, limpieza del seguimiento ni reintento del contexto. Un reintento futuro deberia reevaluar disponibilidad con datos actuales.

La traduccion de excepciones ya esta conectada; las garantias de concurrencia y el esquema requieren verificarse con una base de datos real.

## Puntos a revisar

- Existe `20260909102321_Initial_Database`, su Designer y el Snapshot. Las tablas conservan mayusculas, hay numerosos campos nullable y `Address` sigue siendo opcional; la generacion de la migracion no prueba su aplicacion en una base concreta.
- `EmailService` no envia emails reales todavia.
- `ApplicationDbContext` publica eventos despues de guardar. Si un handler de evento falla, los datos ya fueron persistidos. Para escenarios criticos podria evaluarse un outbox pattern.
- `BookingRepository.IsOverlappingAsync` reduce el riesgo de reservas solapadas, pero por si solo no garantiza atomicidad ante concurrencia. La concurrencia optimista sobre `Apartment` ayuda, aunque para maxima robustez conviene apoyarse tambien en restricciones de base de datos.
- El nombre de la cadena de conexion es `DataBase`. Conviene mantenerlo consistente en los archivos de configuracion.
- Las consultas SQL viven en Application. El DTO ya tiene corregidos importes, monedas y alias de fechas; quedan pendientes el parametro de `GetBooking`, los nombres de tabla y `AmenitiesUpChange`/`AmenitiesUpCharge`.
- Las fabricas de `Name` y `Rating` pueden rechazar datos al materializarlos. Tener una configuracion EF y una compilacion correcta no demuestra que todos los datos existentes sean validos ni que las entidades se materialicen correctamente.
- `DateOnlyTypeHandler.Parse` presupone un `DateTime`; conviene probar el contrato con el proveedor configurado.
- Api ya conecta las capas y aplica migraciones en Development. No hay proyectos de pruebas de integracion; la materializacion real, el SQL y los conflictos concurrentes requieren comprobaciones contra PostgreSQL.

## Resumen

`Bookify.Infrastructure` contiene las implementaciones tecnicas que permiten ejecutar la aplicacion: EF Core para escrituras, Dapper para lecturas, PostgreSQL como proveedor, repositorios para acceder a entidades, conversiones para objetos de valor, reloj del sistema y servicio de email.

Esta capa se ha separado para que Domain y Application puedan mantenerse enfocadas en reglas y casos de uso, sin depender directamente de proveedores externos.
