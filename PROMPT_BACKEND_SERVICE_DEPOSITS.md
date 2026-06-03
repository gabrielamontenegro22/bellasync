# Prompt para agregar `RequiresDeposit` y `DepositPercentage` a Service

> **Cómo usar este archivo:** copiá TODO el contenido y pegalo en una instancia nueva de Claude (o cualquier IA con acceso al filesystem del proyecto en `C:\Proyectos\BellaSync\`).

---

# Tarea

Agregar dos campos nuevos a la entidad `Service` del backend BellaSync para soportar la funcionalidad de **anticipo** (que el frontend ya tiene implementada en localStorage y queremos migrar al backend):

| Campo | Tipo | Default | Significado |
|---|---|---|---|
| `RequiresDeposit` | `bool` | `false` | Si el servicio requiere que el cliente haga un pago parcial para confirmar la cita |
| `DepositPercentage` | `decimal` (0-100) | `0` | Porcentaje del precio que se cobra como anticipo |

---

# Contexto del proyecto

**BellaSync** es un SaaS multi-tenant en .NET 8.0 + ASP.NET Core + PostgreSQL 16 + EF Core 8.0.10, con Clean Architecture (4 proyectos: Domain, Application, Infrastructure, WebApi).

Las convenciones del proyecto son:
- Tablas en snake_case plural (`services`, `users`, etc.).
- `BaseEntity` con `Id (Guid)`, `CreatedAt`, `UpdatedAt` (auditado automáticamente en `SaveChangesAsync`).
- Validators FluentValidation auto-registrados (no hace falta tocar DI).
- Mensajes de error en español.
- DTOs en `Application/Features/Services/Dtos/`.
- Validators en `Application/Features/Services/Validators/`.
- EntityConfigurations en `Infrastructure/Persistence/Configurations/`.

**No instalar paquetes nuevos.** Todo lo necesario ya está en el proyecto.

---

# Archivos a modificar (en orden)

## 1. Entidad — `src/BellaSync.Domain/Entities/Service.cs`

Agregar 2 propiedades nuevas, manteniendo el resto intacto. Estado actual relevante:

```csharp
public class Service : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ServiceCategory Category { get; set; } = ServiceCategory.Otros;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    // ← AGREGAR ACÁ ↓
}
```

Agregar:

```csharp
    /// <summary>
    /// Si es true, el servicio requiere que el cliente haga un pago parcial
    /// (anticipo) para confirmar la cita. El monto se calcula con DepositPercentage.
    /// </summary>
    public bool RequiresDeposit { get; set; } = false;

    /// <summary>
    /// Porcentaje del precio que se cobra como anticipo (0 a 100).
    /// Solo se aplica cuando RequiresDeposit es true.
    /// Si RequiresDeposit es false, este valor se ignora (típicamente 0).
    /// </summary>
    public decimal DepositPercentage { get; set; } = 0m;
```

## 2. Entity Configuration — `src/BellaSync.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs`

Agregar las dos `Property` después de `CommissionPercentage`. Mantener el resto del archivo igual.

Buscar:
```csharp
        builder.Property(s => s.CommissionPercentage)
            .IsRequired()
            .HasColumnType("numeric(5,2)");
```

Agregar inmediatamente después:
```csharp
        builder.Property(s => s.RequiresDeposit).IsRequired();

        builder.Property(s => s.DepositPercentage)
            .IsRequired()
            .HasColumnType("numeric(5,2)");
```

## 3. DTO — `src/BellaSync.Application/Features/Services/Dtos/CreateServiceRequest.cs`

Agregar 2 propiedades después de `Color`:

```csharp
public class CreateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ServiceCategory Category { get; set; } = ServiceCategory.Otros;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }
    public string? Color { get; set; }

    /* AGREGAR ↓ */
    public bool RequiresDeposit { get; set; } = false;
    public decimal DepositPercentage { get; set; } = 0m;
}
```

## 4. DTO — `src/BellaSync.Application/Features/Services/Dtos/UpdateServiceRequest.cs`

Mismos 2 campos en el mismo orden:

```csharp
public class UpdateServiceRequest
{
    // ...campos existentes...
    public string? Color { get; set; }

