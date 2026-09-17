# Roles y autorizacion en Bookify

Este documento describe la implementacion actual de roles de Bookify. Hay dos conceptos que deben mantenerse separados:

- Keycloak autentica al usuario y emite el JWT. El identificador externo del usuario esta en el claim `sub` del token.
- PostgreSQL almacena el usuario de negocio y sus roles locales. La API consulta esa informacion y anade los roles al `ClaimsPrincipal` de la solicitud.

Por tanto, `Registered` no es actualmente un realm role de Keycloak. Es un rol propio de Bookify, definido y asignado en la base de datos de Bookify.

## Flujo completo

```text
Cliente -> POST /api/users/login -> Keycloak -> JWT con sub externo
Cliente -> endpoint [Authorize] -> JwtBearer valida el JWT
         -> CustomClaimsTransformation busca User.IdentityId = identificador externo
         -> PostgreSQL devuelve los roles locales
         -> principal recibe ClaimTypes.Role y un sub local adicional
         -> [Authorize(Roles = "Registered")] permite o rechaza la accion
```

El `sub` del JWT identifica al usuario externo. El flujo espera que JwtBearer lo mapee a `ClaimTypes.NameIdentifier`; `CustomClaimsTransformation` agrega al principal un claim literal `sub` con el GUID local de Bookify. No modifica el JWT firmado. `IUserContext.IdentityId` lee el identificador externo y `IUserContext.UserId` lee el local. No deben intercambiarse aunque ambos valores tengan formato GUID.

## Rol de dominio

[Role.cs](../Bookify.Domain/Users/Role.cs) define la entidad de rol:

```csharp
public static readonly Role Registered = new(1, "Registered");
```

Sus propiedades son:

| Propiedad | Uso |
| --- | --- |
| `Id` | Clave entera del rol. El valor fijo de `Registered` es `1`. |
| `Name` | Nombre que se coloca en `ClaimTypes.Role` y que usa `[Authorize(Roles = ...)]`. |
| `Users` | Navegacion inversa de la relacion muchos-a-muchos con `User`. |
| `Permissions` | Permisos del rol mediante la tabla de union `role_permissions`. Vease [Permisos.md](Permisos.md). |

[User.cs](../Bookify.Domain/Users/User.cs) contiene una lista privada `roles` y expone `Roles` como `IReadOnlyCollection<Role>`. Durante `User.Create`, el usuario recibe automaticamente `Role.Registered` antes de guardarse. No hay por ahora metodos de dominio para agregar, quitar o reemplazar roles una vez creado el usuario.

## Persistencia y migracion

[RoleConfiguration.cs](Configurations/RoleConfiguration.cs) mapea actualmente `Role` a `roles`, declara su clave y configura las relaciones muchos-a-muchos con `User` y `Permission`. Tambien inserta el rol inicial mediante `HasData(Role.Registered)`.

La migracion [20260916173041_Add_UserRole.cs](Migrations/20260916173041_Add_UserRole.cs) realiza estos cambios:

| Objeto | Contenido |
| --- | --- |
| `Roles` | Tabla de roles con `id` entero y `name` texto. |
| `role_user` | Tabla de union con clave compuesta `roles_id`, `users_id`. |
| `Registered` | Semilla con `id = 1` y `name = "Registered"`. |
| `ix_role_user_users_id` | Indice para consultar los roles de un usuario. |

La tabla anterior describe la migracion original. Posteriormente, `20260917064152_Change_TableName_And_Field` renombra `Roles` a `roles`, `Users` a `users` y `identity` a `identity_id`. La migracion `20260917104130_Add_Permission_Tables` incorpora los permisos y su relacion con roles. Estos son los nombres vigentes del esquema.

Las migraciones deben aplicarse antes de usar la nueva version. En Development, `ApplyMigration()` aplica las pendientes al arrancar la API. En otros entornos deben aplicarse de forma controlada.

[UserRepository.cs](Repositories/UserRepository.cs) adjunta cada rol del usuario al `DbContext` antes de agregar al usuario. Esto evita que EF intente insertar de nuevo el rol semilla `Registered`; solo crea la fila de usuario y su relacion en `role_user`.

