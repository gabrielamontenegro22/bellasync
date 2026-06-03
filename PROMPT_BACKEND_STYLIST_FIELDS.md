# Prompt para extender la entidad Stylist con campos del mockup

> **Cómo usar este archivo:** copiá TODO el contenido y pegalo en una instancia nueva de Claude (o cualquier IA con acceso al filesystem en `C:\Proyectos\BellaSync\`).

---

# Tarea

Extender la entidad `Stylist` del backend BellaSync para soportar los campos que el mockup `stylists.jsx` muestra y que hoy NO existen en la entidad. Específicamente:

| Campo | Tipo | Default | Significado |
|---|---|---|---|
| `Role` | `string` (max 80) | `"Estilista"` | Cargo: "Estilista", "Estilista Senior", "Colorista", "Manicurista", "Maquilladora", "Esteticista", "Aprendiz", "Recepcionista" (string libre — los salones pueden inventar otros) |
| `Email` | `string?` (max 150) | `null` | Email de contacto del estilista (NO confundir con el del User asociado, que es para login) |
| `IdNumber` | `string?` (max 30) | `null` | Cédula colombiana, ej. "1.020.554.901". Se almacena tal cual la usuaria la escribe |
| `Status` | enum `StylistStatus` | `Active` | Estado del estilista: `Active`, `Vacation`, `Inactive`. **REEMPLAZA al actual `IsActive` (bool)** |

⚠️ **Cambio invasivo:** se reemplaza `IsActive` (bool) por `Status` (enum). La migración debe mapear los valores existentes:
- `IsActive == true`  → `Status = Active` (0)
- `IsActive == false` → `Status = Inactive` (2)

Y todos los usos de `IsActive` en el código se cambian por `Status != Inactive` (o el valor exacto según el contexto).

---

# Contexto del proyecto

**BellaSync** es un SaaS multi-tenant en .NET 8.0 + ASP.NET Core + PostgreSQL 16 + EF Core 8.0.10, con Clean Architecture (4 proyectos: Domain, Application, Infrastructure, WebApi).

**Convenciones del proyecto:**
- Tablas en snake_case plural (`stylists`, `services`, etc.)
- `BaseEntity` con `Id (Guid)`, `CreatedAt`, `UpdatedAt` (auditado automáticamente)
- `ITenantEntity` para multi-tenancy con filtro global EF
- Validators FluentValidation auto-registrados
- Mensajes de error en español
- DTOs en `Application/Features/Stylists/Dtos/`
- Validators en `Application/Features/Stylists/Validators/`
- EntityConfigurations en `Infrastructure/Persistence/Configurations/`
- Enums se persisten como `int` (`HasConversion<int>()`)
- El frontend ya tiene `JsonStringEnumConverter` configurado en `Program.cs`, así que los enums se serializan como string ("Active", "Vacation", "Inactive")

**No instalar paquetes nuevos.** Todo lo necesario ya está en el proyecto.

---

# Estado actual relevante (lo que vas a modificar)

## Entidad `Stylist.cs` (en `Domain/Entities/`)

Tiene actualmente: `Id`, `TenantId`, `FullName`, `Phone`, `Color`, `HireDate`, `IsActive`, `UserId`, `CreatedAt`, `UpdatedAt`, navegación a `StylistServices`.

Después del cambio: `IsActive` se reemplaza por `Status` (enum) y se agregan `Role`, `Email`, `IdNumber`.

## DTOs (`Application/Features/Stylists/Dtos/`)

- `CreateStylistRequest.cs` — agregar campos nuevos. NO agregar `Status` acá (al crear, siempre arranca como `Active`).
- `UpdateStylistRequest.cs` — agregar campos nuevos. Reemplazar `IsActive` (bool) por `Status` (enum).
- `StylistResponse.cs` — agregar campos nuevos. Reemplazar `IsActive` (bool) por `Status` (string).

## Validators (`Application/Features/Stylists/Validators/`)

Agregar reglas para los 3 strings nuevos. Para `Status`, usar `IsInEnum()`.

## `StylistsController.cs` (`WebApi/Controllers/`)

Reemplazar todos los `s.IsActive` por `s.Status != StylistStatus.Inactive` (o lo que aplique en cada caso). En `Delete` → setear `Status = Inactive` en vez de `IsActive = false`.

---

# Archivos a CREAR

## 1. Enum `StylistStatus` — `src/BellaSync.Domain/Entities/StylistStatus.cs`

```csharp
namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del estilista dentro del salón.
/// Reemplaza el antiguo IsActive (bool) para soportar también el estado "Vacaciones",
/// donde la persona sigue siendo parte del equipo pero no toma citas temporalmente.
/// </summary>
public enum StylistStatus
{
    /// <summary>Toma citas normalmente.</summary>
    Active = 0,

    /// <summary>Sigue en el equipo pero temporalmente no toma citas.</summary>
    Vacation = 1,

    /// <summary>
    /// Ya no forma parte del equipo (soft delete). No aparece en listas
    /// para agendar pero las citas históricas siguen referenciándolo.
    /// </summary>
    Inactive = 2,
}
```

---

# Archivos a MODIFICAR

## 1. `src/BellaSync.Domain/Entities/Stylist.cs`

### Estado actual (relevante)

La entidad tiene:
```csharp
public bool IsActive { get; set; } = true;
```

### Cambio

**Quitar** la propiedad `IsActive` (REPLACE).

**Agregar** las 4 nuevas:

```csharp
    /// <summary>
    /// Cargo del estilista dentro del salón.
    /// String libre porque los salones pueden inventar roles propios
    /// ("Maquillador profesional", "Asistente de color", etc.).
    /// Sugeridos: Estilista, Estilista Senior, Colorista, Manicurista,
    /// Maquilladora, Esteticista, Aprendiz, Recepcionista.
    /// </summary>
    public string Role { get; set; } = "Estilista";

    /// <summary>
    /// Email de contacto del estilista. NO confundir con el email del User
    /// asociado (que es para login). Este es solo para notificaciones internas
    /// del salón. Opcional.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Cédula de ciudadanía. Se almacena tal cual la escribe la administradora
    /// (con o sin puntos). Sirve para liquidación y trazabilidad.
    /// </summary>
    public string? IdNumber { get; set; }

    /// <summary>
    /// Estado del estilista. Reemplaza al antiguo IsActive (bool) y soporta
    /// además el caso "Vacation" donde sigue en el equipo pero no toma citas.
    /// </summary>
    public StylistStatus Status { get; set; } = StylistStatus.Active;
```

## 2. `src/BellaSync.Infrastructure/Persistence/Configurations/StylistConfiguration.cs`

### Buscar la línea actual

```csharp
        builder.Property(s => s.IsActive).IsRequired();
```

### Reemplazar por

```csharp
        builder.Property(s => s.Role)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(s => s.Email)
            .HasMaxLength(150);

        builder.Property(s => s.IdNumber)
            .HasMaxLength(30);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();
```

### Si hay un índice/filtro que mencione `IsActive` (ej. `HasFilter("\"IsActive\" = true")`)

Reemplazarlo por el equivalente con `Status`. Por ejemplo, si había un índice único de `(TenantId, FullName)` filtrado por activos:

```csharp
        builder.HasIndex(s => new { s.TenantId, s.FullName })
            .IsUnique()
            .HasFilter("\"Status\" <> 2");  // 2 = Inactive
```

(Si el filtro original era distinto o no existía, dejarlo en línea con la lógica anterior.)

## 3. `src/BellaSync.Application/Features/Stylists/Dtos/CreateStylistRequest.cs`

Agregar 3 campos. NO agregar `Status` (siempre arranca Active al crear):

```csharp
public class CreateStylistRequest
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Cargo: "Estilista", "Estilista Senior", "Colorista", etc.</summary>
    public string Role { get; set; } = "Estilista";

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? IdNumber { get; set; }
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    public List<Guid> ServiceIds { get; set; } = new();
}
```

## 4. `src/BellaSync.Application/Features/Stylists/Dtos/UpdateStylistRequest.cs`

Agregar los nuevos campos y **reemplazar `IsActive` (bool) por `Status` (StylistStatus)**:

```csharp
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Stylists.Dtos;

