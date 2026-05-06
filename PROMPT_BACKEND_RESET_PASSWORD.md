# Prompt para implementar reset password en backend BellaSync

> **Cómo usar este archivo:** copiá TODO el contenido (incluyendo este encabezado) y pegalo en una instancia nueva de Claude o cualquier IA con capacidad de escribir código. La IA debe tener acceso al sistema de archivos del proyecto en `C:\Proyectos\BellaSync\`.

---

# Tarea

Implementar el flujo de **recuperación de contraseña por email** en el backend de BellaSync (.NET 8). El frontend ya está hecho y espera 2 endpoints nuevos: `POST /api/Auth/forgot-password` y `POST /api/Auth/reset-password`. Tu trabajo es agregar entidad, migración, DTOs, validators, servicio de email (stub que loguea con Serilog) y los endpoints en `AuthController`.

---

# Contexto del proyecto

**BellaSync** es un SaaS multi-tenant para gestión de salones de belleza en Colombia (agendamiento, inventario, validación de pagos por WhatsApp, comunicación con clientes). El producto es B2B y NO usa pasarelas de pago en línea (todo va por transferencia bancaria + comprobante por WhatsApp).

## Stack del backend

- **.NET 8.0** (`net8.0`, `ImplicitUsings=enable`, `Nullable=enable`)
- **ASP.NET Core** Web API con controllers
- **PostgreSQL 16** local (host `localhost`, port `5432`, database `bellasync`)
- **EF Core 8.0.10** con `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10`
- **JWT** con `System.IdentityModel.Tokens.Jwt 7.6.0`
- **BCrypt** con `BCrypt.Net-Next 4.0.3`
- **FluentValidation** (auto-registrado)
- **Serilog** (configurado en `Program.cs`, escribe a console)
- **Swagger** (Swashbuckle) con soporte para Bearer JWT

## Arquitectura — Clean Architecture, 4 proyectos

```
src/
├── BellaSync.Domain/         (entidades + interfaces de dominio, sin deps externas)
├── BellaSync.Application/    (DTOs, validators, abstracciones — depende solo de Domain)
├── BellaSync.Infrastructure/ (EF, password hasher, JWT — depende de Application)
└── BellaSync.WebApi/         (Controllers + Program.cs — depende de Application + Infrastructure)
```

## Estado actual del backend (ya implementado)

- **Auth completo:**
  - `POST /api/Auth/register-salon` → crea Tenant + User admin + devuelve `AuthResponse` con JWT
  - `POST /api/Auth/login` → email/password → devuelve `AuthResponse` con JWT
- **Multi-tenancy:** filtro global EF aplicado a toda entidad que implemente `ITenantEntity`. `ICurrentTenantService` lee el `tenant_id` del JWT.
- **Entidades existentes:** `Tenant`, `User`, `Service`, `Stylist`, `StylistService`, `Customer`
- **Validators auto-registrados** con `AddValidatorsFromAssembly` en `BellaSync.Application/DependencyInjection.cs`
- **CORS** abierto para `http://localhost:5173` y `http://localhost:3000` (configurable en `appsettings.json` → `Cors:AllowedOrigins`)
- **Migración** EF se aplica con:
  ```bash
  dotnet ef migrations add <Name> --project src/BellaSync.Infrastructure --startup-project src/BellaSync.WebApi --output-dir Persistence/Migrations
  dotnet ef database update --project src/BellaSync.Infrastructure --startup-project src/BellaSync.WebApi
  ```

## Convenciones del proyecto (RESPETAR)

1. **Nombres de tablas en snake_case + plural.** Ejemplo real existente: `users`, `tenants`, `services`, `stylists`, `customers`, `stylist_services`. La nueva tabla debe llamarse `password_reset_tokens`.

2. **`BaseEntity`** (en `Domain/Common/BaseEntity.cs`):
   ```csharp
   public abstract class BaseEntity {
       public Guid Id { get; set; } = Guid.NewGuid();
       public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
       public DateTime? UpdatedAt { get; set; }
   }
   ```
   Toda entidad persistible hereda de `BaseEntity`. `UpdatedAt` se setea automáticamente en `ApplicationDbContext.SaveChangesAsync` para entidades modificadas.

