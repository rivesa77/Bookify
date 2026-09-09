# Bookify

Bookify es una solucion .NET 10 para reservar apartamentos, organizada en Domain, Application, Infrastructure y Api. El host actual es `Bookify.Api`: registra las capas, expone controladores HTTP y, en Development, publica OpenAPI y Swagger UI y aplica las migraciones de PostgreSQL al arrancar.

## Guias por capa

| Guia | Contenido |
| --- | --- |
| [Domain](Bookify.Domain/readme.md) | Entidades, objetos de valor, reglas, eventos, resultados y constructores de materializacion. |
| [Application](Bookify.Application/readme.md) | Comandos, queries, MediatR, validacion, DTO, contratos y limitaciones actuales del SQL. |
| [Infrastructure](Bookify.Infrastructure/readme.md) | EF Core, repositorios, mapeos, migracion inicial, concurrencia y servicios externos. |
| [Api](Bookify.Api/readme.md) | Cada archivo del host, endpoints, respuestas HTTP, configuracion, arranque y Dockerfile. |

Las guias describen el codigo y las migraciones versionadas. La existencia de estos archivos no demuestra que una base de datos tenga el esquema aplicado ni que todos los endpoints hayan superado pruebas de integracion.

## Arquitectura y dependencias

```text
Bookify.Api -> Bookify.Application -> Bookify.Domain
Bookify.Api -> Bookify.Infrastructure -> Bookify.Application
```

Domain define reglas e interfaces de repositorios y no referencia EF Core. Application orquesta los casos de uso y define contratos externos. Infrastructure implementa persistencia, reloj y correo. Api compone las dependencias y adapta HTTP a mensajes de Application.

Domain utiliza `MediatR.Contracts` para los eventos. Application usa MediatR, FluentValidation y Dapper; sus queries contienen SQL de PostgreSQL, por lo que un cambio de esquema o proveedor exige revisarlas. Infrastructure accede a Domain mediante la referencia transitiva de Application.

Esta separacion permite que las reglas de una reserva no dependan de un controlador ni de un proveedor de correo. Los constructores privados de las entidades permiten reconstruir datos con EF sin exponer una forma publica de crear objetos incompletos.

## Archivos de la raiz

| Archivo | Funcion actual |
| --- | --- |
| [Bookify.slnx](Bookify.slnx) | Agrupa Api, Application, Domain e Infrastructure bajo `/Scr/`, y Docker Compose. `/Tests/` es una carpeta logica sin proyectos de pruebas. |
| [Bookify.csproj](Bookify.csproj) | Proyecto de consola residual, fuera de la solucion y sin referencias a las capas. No es el host actual; ya no existe `Program.cs` en la raiz. |
| [docker-compose.yml](docker-compose.yml) | Define API, PostgreSQL 17, imagen, dependencia, credenciales locales y persistencia de datos. |
| [docker-compose.override.yml](docker-compose.override.yml) | Configuracion local: Development, puertos HTTP/HTTPS y montajes de secretos y certificados de Windows. |
| [docker-compose.dcproj](docker-compose.dcproj) | Integra Compose con las herramientas de contenedores de Visual Studio para Linux. |
| [launchSettings.json](launchSettings.json) | Perfil Docker Compose: inicia `bookify.api` con `StartDebugging` desde Visual Studio. |
| [.dockerignore](.dockerignore) | Excluye del contexto archivos como `bin`, `obj`, `.vs`, `.env` y configuraciones del IDE. |

Las carpetas `bin/` y `obj/` contienen salidas generadas. `obj/Docker` puede contener archivos Compose de depuracion generados por Visual Studio; no son la configuracion fuente que debe editarse.

## Flujo de una solicitud

```text
POST /api/bookings
  -> BookingsController convierte ReserveBookingRequest en ReserveBookingCommand
  -> ISender.Send
  -> LoggingBehavior -> ValidationBehavior
  -> ReserveBookingCommandHandler consulta usuario, apartamento y disponibilidad
  -> Booking.Reserve calcula precios, cambia el apartamento y acumula un evento
  -> IUnitOfWork / ApplicationDbContext guarda los cambios
  -> IPublisher publica BookingReservedDomainEvent
  -> Respuesta HTTP 201 o 400 para fallos de negocio
```

Las lecturas pasan del controlador a la query de MediatR y despues a Dapper mediante `ISqlConnectionFactory`. Devuelven DTO y no materializan entidades EF. Los behaviors actuales solo se aplican a comandos; no envuelven queries ni notificaciones.

Los eventos se publican despues de guardar y forman parte del tiempo de la solicitud. Un fallo del consumidor no deshace el guardado ya realizado. No hay outbox ni entrega duradera; `EmailService` es provisional y no envia correo real.

## Capacidades actuales

| Operacion | Estado |
| --- | --- |
| Buscar apartamentos | `GET /api/apartments?starDate=2026-10-01&endDate=2026-10-05`. El parametro actual se llama literalmente `starDate`. |
| Consultar reserva | `GET /api/bookings/{id}`; query y DTO existentes, con discrepancias SQL pendientes. |
| Reservar | `POST /api/bookings`; comando, validacion, dominio y persistencia conectados. Requiere usuario y apartamento existentes. |
| Confirmar, rechazar, completar y cancelar | Metodos y eventos en Domain; todavia sin comandos ni endpoints. |
| Crear usuario y resena | Fabricas de dominio y mapeos; sin endpoints de alta. |
| Documentacion interactiva | `/swagger/index.html` consume `/openapi/v1.json`, solo en Development. |
| Migraciones | `20260909102321_Initial_Database` en Infrastructure y aplicacion automatica al arrancar en Development. |