Los usuarios existentes no reciben `Registered` de forma retroactiva. Para que participen en la autorizacion deben tener una fila en `role_user` que los relacione con `roles.id = 1`.

## Transformacion de claims

[CustomClaimsTransformation.cs](Authorization/CustomClaimsTransformation.cs) implementa `IClaimsTransformation`. Infrastructure la registra como transitoria desde `AddAuthorization` en [DependencyInjection.cs](DependencyInjection.cs).

Tras autenticar el JWT, la transformacion:

1. Comprueba si el principal ya contiene `ClaimTypes.Role` y un claim literal `sub`. Si ambos existen, no consulta la base de datos.
2. Crea un scope de DI y resuelve `AuthorizationService`.
3. Obtiene el identificador externo con `ClaimsPrincipalExtensions.GetIdentityId()`.
4. Busca en PostgreSQL el `User` cuyo campo `IdentityId` (`users.identity_id`) coincide con ese identificador.
5. Anade un claim `sub` con el GUID local de Bookify.
6. Anade un claim `ClaimTypes.Role` por cada rol local del usuario.

[AuthorizationService.cs](Authorization/AuthorizationService.cs) realiza la consulta EF y devuelve `UserRolesResponse`, que contiene el GUID local y la lista de `Role`. Se registra como scoped para usar el mismo contexto de datos que su scope.

[UserContext.cs](Authentication/UserContext.cs) usa `IHttpContextAccessor` y [ClaimsPrincipalExtensions.cs](Authentication/Extensions/ClaimsPrincipalExtensions.cs) para entregar ambos identificadores a Application: `IdentityId` mediante `GetIdentityId()` y `UserId` mediante `GetUserId()`. La comprobacion de propiedad de reservas usa el segundo; se explica en [Recursos.md](Recursos.md).

## Endpoints que usan roles

[UsersController.cs](../Bookify.Api/Controllers/Users/UsersController.cs) tiene `[Authorize]` a nivel de controlador. Su accion GET `/api/users/LogInUser` agrega:

```csharp
[Authorize(Roles = RolesConstants.Registered)]
[HasPermission(PermissionsConstants.UsersRead)]
```

[RolesConstants.cs](../Bookify.Api/Controllers/Constants/RolesConstants.cs) centraliza la cadena `Registered`, que debe permanecer igual al nombre del rol semilla.

Los requisitos de rol y permiso se combinan: deben satisfacerse ambos. El resultado de la accion tambien depende de las limitaciones actuales de la consulta y del handler descritas en [Permisos.md](Permisos.md):

| Situacion | Resultado habitual |
| --- | --- |
| No hay JWT valido | `401 Unauthorized`. |
| JWT valido, usuario local con `Registered` y comprobacion de permiso satisfecha | La accion puede ejecutarse. |
| JWT valido y requisito de rol o permiso incumplido, sin excepciones durante la evaluacion | `403 Forbidden`. |
| JWT valido, pero no existe `User.IdentityId` local | `FirstAsync()` lanza una excepcion que el middleware convierte en `500`. |
| JWT valido, usuario local sin ningun rol | La consulta de permisos puede lanzar por secuencia vacia y terminar en `500`. |

`POST /api/users/register` y `POST /api/users/login` declaran `[AllowAnonymous]`. Las rutas de apartamentos requieren autenticacion por el atributo del controlador, pero no requieren `Registered` especificamente. Reservas y reviews no tienen atributos de autorizacion en el codigo actual.

## Keycloak y roles administrativos

Keycloak interviene de dos formas distintas:

| Cliente | Funcion en Bookify |
| --- | --- |
| `bookify-auth-client` | Se usa por `JwtService` con `grant_type=password` para obtener el token del usuario al iniciar sesion. |
| `bookify-admin-client` | Se usa por `AdminAuthorizationDelegatingHandler` con `grant_type=client_credentials` para crear identidades durante el registro. |