3. **`ITenantEntity`:** entidades que pertenecen a un tenant lo implementan (tienen propiedad `TenantId`). El filtro global EF las filtra automáticamente.
   **⚠️ `PasswordResetToken` NO implementa `ITenantEntity`** porque la operación de reset es anónima (el usuario perdió su sesión, no hay JWT, no hay tenant_id en el contexto). Igual que `User` se puede buscar con `IgnoreQueryFilters()` en login, los tokens de reset se manejan sin filtro de tenant.

4. **Validators de FluentValidation:**
   - Viven en `Application/Features/<Feature>/Validators/`
   - Heredan de `AbstractValidator<TRequest>`
   - Mensajes **en español**
   - Se auto-registran (no hace falta agregarlos manualmente al DI)

5. **DTOs** viven en `Application/Features/<Feature>/Dtos/`. Son clases simples con propiedades públicas y getters/setters.

6. **EntityConfigurations** viven en `Infrastructure/Persistence/Configurations/`, implementan `IEntityTypeConfiguration<T>`. Configuran nombre de tabla, longitudes, índices, FKs.

7. **Servicios de Infrastructure** (como password hasher, JWT, futuro email service) viven en `Infrastructure/Services/`. Su interfaz vive en `Application/Common/Interfaces/`.

8. **Registro en DI:** los servicios de infraestructura se agregan en `Infrastructure/DependencyInjection.cs` dentro de `AddInfrastructure(IServiceCollection)`.

9. **Endpoints de Auth** son **anónimos** (sin `[Authorize]`). El `AuthController` ya tiene `[ApiController]` y `[Route("api/[controller]")]` a nivel clase.

10. **Errores con `ProblemDetails`:** seguir el patrón existente. Ejemplo del controller actual:
    ```csharp
    return BadRequest(new ProblemDetails {
        Title = "Token inválido",
        Detail = "El enlace expiró o ya fue usado.",
        Status = StatusCodes.Status400BadRequest
    });
    ```

11. **`IApplicationDbContext`** (en `Application/Common/Interfaces/`): abstracción del DbContext. Cuando agregás una entidad, hay que agregar su `DbSet<T>` acá Y en `ApplicationDbContext`.

12. **Auditoría automática:** no setees `UpdatedAt` manualmente en SaveChanges — `ApplicationDbContext.SaveChangesAsync` lo hace solo para entidades modificadas (no para nuevas).

---

# Contratos exactos de los endpoints (los que el frontend ya está usando)

## 1. `POST /api/Auth/forgot-password`

**Body:**
```json
{ "email": "carolina@bellaspa.co" }
```

**Response:** **siempre 200 OK** con cuerpo vacío. NO revelar si el email existe (security best practice — evita enumeration de usuarios).

**Comportamiento:**
- Validar formato email (FluentValidation `.EmailAddress()`).
- Si validación falla → 400 con ProblemDetails (lo hace `ValidationProblem(...)` que ya se usa en el controller).
- Buscar `User` por email (normalizado a lowercase + trim) usando `IgnoreQueryFilters()`.
- Si NO existe O `IsActive == false`: igual devolver 200 OK. Loguear `Information("Forgot password solicitado para email no existente: {Email}")`.
- Si existe:
  - Generar token: `Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()` → 64 chars hex.
  - Insertar fila en `password_reset_tokens`: `UserId`, `Token`, `ExpiresAt = UtcNow.AddHours(1)`, `UsedAt = null`.
  - Construir URL: `{Frontend:BaseUrl}/reset-password?token={token}` (leer base URL de `appsettings.json`).
  - Llamar `IEmailService.SendPasswordResetAsync(user.Email, user.FullName, resetUrl, ct)`.
  - Devolver 200 OK.

## 2. `POST /api/Auth/reset-password`

**Body:**
```json
{ "token": "abcdef0123...", "newPassword": "NuevaPass2026" }
```

**Response OK:** **204 No Content** (sin cuerpo).

**Response error:** 400 con `ProblemDetails`:
- Validación: 400 con `ValidationProblem` (errores por campo).
- Token inválido / expirado / ya usado: 400 con `Title = "Token inválido"`, `Detail = "El enlace expiró o ya fue usado. Solicita uno nuevo."`.