    public bool RequiresDeposit { get; set; } = false;
    public decimal DepositPercentage { get; set; } = 0m;

    public bool IsActive { get; set; } = true;
}
```

> ⚠️ Cuidado: `IsActive` debe quedar **después** de los nuevos campos (que es como ya lo tiene actualmente).

## 5. DTO — `src/BellaSync.Application/Features/Services/Dtos/ServiceResponse.cs`

Mismos 2 campos:

```csharp
public class ServiceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }

    /* AGREGAR ↓ */
    public bool RequiresDeposit { get; set; }
    public decimal DepositPercentage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

## 6. Validator Create — `src/BellaSync.Application/Features/Services/Validators/CreateServiceValidator.cs`

Agregar la regla de `DepositPercentage` después de la regla de `CommissionPercentage`. (`RequiresDeposit` siendo bool no necesita validación explícita.)

Buscar:
```csharp
        RuleFor(x => x.CommissionPercentage)
            .InclusiveBetween(0m, 100m)
            .WithMessage("La comisión debe estar entre 0 y 100 por ciento.");
```

Agregar después:
```csharp
        RuleFor(x => x.DepositPercentage)
            .InclusiveBetween(0m, 100m)
            .WithMessage("El anticipo debe estar entre 0 y 100 por ciento.");
```

## 7. Validator Update — `src/BellaSync.Application/Features/Services/Validators/UpdateServiceValidator.cs`

Mismo cambio que el validator de Create — agregar la regla de `DepositPercentage` con el mismo mensaje.

## 8. Controller — `src/BellaSync.WebApi/Controllers/ServicesController.cs`

Hay **3 lugares** donde modificar:

### 8.a. En el método `Create()` — al construir el `Service` nuevo

Buscar:
```csharp
        var service = new Service
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            Name = name,
            Description = request.Description?.Trim(),
            Category = request.Category,
            DurationMinutes = request.DurationMinutes,
            Price = request.Price,
            CommissionPercentage = request.CommissionPercentage,
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
```

Agregar las 2 líneas nuevas (mantener el resto):
```csharp
        var service = new Service
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            Name = name,
            Description = request.Description?.Trim(),
            Category = request.Category,
            DurationMinutes = request.DurationMinutes,
            Price = request.Price,
            CommissionPercentage = request.CommissionPercentage,
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            RequiresDeposit = request.RequiresDeposit,
            DepositPercentage = request.DepositPercentage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
```

### 8.b. En el método `Update()` — al asignar campos

Buscar:
```csharp
        service.Name = newName;
        service.Description = request.Description?.Trim();
        service.Category = request.Category;
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.CommissionPercentage = request.CommissionPercentage;
        service.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        service.IsActive = request.IsActive;
```

Agregar las 2 líneas nuevas:
```csharp
        service.Name = newName;
        service.Description = request.Description?.Trim();
        service.Category = request.Category;
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.CommissionPercentage = request.CommissionPercentage;
        service.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        service.RequiresDeposit = request.RequiresDeposit;
        service.DepositPercentage = request.DepositPercentage;
        service.IsActive = request.IsActive;
```

### 8.c. En el método estático `MapToResponse()`

Buscar:
```csharp
    private static ServiceResponse MapToResponse(Service s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        Category = s.Category.ToString(),
        DurationMinutes = s.DurationMinutes,
        Price = s.Price,
        CommissionPercentage = s.CommissionPercentage,
        Color = s.Color,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
```

Agregar las 2 líneas nuevas:
```csharp
    private static ServiceResponse MapToResponse(Service s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        Category = s.Category.ToString(),
        DurationMinutes = s.DurationMinutes,
        Price = s.Price,
        CommissionPercentage = s.CommissionPercentage,
        Color = s.Color,
        IsActive = s.IsActive,
        RequiresDeposit = s.RequiresDeposit,
        DepositPercentage = s.DepositPercentage,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
```

---

# Pasos finales (en orden)

1. **Compilar** para detectar errores antes de la migración:
   ```bash
   dotnet build
   ```
   Tiene que terminar con `Build succeeded` y 0 errores.

