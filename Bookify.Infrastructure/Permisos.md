# Permisos y roles en Bookify

Este documento explica el sistema de permisos local de Bookify. Su finalidad es decidir si un usuario autenticado puede realizar una accion funcional concreta, por ejemplo leer usuarios. No sustituye a Keycloak: Keycloak autentica, emite el JWT y proporciona el identificador externo; Bookify mantiene sus roles y permisos en PostgreSQL.

## Conceptos

| Concepto | Responsabilidad | Ejemplo |
| --- | --- | --- |
| Usuario | Identidad de negocio de Bookify, vinculada a una identidad de Keycloak. | Un usuario con `IdentityId` igual al `sub` del JWT. |
| Rol | Agrupa permisos reutilizables. | `Registered`. |
| Permiso | Representa una accion de negocio autorizable. | `users:read`. |
| Asignacion de rol | Relaciona usuarios y roles. | Un usuario tiene `Registered`. |
| Asignacion de permiso | Relaciona roles y permisos. | `Registered` tiene `users:read`. |

El permiso expresa que se puede hacer y el rol expresa quien recibe ese conjunto de permisos. No se deben usar nombres de controladores, rutas HTTP o pantallas como permisos; deben describir acciones estables del negocio.

## Modelo de dominio

Las clases se encuentran en `Bookify.Domain/Users`.

| Clase | Responsabilidad | Miembros relevantes |
| --- | --- | --- |
| `Permission` | Entidad catalogo de permisos. | `Id`, `Name` y la semilla `Permission.UserRead`. |
| `Role` | Entidad catalogo de roles. | `Id`, `Name`, `Users` y `Permissions`. `Permissions` es la navegacion hacia los permisos del rol. |
| `RolePermission` | Entidad de union de la relacion muchos-a-muchos. | `RoleId` y `PermissionId`, que forman la clave compuesta. |
| `User` | Expone los roles locales del usuario. | `Roles` permite llegar desde el usuario a los permisos de sus roles. |

El catalogo inicial define `Permission.UserRead` con identificador `1` y nombre `users:read`. Los nombres usados en atributos se centralizan en `Bookify.Api/Controllers/Constants/PermissionsConstants.cs`; actualmente contiene `UsersRead`. La constante y el valor de `Permission.Name` deben ser exactamente iguales.

## Persistencia con EF Core

`PermissionConfiguration` mapea `Permission` a `permissions`, define `Id` como clave y marca `Name` como obligatorio. Esto impide `NULL`, pero no impide una cadena vacia ni nombres duplicados: no hay una validacion de contenido ni un indice unico sobre `Name`.

`RoleConfiguration` configura dos relaciones distintas:

1. `Role` y `User` tienen una relacion muchos-a-muchos para asignar roles a usuarios.
2. `Role` y `Permission` tienen otra relacion muchos-a-muchos. `UsingEntity<RolePermission>()` indica que `RolePermission` es la tabla de union de esta segunda relacion.

`RolePermissionConfiguration` da el nombre `role_permissions`, define la clave `{ RoleId, PermissionId }` y siembra la asignacion inicial entre `Registered` y `users:read`.

La migracion `20260917104130_Add_Permission_Tables` crea:

| Tabla | Contenido |
| --- | --- |
| `permissions` | Catalogo de permisos: `id` y `name`. |
| `role_permissions` | Relacion entre roles y permisos: `role_id`, `permission_id`, clave compuesta e integridad referencial hacia `roles` y `permissions`. |

La tabla `permissions` no contiene `role_id`: un mismo permiso puede pertenecer a varios roles. La relacion vive exclusivamente en `role_permissions`.

## Flujo de autorizacion por permiso

El siguiente diagrama describe el flujo actual. El handler todavia no compara el nombre solicitado; la comprobacion exacta es una correccion pendiente.

```text
Cliente con JWT
  -> JwtBearer valida el token
  -> [HasPermission("users:read")] solicita una policy llamada "users:read"
  -> PermissionAuthorizationPolicyProvider crea la policy dinamicamente
  -> PermissionRequirement conserva el permiso solicitado
  -> PermissionAuthorizationHandler consulta los permisos locales del usuario
  -> si la coleccion consultada no esta vacia, el handler satisface el requisito
  -> ASP.NET Core permite la accion si tambien se cumplen los demas requisitos
```

Las clases de `Bookify.Infrastructure/Authorization` intervienen asi:

| Clase | Funcion |
| --- | --- |
| `HasPermissionAttribute` | Atributo reutilizable para los endpoints. Hereda de `AuthorizeAttribute` y usa el nombre del permiso como nombre de policy. |
| `PermissionRequirement` | Requisito de ASP.NET Core que transporta el nombre del permiso que debe comprobarse. |
| `PermissionAuthorizationPolicyProvider` | Crea bajo demanda una policy para el nombre recibido. Permite agregar permisos sin registrar una policy manual por cada uno. |
| `PermissionAuthorizationHandler` | Ejecuta la comprobacion durante la autorizacion de la solicitud. |
| `AuthorizationService` | Consulta EF Core para obtener los permisos que llegan al usuario a traves de sus roles. |