Para registrar usuarios, la cuenta de servicio de `bookify-admin-client` necesita permisos de administracion en el realm `bookify`. El permiso minimo que corresponde al POST administrativo de usuarios es normalmente el client role `manage-users` del cliente interno `realm-management`. No usar `realm-admin` salvo que exista una necesidad concreta: concede muchas mas capacidades que crear usuarios.

El export [bookify-realm-export.json](../.files/bookify-realm-export.json) contiene los roles internos estandar de Keycloak, incluido `realm-management/manage-users`, y define ambos clientes. Eso no demuestra por si solo que la cuenta de servicio tenga el rol asignado: debe comprobarse en Keycloak bajo Service account roles del cliente administrativo. Los secretos de esos clientes no deben copiarse a este documento ni a codigo versionado.

Los realm roles de Keycloak y el rol local `Registered` no se sincronizan. El token puede incluir roles de Keycloak por sus mappers, pero la autorizacion `Registered` de Bookify procede de PostgreSQL y de la transformacion de claims. Si se decide usar roles de Keycloak directamente, se necesita un diseno explicito de mappers, nombres, migracion de datos y fuente de verdad; no basta con crear un rol homonimo.

## Limites y riesgos actuales

- La transformacion consulta PostgreSQL cuando se autentica una solicitud que no cumple su condicion de salida. Esto anade una consulta a la ruta de autorizacion y depende de que la base de datos este disponible.
- Si el JWT es valido pero no hay usuario local asociado a `IdentityId`, `FirstAsync()` produce una excepcion. Conviene decidir si el comportamiento de negocio debe ser 401, 403 o un error controlado.
- La condicion de salida comprueba `ClaimTypes.Role` y el claim literal `sub`, mientras `GetIdentityId()` busca `ClaimTypes.NameIdentifier`. El mapeo de claims del manejador JWT debe cubrir ambos casos; cualquier cambio en `MapInboundClaims` debe probarse.
- El codigo agrega un claim literal `sub` con el GUID local. Si tambien se conserva el `sub` externo sin mapear, `FindFirstValue` puede leer el identificador equivocado, incluso si es un GUID valido. Un tipo de claim propio, por ejemplo `bookify_user_id`, evitaria esa ambiguedad.
- No hay cache de roles ni invalidacion. Los cambios de rol se reflejan cuando una nueva autenticacion transforma el principal, a costa de consultar la base de datos.
- La asignacion `EmailVerified = true` al registrar una identidad no representa una verificacion real de correo. No debe utilizarse como prueba de identidad.
- El login usa Resource Owner Password Credentials (`grant_type=password`). Es util para este entorno de aprendizaje, pero no es el flujo recomendado para aplicaciones interactivas modernas; para estas conviene Authorization Code con PKCE.
- No hay pruebas HTTP de extremo a extremo que demuestren 401, 403 y acceso correcto con `Registered`. Las pruebas de controladores que los invocan directamente no ejecutan `[Authorize]` ni la transformacion de claims.

## Comprobacion manual

1. Aplicar las migraciones pendientes, incluidas `Add_UserRole`, `Change_TableName_And_Field` y `Add_Permission_Tables`.
2. Verificar en Keycloak que `bookify-admin-client` tiene cuenta de servicio y el permiso administrativo minimo para crear usuarios.
3. Registrar un usuario mediante `POST /api/users/register`; el flujo debe crear la identidad externa, guardar su id en `users.identity_id`, insertar la relacion en `role_user` y asociarla a `roles.id = 1`. Comprobar que `role_permissions` relaciona ese rol con `users:read`.
4. Iniciar sesion mediante `POST /api/users/login` y copiar el access token recibido.
5. Llamar a GET `/api/users/LogInUser` con `Authorization: Bearer <access_token>`.
6. Si falla, comprobar el claim `sub` del JWT, `users.identity_id`, las relaciones `role_user` y `role_permissions` y los nombres exactos `Registered` y `users:read`.

No registrar ni publicar tokens, contrasenas ni secretos de cliente en archivos de ejemplo o incidencias.