`Result` representa fallos de negocio. El controlador de reserva los convierte en `400`; el de consulta devuelve `404` ante un resultado fallido. La excepcion de FluentValidation de Application y otras excepciones tecnicas no tienen un manejador HTTP propio configurado.

## Ejecucion local

Compilar desde la raiz con el SDK de .NET 10:

```powershell
dotnet build Bookify.Api/Bookify.Api.csproj
```

Este proyecto construye tambien las tres capas referenciadas. La solucion completa incluye las herramientas Docker de Visual Studio.

Para ejecutar la API en Windows con PostgreSQL publicado por Docker:

```powershell
docker compose up -d bookify-db
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=bookify;Username=postgres;Password=postgres"
dotnet run --project Bookify.Api/Bookify.Api.csproj --launch-profile http
```

Swagger se abre en [http://localhost:5285/swagger/index.html](http://localhost:5285/swagger/index.html). La variable afecta a los procesos de esa sesion PowerShell. En Windows se usa `localhost`; `bookify-db` es el nombre DNS dentro de la red Compose.

El arranque ejecuta `Database.Migrate()` en Development antes de atender solicitudes. PostgreSQL debe estar disponible; un error de migracion impide terminar el arranque. La guia de Infrastructure explica como generar y aplicar migraciones manualmente.

## Docker Compose

La API se construye con [Bookify.Api/Dockerfile](Bookify.Api/Dockerfile). PostgreSQL usa `postgres:17`, base `bookify` y usuario y clave `postgres`, configurados para desarrollo local. Guarda sus datos en `./containers/database`, montado en `/var/lib/postgresql/data`.

| Servicio | Puerto del equipo | Puerto del contenedor | Uso |
| --- | --- | --- | --- |
| `bookify.api` | 5000 | 8080 | HTTP. |
| `bookify.api` | 5001 | 8081 | HTTPS, requiere certificado accesible y configurado. |
| `bookify-db` | 5432 | 5432 | PostgreSQL desde Windows o pgAdmin. |

Con Docker Desktop activo y los secretos/certificados de desarrollo preparados:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f bookify.api
```

Swagger: [http://localhost:5000/swagger/index.html](http://localhost:5000/swagger/index.html). En Development ya no se aplica `UseHttpsRedirection`, por lo que OpenAPI se puede solicitar por ese mismo puerto HTTP. El override sigue habilitando el listener HTTPS: abrir HTTP no evita un fallo de arranque si Kestrel no puede configurar su certificado.

`depends_on` establece el orden de inicio; el Compose actual no tiene un healthcheck que espere a que PostgreSQL acepte conexiones. Esto puede afectar a `ApplyMigration()` durante el primer arranque. Los montajes de certificados son de solo lectura y no generan ni dan confianza automaticamente a un certificado.

Para pgAdmin ejecutado en Windows: host `localhost`, puerto `5432`, base de mantenimiento `bookify`, usuario `postgres` y clave local `postgres`. Si pgAdmin se ejecuta en otro contenedor, la direccion depende de su red; `localhost` designaria ese otro contenedor.

### Visual Studio y ejecucion independiente

En modo rapido, Visual Studio construye la etapa `base`, monta los binarios y puede mantener `DistrolessHelper.dll --wait`. Docker puede mostrar el contenedor activo mientras la API no esta ejecutandose. Las etiquetas de Visual Studio, el entrypoint y los montajes de depuracion ayudan a identificarlo en Inspect.

Compose desde terminal construye la etapa final y su entrada es `dotnet Bookify.Api.dll`. Usa los nombres fijos `Bookify.Api` y `Bookify.db`; no pueden coexistir otros contenedores con esos nombres creados por un proyecto de Visual Studio diferente. Al cambiar de modo hay que detener y retirar los contenedores anteriores, conservando la carpeta de datos si se quiere mantener la base.

Una imagen final compilada en Release no cambia `ASPNETCORE_ENVIRONMENT`: con el override actual sigue siendo Development. Usar Compose sin Visual Studio no convierte esta configuracion local en un despliegue de produccion.

## Cambios recientes y limites

- `Entity` tiene constructor protegido vacio; `Apartment`, `Booking`, `Review` y `User` tienen constructores privados vacios. EF puede construir el modelo sin enlazar las navegaciones owned al constructor de negocio.
- Existe la migracion inicial y sus archivos Designer y Snapshot. Crear una migracion genera codigo; aplicarla modifica la base de datos.
- `BookingResponse` ya expone `PriceAmount`, codigos de moneda `string` y `DurationStart`/`DurationEnd` alineados con los alias de la query.
- La migracion conserva tablas `Apartments`, `Bookings`, `Reviews` y `Users` con mayusculas, mientras Dapper consulta `apartments` y `bookings` sin comillas. Hay que alinear esos nombres para ejecutar las lecturas sobre ese esquema.
- `GetBookingQueryHandler` sigue enviando `Id` cuando SQL solicita `@BookingId`, y consulta `amenities_up_charge_*` cuando la migracion contiene `amenities_up_change_*`.
- `Address` sigue siendo opcional para EF; sus columnas admiten `NULL`. El constructor vacio no establece obligatoriedad de campos ni navegaciones.
- El conflicto de EF se traduce a `ConcurrencyException` y despues a `BookingErrors.Overlap`. El token sombra del apartamento usa `xmin` y requiere una actualizacion efectiva del apartamento; no protege todas las entidades ni cualquier insercion por otros medios.
- No hay proyectos de pruebas, autenticacion ni autorizacion configuradas. Estas guias no afirman una validacion integral de reservas concurrentes o de las consultas SQL.

Para profundizar, seguir las guias en el orden Domain, Application, Infrastructure y Api. Cada una incluye el inventario de archivos y explica las decisiones y el comportamiento actual de su capa.