Estas clases se registran desde `DependencyInjection.AddAuthorization`: el proveedor de policies y el handler son transitorios; `AuthorizationService` es scoped porque depende de `ApplicationDbContext`.

El proveedor busca primero una policy registrada. Si no existe, interpreta su nombre como permiso, crea el requisito y guarda la policy en `AuthorizationOptions`. No verifica que ese permiso exista en el catalogo. Una errata en el atributo tambien crea una policy; con la comprobacion actual de `Count > 0`, incluso esa policy podria satisfacerse.

El handler comprueba `IsAuthenticated`, crea un scope, lee `IdentityId` y consulta los permisos. Si no llama a `context.Succeed(requirement)`, el requisito queda sin satisfacer. No lanza un rechazo explicito mediante `context.Fail()`.

Este es el uso real en `UsersController.LogInUser`, que ademas hereda `[Authorize]` del controlador:

```csharp
[Authorize(Roles = RolesConstants.Registered)]
[HasPermission(PermissionsConstants.UsersRead)]
[HttpGet("LogInUser")]
public async Task<IActionResult> LogInUser(CancellationToken cancellationToken)
{
    GetLoggedInUserQuery query = new();
    Result<UserResponse> result = await sender.Send(query, cancellationToken);
    return Ok(result.Value);
}
```

`[Authorize]` exige un JWT valido. `HasPermission` anade la comprobacion funcional. Tambien se puede combinar con `[Authorize(Roles = RolesConstants.Registered)]` cuando la accion exija ambos requisitos, como ocurre en `UsersController.LogInUser`.

## Como agregar un permiso nuevo

1. Crear una constante estable en `PermissionsConstants`, por ejemplo `public const string ApartmentsUpdate = "apartments:update";`.
2. Crear la semilla equivalente en `Permission`, con un identificador unico y el mismo nombre.
3. Incluir el nuevo `Permission` en `PermissionConfiguration.HasData(...)` y agregar la asignacion en `RolePermissionConfiguration.HasData(...)`. Declarar solamente un campo estatico en `Permission` no inserta la fila.
4. Generar y aplicar una migracion si cambian los datos semilla o el modelo.
5. Anotar el endpoint con `[HasPermission(PermissionsConstants.ApartmentsUpdate)]`.
6. Corregir primero las limitaciones descritas a continuacion y probar: sin JWT (401), JWT sin el permiso solicitado (403) y JWT con ese permiso (respuesta propia de la accion). Incluir un usuario con un permiso distinto y otro con varios roles.

Para permisos administrables desde interfaz se puede sustituir parte de las semillas por comandos de Application que creen permisos y asignaciones. La regla sigue siendo la misma: el nombre usado por la policy debe existir y estar asignado a algun rol.

## Comprobaciones y limites actuales

- El handler debe comprobar `permissions.Contains(requirement.Permission)`. Actualmente `permissions.Count > 0` satisface cualquier requisito dinamico si la coleccion devuelta tiene algun permiso; los otros requisitos del endpoint, como el rol, siguen siendo obligatorios.
- `GetPermissionsForUserAsync` usa `FirstAsync()` despues de obtener colecciones de permisos por rol. Solo toma la primera coleccion, sin un orden definido, y el `HashSet` elimina duplicados exclusivamente dentro de ella. La consulta debe aplanar todos los roles y sus permisos antes de construir el conjunto.
- Si no hay usuario o no tiene roles, esa consulta no obtiene ninguna coleccion y `FirstAsync()` lanza. Si hay un rol sin permisos, su coleccion es vacia y el requisito no se satisface. Son situaciones diferentes; la excepcion termina en `500` mediante el middleware actual.
- No hay cache de los permisos del usuario. Las policies si se almacenan en `AuthorizationOptions`. Cada comprobacion autenticada abre un scope y consulta la base de datos.
- Un permiso global no determina si el usuario puede actuar sobre una fila concreta. Para ese caso se necesita autorizacion basada en recursos; vease `Recursos.md`.
- Los permisos locales no se sincronizan automaticamente con roles de Keycloak. La fuente de verdad de estas autorizaciones es PostgreSQL.

## Seguridad operativa

No se deben exponer tokens, contrasenas, secretos de cliente o cabeceras `Authorization` en datos semilla, documentos ni registros. Los cambios de permisos y roles deben revisarse como cambios de seguridad: conceder un permiso nuevo puede abrir capacidades de negocio a usuarios existentes.
