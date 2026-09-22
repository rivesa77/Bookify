# Autorizacion basada en recursos

Este documento describe la autorizacion basada en recursos implementada en el commit `dea2a2ea95a870f3cdcd13cf8f259398df9eb512`. El recurso protegido es una reserva (`Booking`) y la regla es simple: un usuario solo puede obtener una reserva cuya propiedad `UserId` coincide con su identificador local de Bookify.

La autorizacion se realiza dentro del caso de uso de Application, cuando ya se ha recuperado el recurso solicitado. La query recibe solo el id de reserva; el identificador del usuario se obtiene del contexto de la solicitud. La regla queda centralizada en el handler para todos sus invocadores, siempre que proporcionen un contexto de usuario fiable.

El commit modifica cuatro archivos: agrega `UserId` a `IUserContext`, implementa su lectura en `ClaimsPrincipalExtensions` y `UserContext`, e incorpora la comprobacion en `GetBookingQueryHandler`. La transformacion de claims y el controlador ya existian: forman parte del flujo, pero no fueron introducidos por ese commit. La persistencia de roles y permisos se explica en [Roles.md](Roles.md) y [Permisos.md](Permisos.md).

## Que es un recurso

Un recurso es una instancia concreta del dominio sobre la que se solicita una accion. En este caso:

| Elemento | Valor |
| --- | --- |
| Recurso | Una fila de la tabla `bookings`, representada por `BookingResponse`. |
| Identificador del recurso | `BookingId`, recibido en la ruta `GET /api/bookings/{id}`. |
| Propietario | El campo `booking.UserId`. |
| Actor | El usuario autenticado, expuesto como `IUserContext.UserId`. |
| Regla | El actor puede leer el recurso solo si ambos identificadores son iguales. |

La diferencia con una autorizacion por rol es importante. Un rol permite capacidades generales, mientras que esta regla decide si el usuario puede acceder a una reserva especifica. Dos usuarios pueden tener el mismo rol y, aun asi, cada uno solo puede consultar sus propias reservas.

## Flujo completo

```text
JWT valido
  -> JwtBearer construye el ClaimsPrincipal
  -> CustomClaimsTransformation obtiene el usuario local de Bookify
  -> se anade el id local de Bookify como claim sub
  -> UserContext lee ese id y lo expone mediante IUserContext.UserId
  -> GetBookingQueryHandler carga la reserva solicitada
  -> compara booking.UserId con userContext.UserId
  -> coincide: devuelve la reserva; no coincide o no existe: devuelve Booking.NotFound
```

Con un contexto autenticado valido, tanto una reserva inexistente como una reserva ajena producen `404 Not Found`. No hay una excepcion de acceso para administradores ni una comprobacion de permisos globales en este handler. El comportamiento sin autenticacion tiene una limitacion distinta, explicada al final.

## Clases e interfaces

### `IUserContext`

Ubicacion: `Bookify.Application/Abstractions/Authentication/IUserContext.cs`.

Es la abstraccion que permite a Application conocer al usuario de la solicitud sin depender de `HttpContext`, JWT ni ASP.NET Core. Expone dos identificadores con responsabilidades diferentes:

| Propiedad | Tipo | Origen y uso |
| --- | --- | --- |
| `IdentityId` | `string` | Identificador externo emitido por Keycloak. Se usa para localizar el usuario local asociado a la identidad. |
| `UserId` | `Guid` | Identificador local del usuario de Bookify. Se usa para comparar la propiedad de una reserva, review u otro recurso. |

Application depende de la interfaz; Infrastructure entrega la implementacion real. Esta separacion mantiene las capas de casos de uso libres de dependencias HTTP.

### `ClaimsPrincipalExtensions`

Ubicacion: `Bookify.Infrastructure/Authentication/Extensions/ClaimsPrincipalExtensions.cs`.

Contiene dos extensiones sobre `ClaimsPrincipal`:

| Metodo | Funcion |
| --- | --- |
| `GetIdentityId()` | Obtiene `ClaimTypes.NameIdentifier`, que identifica la cuenta externa procedente del token. |
| `GetUserId()` | Busca el primer claim literal `sub` mediante `FindFirstValue`, intenta convertirlo a `Guid` y devuelve el valor. El flujo espera que sea el id local agregado por la transformacion. Si falta o no es un GUID valido, lanza `ApplicationException`. |