public class UpdateStylistRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Estilista";

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? IdNumber { get; set; }
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>Reemplaza al antiguo IsActive. Soporta Active / Vacation / Inactive.</summary>
    public StylistStatus Status { get; set; } = StylistStatus.Active;

    public List<Guid> ServiceIds { get; set; } = new();
}
```

## 5. `src/BellaSync.Application/Features/Stylists/Dtos/StylistResponse.cs`

Agregar los nuevos campos y reemplazar `IsActive` (bool) por `Status` (string):

```csharp
public class StylistResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? IdNumber { get; set; }
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>Active / Vacation / Inactive (serializado como string gracias al JsonStringEnumConverter).</summary>
    public string Status { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public List<StylistAssignedServiceDto> Services { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

## 6. `src/BellaSync.Application/Features/Stylists/Validators/CreateStylistValidator.cs`

Agregar reglas para los nuevos campos. Mantener todas las existentes:

```csharp
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("El cargo es obligatorio.")
            .MaximumLength(80).WithMessage("El cargo no puede superar los 80 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.IdNumber)
            .MaximumLength(30).WithMessage("La cédula no puede superar los 30 caracteres.");
```

(Las reglas de `Phone`, `Color`, `HireDate`, `FullName` ya existen — no tocar.)

## 7. `src/BellaSync.Application/Features/Stylists/Validators/UpdateStylistValidator.cs`

Mismas reglas que el Create + agregar:

```csharp
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Estado inválido.");
```

(Y quitar la regla de `IsActive` si existía.)

## 8. `src/BellaSync.WebApi/Controllers/StylistsController.cs`

Hay **varios lugares** donde se usa `s.IsActive`. Reemplazar todos:

### 8.a. En `List()` — el filtro `includeInactive`

Buscar:
```csharp
        IQueryable<Stylist> filtered = includeInactive
            ? query
            : query.Where(s => s.IsActive);
```

Reemplazar por:
```csharp
        IQueryable<Stylist> filtered = includeInactive
            ? query
            : query.Where(s => s.Status != StylistStatus.Inactive);
```

### 8.b. En `Create()` — la validación de nombre duplicado

Buscar:
```csharp
        var nameTaken = await _db.Stylists
            .AnyAsync(s => s.IsActive && s.FullName == fullName, ct);
```

Reemplazar por:
```csharp
        var nameTaken = await _db.Stylists
            .AnyAsync(s => s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
```

### 8.c. En `Create()` — al construir el Stylist nuevo

Buscar:
```csharp
        var stylist = new Stylist
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            FullName = fullName,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            HireDate = request.HireDate,
            IsActive = true,
            UserId = null,
            CreatedAt = DateTime.UtcNow
        };
```

Reemplazar `IsActive = true` por `Status = StylistStatus.Active` y agregar los nuevos campos:

```csharp
        var stylist = new Stylist
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            FullName = fullName,
            Role = request.Role.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IdNumber = string.IsNullOrWhiteSpace(request.IdNumber) ? null : request.IdNumber.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            HireDate = request.HireDate,
            Status = StylistStatus.Active,
            UserId = null,
            CreatedAt = DateTime.UtcNow
        };
```

### 8.d. En `Update()` — la validación de nombre duplicado y los asignamientos

Buscar:
```csharp
        if (request.IsActive && nameChanged)
        {
            var nameTaken = await _db.Stylists
                .AnyAsync(s => s.Id != id && s.IsActive && s.FullName == fullName, ct);
```

Reemplazar por:
```csharp
        if (request.Status != StylistStatus.Inactive && nameChanged)
        {
            var nameTaken = await _db.Stylists
                .AnyAsync(s => s.Id != id && s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
```

Y al asignar campos:

Buscar:
```csharp
        stylist.FullName = fullName;
        stylist.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        stylist.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        stylist.HireDate = request.HireDate;
        stylist.IsActive = request.IsActive;
```

Reemplazar por:
```csharp
        stylist.FullName = fullName;
        stylist.Role = request.Role.Trim();
        stylist.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        stylist.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        stylist.IdNumber = string.IsNullOrWhiteSpace(request.IdNumber) ? null : request.IdNumber.Trim();
        stylist.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        stylist.HireDate = request.HireDate;
        stylist.Status = request.Status;
```

### 8.e. En `Delete()` — soft delete

Buscar:
```csharp
        if (!stylist.IsActive) return NoContent();

        stylist.IsActive = false;
```

Reemplazar por:
```csharp
        if (stylist.Status == StylistStatus.Inactive) return NoContent();

        stylist.Status = StylistStatus.Inactive;
```

### 8.f. En `MapToResponse()`

Buscar:
```csharp
    private static StylistResponse MapToResponse(Stylist s) => new()
    {
        Id = s.Id,
        FullName = s.FullName,
        Phone = s.Phone,
        Color = s.Color,
        HireDate = s.HireDate,
        IsActive = s.IsActive,
        UserId = s.UserId,
        ...
    };
```

Reemplazar por:
```csharp
    private static StylistResponse MapToResponse(Stylist s) => new()
    {
        Id = s.Id,
        FullName = s.FullName,
        Role = s.Role,
        Email = s.Email,
        Phone = s.Phone,
        IdNumber = s.IdNumber,
        Color = s.Color,
        HireDate = s.HireDate,
        Status = s.Status.ToString(),
        UserId = s.UserId,
        ...
    };
```

### 8.g. ⚠️ Agregar `using` al tope del controller

```csharp
using BellaSync.Domain.Entities;  // probablemente ya está, sino agregalo
```

(`StylistStatus` vive en ese namespace.)

---

# Migración EF — IMPORTANTE

Esta migración tiene 3 partes:
1. Agregar 3 columnas nuevas (`Role`, `Email`, `IdNumber`)
2. Agregar columna `Status` con default Active (0)
3. **Migrar los datos**: convertir `IsActive=false` → `Status=2` (Inactive). Los `IsActive=true` ya quedan como `Status=0` (Active) por el default.
4. Eliminar la columna `IsActive`

Crear la migración:

```bash
dotnet ef migrations add ExtendStylistFields \
  --project src/BellaSync.Infrastructure \
  --startup-project src/BellaSync.WebApi \
  --output-dir Persistence/Migrations
```

Inspeccionar el archivo generado en `src/BellaSync.Infrastructure/Persistence/Migrations/`. **EF probablemente NO va a hacer la migración de datos automáticamente**. Hay que editarlo manualmente.

Ejemplo del `Up()` esperado tras tu edición manual:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Nuevos campos string
    migrationBuilder.AddColumn<string>(
        name: "Role",
        table: "stylists",
        type: "character varying(80)",
        maxLength: 80,
        nullable: false,
        defaultValue: "Estilista");

    migrationBuilder.AddColumn<string>(
        name: "Email",
        table: "stylists",
        type: "character varying(150)",
        maxLength: 150,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "IdNumber",
        table: "stylists",
        type: "character varying(30)",
        maxLength: 30,
        nullable: true);

    // 2. Status enum, default Active
    migrationBuilder.AddColumn<int>(
        name: "Status",
        table: "stylists",
        type: "integer",
        nullable: false,
        defaultValue: 0); // 0 = Active

    // 3. Migrar datos: los que tenían IsActive=false pasan a Status=2 (Inactive)
    migrationBuilder.Sql(@"UPDATE stylists SET ""Status"" = 2 WHERE ""IsActive"" = false;");

    // 4. Si existe un índice que filtraba por IsActive, recrearlo con Status
    //    (Si el StylistConfiguration tenía un HasFilter sobre IsActive, EF lo va a regenerar
    //    automáticamente con Status. Si NO, ignorar este paso.)

    // 5. Eliminar IsActive
    migrationBuilder.DropColumn(
        name: "IsActive",
        table: "stylists");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "IsActive",
        table: "stylists",
        type: "boolean",
        nullable: false,
        defaultValue: true);

    migrationBuilder.Sql(@"UPDATE stylists SET ""IsActive"" = false WHERE ""Status"" = 2;");

    migrationBuilder.DropColumn(name: "Status",   table: "stylists");
    migrationBuilder.DropColumn(name: "IdNumber", table: "stylists");
    migrationBuilder.DropColumn(name: "Email",    table: "stylists");
    migrationBuilder.DropColumn(name: "Role",     table: "stylists");
}
```

⚠️ **Si la migración auto-generada hace `DropColumn("IsActive")` ANTES del `Sql("UPDATE ...")`**, el SQL falla porque `IsActive` ya no existe. **Asegurate del orden**: primero el UPDATE, después el DropColumn.

---

# Pasos finales (en orden)

1. **Compilar:**
   ```bash
   dotnet build
   ```
   Esperás `Build succeeded`. Si hay errores, suelen ser de algún `s.IsActive` que se te pasó en el controller — buscalo y reemplazalo por `s.Status != StylistStatus.Inactive`.

2. **Crear migración** (ver arriba). **Editar el archivo generado** si es necesario para asegurar el orden correcto.

3. **Aplicar migración:**
   ```bash
   dotnet ef database update \
     --project src/BellaSync.Infrastructure \
     --startup-project src/BellaSync.WebApi
   ```

4. **Verificar en pgAdmin:**
   ```sql
   \d stylists
   ```
   Deben aparecer:
   - `Role` (varchar 80, NOT NULL, default 'Estilista')
   - `Email` (varchar 150, nullable)
   - `IdNumber` (varchar 30, nullable)
   - `Status` (integer, NOT NULL, default 0)
   - **No debe aparecer `IsActive`**

   ```sql
   SELECT "FullName", "Role", "Status", "Email", "IdNumber"
   FROM stylists ORDER BY "CreatedAt" DESC LIMIT 5;
   ```

5. **Correr la API:**
   ```bash
   dotnet run --project src/BellaSync.WebApi
   ```

6. **Probar con Swagger** (`http://localhost:5059/swagger`):

   - `POST /api/Stylists`:
     ```json
     {
       "fullName": "Carolina Rodríguez",
       "role": "Estilista Senior",
       "email": "carolina@bellaaurora.co",
       "phone": "+57 314 220 8841",
       "idNumber": "1.020.554.901",
       "color": "#0f766e",
       "hireDate": "2024-03-14",
       "serviceIds": []
     }
     ```
     Esperado: 201 con response que incluye `"status": "Active"` y todos los campos.

   - `PUT /api/Stylists/{id}` cambiando `"status": "Vacation"` → 200 OK con `status: Vacation`.

   - `DELETE /api/Stylists/{id}` → 204. Después GET → `status: Inactive`.

   - `GET /api/Stylists` (sin includeInactive) → no aparece el inactivo.

   - `GET /api/Stylists?includeInactive=true` → aparece todos los estados.

