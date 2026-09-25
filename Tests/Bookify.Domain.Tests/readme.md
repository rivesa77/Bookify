# Pruebas de Bookify.Domain

ApartmentTests comprueba que Update sustituye los valores editables, copia Amenities y conserva identidad y fecha de ultima reserva. La reserva previa se crea con Booking.Reserve, sin alterar setters privados mediante reflexion.

## Objetivo y ejecucion

Pruebas unitarias de las reglas de negocio y los contratos publicos con comportamiento de Domain. El proyecto referencia solo `Bookify.Domain`: no carga Application, API, Infrastructure, contenedores de dependencias, PostgreSQL ni Keycloak. No necesita mocks de repositorios.

Se usan las mismas versiones de MSTest 4, FluentAssertions y `Microsoft.NET.Test.Sdk` que en Application.Tests. El runner es VSTest con `MSTest.TestAdapter`, no Microsoft.Testing.Platform. Esta incluido en la carpeta Tests de `Bookify.slnx`.

Desde la raiz del repositorio:

```powershell
dotnet test Tests/Bookify.Domain.Tests/Bookify.Domain.Tests.csproj
dotnet test Tests/Bookify.Domain.Tests/Bookify.Domain.Tests.csproj --collect "Code Coverage;Format=Cobertura"
```

Las lineas no ejecutadas corresponden a los constructores privados sin parametros de `Apartment`, `Booking`, `Review` y `User`, reservados para materializacion. No se invocan por reflexion para aumentar artificialmente la cobertura: su uso debe comprobarse en pruebas de integracion de persistencia. Una cobertura alta no demuestra que las reglas de negocio esten completas.


## Contenido

| Archivo | Comportamiento comprobado |
| --- | --- |
| `Abstractions/ResultTests.cs` | `Result` y `Result<T>`: exito, fallo, conservacion del error y del valor, combinaciones invalidas del constructor, acceso prohibido al valor de un fallo, conversion implicita, entrada nula y valores por defecto. Una subclase minima permite probar el constructor protegido sin cambiar su visibilidad. |
| `Abstractions/EntityTests.cs` | `Entity`: identificador, constructor protegido vacio, acumulacion ordenada de eventos, duplicados, copia independiente de la lista, limpieza repetida y reutilizacion. El doble hereda de Entity para acceder a la operacion protegida de publicacion. |
| `Abstractions/ErrorTests.cs` | `Error`: igualdad por codigo y mensaje, ausencia de error y catalogos `ApartmentErrors`, `NameErrors`, `BookingErrors`, `ReviewErrors`, `UserErrors` y `Rating.Invalid`. Se fijan los codigos; los mensajes deben existir, sin congelar su redaccion. |
| `Apartments/ApartmentTests.cs` | Constructor de Apartment: todos los datos recibidos, amenities, fecha inicial sin reserva y ausencia de eventos. Tambien componentes e igualdad de Address. |
| `Apartments/NameTests.cs` | Nombre nulo, vacio o en blanco; limites 1, 199, 200 y 201; conservacion del texto e igualdad por valor. |
| `Bookings/DateRangeTests.cs` | Fechas, dias de duracion, igualdad, rango invertido, rango de cero dias, cruces de mes y anio, y dia bisiesto. |
| `Bookings/PricingServicesTests.cs` | `PricingServices` y `PricingDetails`: precio por dia, las diez amenities, suma de recargos sobre el precio base, limpieza una sola vez, monedas, cero dias y precision decimal sin redondeo implicito. |
| `Bookings/BookingTests.cs` | Reserva con identificadores y precios, actualizacion de LastBookedOnUTC, fechas iniciales, 20 combinaciones de estado/operacion, errores sin mutacion, cancelacion hasta la fecha de entrada incluida y ciclo de confirmacion/completado. Comprueba contenido y orden de los cinco eventos de Booking. |
| `Commons/MoneyTests.cs` | Money: suma, operandos sin modificar, rechazo de monedas distintas, ceros con y sin moneda, IsZero e igualdad. |
| `Commons/CurrencyTests.cs` | Currency: EUR y USD, rechazo de codigos desconocidos o mal escritos y catalogo sin duplicados. |
| `Commons/TextValueObjectTests.cs` | Description, Comment, FirstName, LastName y Email: conservacion del texto e igualdad por valor. Son records sin validacion propia. |
| `Reviews/RatingTests.cs` | Los cinco valores admitidos y valores fuera del intervalo, incluidos extremos de int. |
| `Reviews/ReviewTests.cs` | Creacion solo desde reserva completada, todos los campos del review, identificadores independientes y ReviewCreatedDomainEvent. Los otros cuatro estados fallan sin cambiar la reserva. |
| `Users/UserTests.cs` | Creacion, datos, identidad inicial, rol Registered, UserCreatedDomainEvent, identificadores y colecciones independientes, sustitucion de IdentityId sin nuevos eventos. |
| `Users/AuthorizationModelTests.cs` | Role, Permission y RolePermission: identificadores y nombres predefinidos, constructores, colecciones independientes y enlace entre rol y permiso. No modifica las instancias estaticas compartidas. |
| `Support/DomainTestData.cs` | Factorias de apartamentos, reservas y usuarios. Fechas fijas; cada llamada crea entidades nuevas. Las reservas alcanzan sus estados mediante operaciones publicas, sin modificar propiedades privadas. |

Los eventos se ejercitan al ejecutar las operaciones que los producen y comprobar sus identificadores mediante igualdad con el evento esperado. `Amenity` y `BookingStatus` se usan como datos de los casos. `IDomainEvent`, `IRepository<T>`, los cuatro repositorios e `IUnitOfWork` no contienen implementacion propia que pueda probarse de forma aislada; sus implementaciones e interacciones pertenecen a tests de otras capas. Los tests de arquitectura existentes siguen siendo independientes.

## Alcance y limitaciones

Outbox y Quartz no agregan dependencias a este proyecto. Los tests comprueban la produccion, copia y limpieza de eventos en las entidades; su serializacion, almacenamiento y publicacion posterior se verifican en [Infrastructure.Tests](../Bookify.Infrastructure.Tests/readme.md#pruebas-outbox).

- Domain permite DateRange de cero dias; la exigencia de fecha final posterior pertenece al validador de Application. Complete comprueba el estado, pero no exige que haya terminado la estancia. Cancel permite todo el dia de entrada.
- Los records de texto no validan longitud, formato o contenido; por ejemplo, Email no comprueba que el texto sea un correo. Ademas, los init publicos de algunos objetos de valor pueden permitir saltarse sus factorias. Esta bateria no afirma que esas invariantes esten protegidas.
- Las colecciones publicas de amenities y permisos no son inmutables. Las pruebas de aislamiento de roles usan instancias locales, nunca cambian Role.Registered.
- La cobertura no comprueba mapeos EF Core, concurrencia entre transacciones, persistencia de eventos ni permisos HTTP. Esos comportamientos requieren otras capas o pruebas de integracion.
- Domain produce advertencias CS8632 por usar anotaciones de nulabilidad con Nullable desactivado. Son anteriores a este proyecto y no se han silenciado.