**Reglas de validación de `newPassword` (mismas que register-salon)**:
- No vacío
- 8 a 100 chars
- ≥1 letra mayúscula (regex `[A-Z]`)
- ≥1 letra minúscula (regex `[a-z]`)
- ≥1 número (regex `[0-9]`)

**Reglas de validación de `token`:**
- No vacío
- Longitud entre 32 y 128 chars

**Comportamiento:**
- Validar request con FluentValidation.
- Buscar `PasswordResetToken` por `Token` con `IgnoreQueryFilters()` (no aplica filtro tenant), incluyendo `User`.
- Si no existe OR `UsedAt.HasValue` OR `ExpiresAt < UtcNow`: 400 con ProblemDetails "Token inválido".
- Si OK:
  - `entity.User.PasswordHash = _passwordHasher.Hash(request.NewPassword)`
  - `entity.UsedAt = DateTime.UtcNow`
  - `await _db.SaveChangesAsync(ct)`
  - Loguear `Information("Password reset completado para {Email}", entity.User.Email)`.
  - Devolver `NoContent()`.

---

# Archivos a CREAR (en orden)

## 1. `src/BellaSync.Domain/Entities/PasswordResetToken.cs`

```csharp
using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Token de un solo uso para restablecer contraseña.
/// NO implementa ITenantEntity porque el flujo es anónimo
/// (el usuario perdió su sesión, no hay JWT en el request).
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Token hex de 64 caracteres (32 bytes random).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Vence 1 hora después de generarse (configurable).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Null si aún no se usó. Cuando se usa, se setea a UtcNow.</summary>
    public DateTime? UsedAt { get; set; }

    // Navegación
    public User User { get; set; } = null!;
}
```

## 2. `src/BellaSync.Application/Common/Interfaces/IEmailService.cs`

```csharp
namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción para envío de emails transaccionales.
/// En desarrollo se usa LoggingEmailService (loguea con Serilog).
/// En producción se reemplaza por SendGrid / Postmark / Mailgun / etc.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un email con el enlace de reseteo de contraseña.
    /// </summary>
    /// <param name="toEmail">Email del destinatario.</param>
    /// <param name="fullName">Nombre completo del usuario (para el saludo).</param>
    /// <param name="resetUrl">URL completa con token (ej. http://localhost:5173/reset-password?token=xxx).</param>
    Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default);
}
```

## 3. `src/BellaSync.Application/Features/Auth/Dtos/ForgotPasswordRequest.cs`

```csharp
namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>Solicitud para enviar enlace de reseteo de contraseña por email.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
```

## 4. `src/BellaSync.Application/Features/Auth/Dtos/ResetPasswordRequest.cs`

```csharp
namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>Solicitud para guardar la nueva contraseña usando un token recibido por email.</summary>
public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
```

## 5. `src/BellaSync.Application/Features/Auth/Validators/ForgotPasswordValidator.cs`

```csharp
using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.")
            .MaximumLength(150);
    }
}
```

## 6. `src/BellaSync.Application/Features/Auth/Validators/ResetPasswordValidator.cs`

```csharp
using BellaSync.Application.Features.Auth.Dtos;
using FluentValidation;

namespace BellaSync.Application.Features.Auth.Validators;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token requerido.")
            .Length(32, 128).WithMessage("Token con longitud inválida.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
```

## 7. `src/BellaSync.Infrastructure/Persistence/Configurations/PasswordResetTokenConfiguration.cs`

```csharp
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(128);

        // El token es buscado por valor en cada reset → índice único
        builder.HasIndex(t => t.Token).IsUnique();

        // Para limpiar tokens antiguos por usuario rápido
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

## 8. `src/BellaSync.Infrastructure/Services/LoggingEmailService.cs`

```csharp
using BellaSync.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación de IEmailService para desarrollo.
/// NO envía emails reales; loguea el contenido con Serilog para que la
/// dev pueda copiar el reset URL desde la consola del backend.
/// En producción se reemplaza por SendGridEmailService (a implementar).
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            @"
================================================================
  PASSWORD RESET (DEV MODE — no se envió email real)
  Para:    {Email}  ({Name})
  URL:     {Url}
  Asunto:  Restablece tu contraseña en BellaSync