`GetUserId()` lee el principal de la solicitud; por si mismo no comprueba `IsAuthenticated`, el emisor del claim ni su procedencia. La autenticacion del endpoint y el correcto enriquecimiento del principal son precondiciones para confiar en el identificador.

### `CustomClaimsTransformation`

Ubicacion: `Bookify.Infrastructure/Authorization/CustomClaimsTransformation.cs`.

Tras validar el JWT, esta clase usa `IdentityId` para buscar el `User` local y agrega al principal un claim `sub` con el `Guid` local (`UserRolesResponse.Id`). De este modo, el contexto puede trabajar con `users.id` y `bookings.user_id`. Se modifica el `ClaimsPrincipal` en memoria, no el token JWT firmado ni la cuenta de Keycloak.

El flujo espera que el `sub` externo se haya mapeado a `ClaimTypes.NameIdentifier` al validar el token. `GetIdentityId()` lee ese claim para buscar `User.IdentityId`; el `sub` literal agregado despues contiene `User.Id`. Tener formato GUID no hace equivalentes esos identificadores.

La transformacion omite la consulta si ya existen un claim de rol y un `sub` literal. Esa condicion no verifica que dicho `sub` sea local. Por ello, los mappers de claims y esa condicion de salida forman parte de las precondiciones de este mecanismo.

La transformacion se registra como `IClaimsTransformation` en `DependencyInjection.AddAuthorization`, por lo que se ejecuta durante la autenticacion antes de que el caso de uso consulte `IUserContext`.

### `UserContext`

Ubicacion: `Bookify.Infrastructure/Authentication/UserContext.cs`.

Implementa `IUserContext`. Recibe `IHttpContextAccessor`, accede al `ClaimsPrincipal` de la solicitud y delega la lectura de ambos identificadores en las extensiones anteriores. Se registra como scoped en `DependencyInjection.AddAuthentication`:

```csharp
services.AddScoped<IUserContext, UserContext>();
```

El contexto se crea para cada solicitud y no debe almacenarse ni reutilizarse fuera de ella.

### `GetBookingQueryHandler`

Ubicacion: `Bookify.Application/Bookings/GetBooking/GetBookingQueryHandler.cs`.

Es el caso de uso que aplica la regla de recurso. Recibe `ISqlConnectionFactory` para consultar con Dapper e `IUserContext` para conocer al actor de la solicitud. La consulta incluye `user_id AS UserId` porque ese dato es imprescindible para autorizar la lectura.

La comprobacion central es:

```csharp
if (booking is null || booking.UserId != userContext.UserId)
{
    return Result.Failure<BookingResponse>(BookingErrors.NotFound);
}
```

El handler no recibe `UserId` en `GetBookingQuery`; la query solo contiene `BookingId`. Esto impide que la API declare artificialmente como propietario a quien hace la llamada.

El SQL filtra solo por `id = @BookingId`: la fila se materializa antes de comprobar la propiedad. `BookingResponse` contiene el `UserId` usado en la comparacion y los datos devueltos al cliente (apartamento, estado, precios, fechas y creacion). `GetBookingQuery` implementa `IQuery<BookingResponse>` y el handler devuelve `Result<BookingResponse>` para representar exito o fallo.

### `BookingsController` y `BookingErrors`

`BookingsController.GetBooking` crea `GetBookingQuery` a partir del id de ruta y convierte un resultado correcto en `200 OK`. Si el handler devuelve error, responde con `NotFound()`.

`BookingErrors.NotFound`, definido en `Bookify.Domain/Bookings/BookingErrors.cs`, representa tanto la ausencia real de la reserva como la falta de acceso al recurso. La API no distingue ambos casos por motivos de confidencialidad.

## Ejemplo de ejecucion

Supongamos estas reservas:

| Reserva | `booking.UserId` |
| --- | --- |
| `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` | `11111111-1111-1111-1111-111111111111` |
| `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` | `22222222-2222-2222-2222-222222222222` |