2. **Crear la migración EF**:
   ```bash
   dotnet ef migrations add AddDepositFieldsToService \
     --project src/BellaSync.Infrastructure \
     --startup-project src/BellaSync.WebApi \
     --output-dir Persistence/Migrations
   ```

3. **Inspeccionar el archivo generado** en `src/BellaSync.Infrastructure/Persistence/Migrations/`. Debería tener:
   - `migrationBuilder.AddColumn<bool>(name: "RequiresDeposit", table: "services", nullable: false, defaultValue: false);`
   - `migrationBuilder.AddColumn<decimal>(name: "DepositPercentage", table: "services", type: "numeric(5,2)", nullable: false, defaultValue: 0m);`

   ⚠️ **Si los `defaultValue` NO están** (puede pasar si EF no infiere defaults para no-nullable en tablas con datos), editar el archivo manualmente para agregarlos. Sin defaults, la migración fallaría en una BD con servicios existentes.

4. **Aplicar la migración**:
   ```bash
   dotnet ef database update \
     --project src/BellaSync.Infrastructure \
     --startup-project src/BellaSync.WebApi
   ```

5. **Verificar la tabla en pgAdmin**:
   ```sql
   \d services
   ```
   Deberían aparecer las dos columnas nuevas:
   - `RequiresDeposit` (boolean, NOT NULL, default false)
   - `DepositPercentage` (numeric(5,2), NOT NULL, default 0.00)

   Y los servicios existentes deben tener ambos campos en sus valores default:
   ```sql
   SELECT name, "RequiresDeposit", "DepositPercentage"
   FROM services
   ORDER BY "CreatedAt" DESC LIMIT 5;
   ```

6. **Correr la API**:
   ```bash
   dotnet run --project src/BellaSync.WebApi
   ```

7. **Probar con Swagger** (`http://localhost:5059/swagger`):

   - `POST /api/Services` con body que incluya:
     ```json
     {
       "name": "Test con anticipo",
       "category": "Cabello",
       "durationMinutes": 60,
       "price": 100000,
       "commissionPercentage": 30,
       "requiresDeposit": true,
       "depositPercentage": 50
     }
     ```
     Verificar que el response incluya `requiresDeposit: true` y `depositPercentage: 50`.

   - `GET /api/Services/{id}` con el id devuelto: confirmar que se persistieron.

   - `PUT /api/Services/{id}` cambiando `requiresDeposit: false, depositPercentage: 0`: verificar que se actualizan.

   - Probar validación de error: `POST /api/Services` con `depositPercentage: 150` → 400 con mensaje "El anticipo debe estar entre 0 y 100 por ciento."

---

# Notas finales

- **No tocar `ServiceValidationRules.cs`.** Las constantes existentes (PriceMin, PriceMax, etc.) no necesitan cambios. La regla de DepositPercentage es inline en los validators (igual que CommissionPercentage).
- **No agregar lógica condicional al backend** del estilo "si RequiresDeposit es false, forzar DepositPercentage a 0". Mantenerlo simple: validar rango 0-100 siempre, dejar al cliente la responsabilidad de coherencia. Si el día de mañana se quiere agregar esa regla, se hace con `RuleFor(x => x.DepositPercentage).When(x => x.RequiresDeposit, ApplyConditionTo.CurrentValidator).GreaterThan(0m)`.
- **Los servicios existentes** en BD recibirán `RequiresDeposit = false` y `DepositPercentage = 0` automáticamente al aplicar la migración con defaults. No requiere data migration adicional.
- **El frontend ya está preparado:** una vez que estos campos existan en el backend, otra instancia (o yo) refactoriza el frontend para sacar `requiresDeposit` y `depositPercentage` del `serviceExtrasStorage` de localStorage y mandarlos al backend en Create/Update.

# ¿Algo no claro?

Si algún paso no queda claro, lee primero estos archivos como referencia del estilo del proyecto:
- `src/BellaSync.Domain/Entities/Service.cs` (estructura de entity)
- `src/BellaSync.Application/Features/Services/Validators/CreateServiceValidator.cs` (estilo de validators)
- `src/BellaSync.WebApi/Controllers/ServicesController.cs` (mapeo y patrón de controller)

El proyecto sigue patrones consistentes — al replicar lo existente vas a salir bien.