================================================================",
            toEmail, fullName, resetUrl);

        return Task.CompletedTask;
    }
}
```

---

# Archivos a MODIFICAR

## 1. `src/BellaSync.Application/Common/Interfaces/IApplicationDbContext.cs`

Agregar la línea del nuevo `DbSet<PasswordResetToken>`:

```csharp
DbSet<PasswordResetToken> PasswordResetTokens { get; }
```

(después de `DbSet<Customer> Customers { get; }`)

## 2. `src/BellaSync.Infrastructure/Persistence/ApplicationDbContext.cs`

Agregar la propiedad correspondiente:

```csharp
public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
```

(después de `public DbSet<Customer> Customers => Set<Customer>();`)

No hace falta tocar `OnModelCreating` — `ApplyConfigurationsFromAssembly` levanta automáticamente la nueva `PasswordResetTokenConfiguration`.

## 3. `src/BellaSync.Infrastructure/DependencyInjection.cs`

Dentro de `AddInfrastructure(IServiceCollection, IConfiguration)`, registrar:

```csharp
services.AddScoped<IEmailService, LoggingEmailService>();
```

(junto a las otras `services.AddScoped<...>` existentes — ahí donde se registran `IPasswordHasher`, `IJwtTokenService`).

## 4. `src/BellaSync.WebApi/appsettings.json`

Agregar una sección `Frontend` para configurar el origen del enlace que se manda por email:

```json
"Frontend": {
  "BaseUrl": "http://localhost:5173"
}
```

## 5. `src/BellaSync.WebApi/Controllers/AuthController.cs`

Agregar 2 acciones nuevas. **Importante:** seguir el patrón existente del controller (ver `RegisterSalon` y `Login` como referencia: usan `IValidator<T>` inyectado, devuelven `ValidationProblem` ante errores 400, y usan `BuildModelState` que ya existe en el controller).

Vas a necesitar inyectar dos cosas nuevas en el constructor:
- `IValidator<ForgotPasswordRequest> _forgotValidator`
- `IValidator<ResetPasswordRequest> _resetValidator`
- `IEmailService _emailService`
- `IConfiguration _configuration` (para leer `Frontend:BaseUrl`)

Y agregar los 2 endpoints:

```csharp
/// <summary>
/// Solicita el envío de un enlace de reseteo de contraseña.
/// Siempre responde 200 OK aunque el email no exista (no revelar enumeration).
/// </summary>
[HttpPost("forgot-password")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ForgotPassword(
    [FromBody] ForgotPasswordRequest request,
    CancellationToken ct)
{
    var validation = await _forgotValidator.ValidateAsync(request, ct);
    if (!validation.IsValid)
    {
        return ValidationProblem(BuildModelState(validation));
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();

    var user = await _db.Users
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    if (user is null || !user.IsActive)
    {
        _logger.LogInformation(
            "Forgot password solicitado para email no existente o inactivo: {Email}",
            normalizedEmail);
        return Ok();
    }

    // Generar token hex de 64 chars (32 bytes random)
    var token = Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        .ToLowerInvariant();

    var entity = new PasswordResetToken
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Token = token,
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        CreatedAt = DateTime.UtcNow,
    };
    _db.PasswordResetTokens.Add(entity);
    await _db.SaveChangesAsync(ct);

    var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
    var resetUrl = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={token}";

    await _emailService.SendPasswordResetAsync(user.Email, user.FullName, resetUrl, ct);

    return Ok();
}

/// <summary>
/// Guarda la nueva contraseña usando un token recibido por email.
/// El token es de un solo uso (se invalida con UsedAt) y expira a la hora.
/// </summary>
[HttpPost("reset-password")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ResetPassword(
    [FromBody] ResetPasswordRequest request,
    CancellationToken ct)
{
    var validation = await _resetValidator.ValidateAsync(request, ct);
    if (!validation.IsValid)
    {
        return ValidationProblem(BuildModelState(validation));
    }

    var entity = await _db.PasswordResetTokens
        .IgnoreQueryFilters()
        .Include(t => t.User)
        .FirstOrDefaultAsync(t => t.Token == request.Token, ct);

    if (entity is null || entity.UsedAt.HasValue || entity.ExpiresAt < DateTime.UtcNow)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Token inválido",
            Detail = "El enlace expiró o ya fue usado. Solicita uno nuevo.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    entity.User.PasswordHash = _passwordHasher.Hash(request.NewPassword);
    entity.UsedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);

    _logger.LogInformation("Password reset completado para {Email}", entity.User.Email);
    return NoContent();
}
```

Vas a necesitar agregar también estos `using` al tope del controller:
- `using BellaSync.Application.Features.Auth.Dtos;` (probablemente ya está)
- `using BellaSync.Application.Common.Interfaces;` (para `IEmailService`)
- `using BellaSync.Domain.Entities;` (probablemente ya está)
- `using Microsoft.Extensions.Configuration;` (para `IConfiguration` — quizás ya está implícitamente)

---

# Pasos finales (en orden)

1. **Crear todos los archivos** listados arriba.

2. **Agregar las modificaciones** a los 5 archivos existentes.

3. **Compilar** para verificar que no hay errores:
   ```bash
   dotnet build
   ```

4. **Crear migración EF**:
   ```bash
   dotnet ef migrations add AddPasswordResetTokens \
     --project src/BellaSync.Infrastructure \
     --startup-project src/BellaSync.WebApi \
     --output-dir Persistence/Migrations
   ```
   Inspeccionar el archivo generado en `src/BellaSync.Infrastructure/Persistence/Migrations/` — debería crear la tabla `password_reset_tokens` con FK a `users(id)` con `ON DELETE CASCADE` y un índice único en `token`.

5. **Aplicar migración a PostgreSQL**:
   ```bash
   dotnet ef database update \
     --project src/BellaSync.Infrastructure \
     --startup-project src/BellaSync.WebApi
   ```

6. **Correr la API**:
   ```bash
   dotnet run --project src/BellaSync.WebApi
   ```

7. **Probar end-to-end con Swagger**:
   - Abrir `http://localhost:5059/swagger`
   - `POST /api/Auth/forgot-password` con body `{ "email": "gabriela@bellaspa.com" }` (o cualquier email registrado).
   - Verificar 200 OK.
   - **En la terminal del backend** vas a ver el log warning con el reset URL completo. Copiar el `token` de la URL.
   - `POST /api/Auth/reset-password` con body `{ "token": "<token-copiado>", "newPassword": "NuevaPass2026" }`.
   - Verificar 204 No Content.
   - `POST /api/Auth/login` con la nueva password — verificar que da 200 OK con `AuthResponse`.

8. **Verificar en pgAdmin**:
   ```sql
   SELECT user_id, token, expires_at, used_at, created_at
   FROM password_reset_tokens
   ORDER BY created_at DESC;
   ```
   Debe haber una fila con `used_at` ya seteado.

9. **Casos de error que probar**:
   - Token inexistente → 400 "Token inválido"
   - Token ya usado (correr el reset 2 veces seguidas con el mismo token) → 400
   - Email mal formado en forgot-password → 400 con `ProblemDetails.errors.Email`
   - Password sin mayúscula en reset-password → 400 con `ProblemDetails.errors.NewPassword`
   - Email NO existente en forgot-password → 200 OK (sin token generado, sin email enviado)

---

# Notas finales

- **NO instalar paquetes nuevos.** Todo lo que se necesita ya está en `BellaSync.Infrastructure.csproj` (BCrypt, Npgsql, EF Core).
- **NO tocar JWT, BCrypt, o el pipeline de auth existente.** Solo agregar lo nuevo.
- Cuando más adelante se agregue email real (SendGrid, Postmark, etc.), se va a crear `SendGridEmailService : IEmailService` en `Infrastructure/Services/`, y en `DependencyInjection.cs` se va a hacer:
  ```csharp
  if (env.IsDevelopment())
      services.AddScoped<IEmailService, LoggingEmailService>();
  else
      services.AddScoped<IEmailService, SendGridEmailService>();
  ```
- El frontend ya está hecho y espera estos endpoints exactos. NO cambiar las URLs ni los cuerpos.

# ¿Algo no claro?

Si algo del proyecto no queda claro al leer este prompt, primero leé los archivos existentes en `C:\Proyectos\BellaSync\src\` (especialmente `AuthController.cs`, `ApplicationDbContext.cs`, `IApplicationDbContext.cs`, y cualquier `*Configuration.cs` ya existente como referencia de estilo). El proyecto sigue convenciones Clean Architecture estándar y los patrones existentes son consistentes.