Si el principal enriquecido corresponde al usuario local `11111111-1111-1111-1111-111111111111` y no existe la reserva `cccccccc-cccc-cccc-cccc-cccccccccccc`:

| Solicitud | Resultado |
| --- | --- |
| `GET /api/bookings/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` | `200 OK` con la reserva propia. |
| `GET /api/bookings/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` | `404 Not Found`, reserva ajena. |
| `GET /api/bookings/cccccccc-cccc-cccc-cccc-cccccccccccc` | `404 Not Found`, reserva inexistente. |

Los dos ultimos casos devuelven la misma respuesta HTTP. Una ruta como `/api/bookings/A` no sirve para probar la propiedad: `id` se enlaza a un `Guid` y, con `[ApiController]`, ese formato invalido produce `400` antes de ejecutar el handler.

## Como extender el patron

Las siguientes son posibles extensiones, no operaciones protegidas por este commit:

| Recurso | Identificador de propiedad que comparar | Ejemplo de regla |
| --- | --- | --- |
| `Review` | `Review.UserId` | Solo el autor puede editar o eliminar su review. |
| `Apartment` | Identificador del propietario que se modele en el apartamento | Solo el propietario puede actualizarlo o retirarlo. |
| `Booking` | `Booking.UserId` | Solo el huesped puede consultar o cancelar su reserva, sujeto tambien al estado de negocio. |

Para cada caso, el handler de Application debe cargar el recurso, obtener `IUserContext.UserId` y devolver un error de negocio adecuado si la propiedad no coincide. Las reglas de estado, por ejemplo impedir cancelar una reserva ya iniciada, deben seguir aplicandose en las entidades o comandos de dominio; la autorizacion por recurso no las sustituye.

## Requisitos y limites actuales

- `BookingsController` no tiene `[Authorize]` y el proyecto no configura una policy global que lo proteja. Sin JWT, una reserva inexistente devuelve `404` por el cortocircuito de `booking is null`; una existente intenta leer `UserId`, lanza y el middleware devuelve `500`. Esta diferencia permite inferir existencia. Requerir autenticacion antes del handler es una correccion pendiente, no un comportamiento ya implementado.
- `CustomClaimsTransformation` agrega un claim `sub` local. Si hay otro `sub` anterior, `GetUserId()` puede leerlo aunque sea un GUID valido y comparar el identificador externo con una clave local. Un tipo de claim propio, como `bookify_user_id`, evitaria esa ambiguedad.
- Si el JWT es valido pero no existe un `User` local asociado, la transformacion actual usa `FirstAsync()` y falla. Conviene convertir esa situacion en una respuesta controlada.
- La lectura comprueba la relacion almacenada `bookings.user_id`. El POST actual recibe `UserId` del cliente; este commit no cambia la creacion para obtenerlo de `IUserContext`. Por tanto, "reserva propia" significa asociada al usuario en la base de datos, no una garantia de que ese usuario haya enviado la solicitud de creacion.
- Crear, actualizar o cancelar una reserva necesita la comprobacion apropiada en cada caso de uso. Un cambio de rol tampoco evita la comparacion estricta de propiedad de esta query.

## Verificacion del mecanismo

GetBookingTests en Bookify.Application.Tests ya sustituye IUserContext y comprueba reserva propia (exito), ajena (BookingErrors.NotFound) e inexistente (el mismo fallo), ademas del mapeo y los parametros Dapper con conexion simulada. La bateria Application pasa 138 casos el 22/09/2026. Usar GUID distintos para identidad externa y usuario local evita confundirlos.

Api.Tests comprueba rutas, model binding, Location y respuestas HTTP con ISender/autenticacion simulados: no demuestra la propiedad real del recurso ni ejecuta la transformacion PostgreSQL. Faltan pruebas de extremo a extremo con JWT, claims locales ausentes y usuario sin registro local. Al corregir la proteccion del endpoint, las solicitudes sin JWT o con JWT invalido deberian dar 401 independientemente de si existe la reserva. Invocar directamente el controlador no verifica esa proteccion. El estado actual de arranque de esas pruebas se documenta en [Api.Tests](../Tests/Bookify.Api.Tests/readme.md).