7. **Validación de errores a probar:**
   - `POST` con `role: ""` → 400 con "El cargo es obligatorio."
   - `POST` con `email: "no-es-email"` → 400 con "Formato de correo electrónico inválido."
   - `PUT` con `status: "Invalid"` → 400 con "Estado inválido." (gracias a `IsInEnum()`)

---

# Notas finales

- **El frontend ya espera estos cambios.** Una vez que el backend compile y la migración esté aplicada, otra instancia (o yo) implementa la pantalla F5 que va a usar todos estos campos.
- **No tocar `StylistService.cs`** ni nada de la relación M:N. Eso queda como está — sigue funcionando.
- **No tocar otras entidades** (`User`, `Service`, `Tenant` no se tocan; mantienen su `IsActive` propio).
- **El JsonStringEnumConverter ya está configurado** en `Program.cs`, así que `Status` se serializa como string en JSON ("Active", "Vacation", "Inactive") sin trabajo adicional.

# ¿Algo no claro?

Si algo del proyecto no queda claro, mirar como referencia:
- `src/BellaSync.Application/Features/Auth/Validators/RegisterSalonValidator.cs` (estilo de validators con `When` opcional)
- `src/BellaSync.Domain/Entities/Service.cs` (cómo está modelado un enum como propiedad)
- La migración previa `20260506235257_AddDepositFieldsToService.cs` (estructura de migraciones similares)

Los patrones del proyecto son consistentes — al replicar lo existente vas a salir bien.
