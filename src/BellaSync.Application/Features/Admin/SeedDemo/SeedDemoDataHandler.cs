using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Admin.SeedDemo;

/// <summary>
/// Crea datos demo (estilistas, servicios, clientes, citas) en el tenant
/// actual. Idempotente: nunca duplica si ya existe algo con el mismo
/// nombre/teléfono.
///
/// Las citas se programan para la fecha indicada (default: mañana en hora
/// Colombia UTC-5) entre 9 AM y 6 PM. Si alguna no pasa la validación de
/// dominio (overlap, en pasado, etc.) se saltea silenciosamente y la
/// respuesta reporta el conteo.
/// </summary>
public sealed class SeedDemoDataHandler : ICommandHandler<SeedDemoDataCommand, SeedDemoDataResponse>
{
    // Colombia es UTC-5 todo el año (no aplica DST). Convertir hora local
    // a UTC = sumar 5 horas. Lo dejamos hardcoded acá porque BellaSync solo
    // opera en Colombia.
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<SeedDemoDataHandler> _logger;

    public SeedDemoDataHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<SeedDemoDataHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SeedDemoDataResponse>> HandleAsync(
        SeedDemoDataCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var response = new SeedDemoDataResponse();

        // ===== 1. Servicios =====
        var serviceMap = await EnsureServicesAsync(tenantId, response, ct);

        // ===== 2. Estilistas (cada uno con sus servicios asignados) =====
        var stylistMap = await EnsureStylistsAsync(tenantId, serviceMap, response, ct);

        // ===== 3. Clientes =====
        var customerMap = await EnsureCustomersAsync(tenantId, response, ct);

        // ===== 4. Citas para la fecha indicada =====
        var targetDate = command.TargetDate ?? DateOnly.FromDateTime(
            DateTime.UtcNow.Add(ColombiaOffset).AddDays(1));
        response.TargetDate = targetDate.ToString("yyyy-MM-dd");

        await EnsureAppointmentsAsync(tenantId, targetDate, stylistMap, serviceMap, customerMap, response, ct);

        _logger.LogInformation(
            "Seed demo data ejecutado en tenant {TenantId}: stylists +{S}, services +{Sv}, customers +{C}, appointments +{A}",
            tenantId, response.StylistsCreated, response.ServicesCreated, response.CustomersCreated, response.AppointmentsCreated);

        return Result<SeedDemoDataResponse>.Success(response);
    }

    // ===== Datos crudos del seed =====

    private record ServiceSeed(string Name, ServiceCategory Category, int Minutes, decimal Price);
    private record StylistSeed(string Name, string Role);
    private record CustomerSeed(string Name, string Phone, string Email, string Birthday, bool Marketing);
    private record AppointmentSeed(string CustomerName, string StylistName, string ServiceName, int Hour, int Minute);

    private static readonly ServiceSeed[] Services = new[]
    {
        new ServiceSeed("Corte + cepillado",           ServiceCategory.Cabello,    60,  60_000m),
        new ServiceSeed("Tinte raíz + corte",          ServiceCategory.Cabello,    90, 160_000m),
        new ServiceSeed("Balayage + tratamiento",      ServiceCategory.Cabello,   120, 220_000m),
        new ServiceSeed("Alisado brasilero",           ServiceCategory.Cabello,    90, 350_000m),
        new ServiceSeed("Mechas californianas",        ServiceCategory.Cabello,    90, 295_000m),
        new ServiceSeed("Manicure semipermanente",     ServiceCategory.Unas,       45,  45_000m),
        new ServiceSeed("Pedicure spa",                ServiceCategory.Unas,       60,  55_000m),
        new ServiceSeed("Depilación cejas",            ServiceCategory.Depilacion, 30,  25_000m),
        new ServiceSeed("Lifting + tinte pestañas",    ServiceCategory.Estetica,   90,  90_000m),
        new ServiceSeed("Color completo + corte",      ServiceCategory.Cabello,    90, 160_000m),
        new ServiceSeed("Retoque raíz",                ServiceCategory.Cabello,    45,  70_000m),
        new ServiceSeed("Cepillado",                   ServiceCategory.Cabello,    30,  35_000m),
        new ServiceSeed("Pedicure + esmaltado",        ServiceCategory.Unas,       45,  40_000m),
        new ServiceSeed("Decoloración + matiz",        ServiceCategory.Cabello,   120, 280_000m),
        new ServiceSeed("Extensiones pestañas pelo a pelo", ServiceCategory.Estetica, 90, 120_000m),
        new ServiceSeed("Cera bigote + cejas",         ServiceCategory.Depilacion, 30,  20_000m),
    };

    private static readonly StylistSeed[] Stylists = new[]
    {
        new StylistSeed("Carolina Rodríguez", "Estilista senior"),
        new StylistSeed("Andrea Patiño",      "Color & balayage"),
        new StylistSeed("Lina Mejía",         "Manicure & pedicure"),
        new StylistSeed("Juliana Ríos",       "Cejas & pestañas"),
    };

    private static readonly CustomerSeed[] Customers = new[]
    {
        new CustomerSeed("María González",      "+57 311 245 7782", "mariag@gmail.com",     "1992-03-14", true),
        new CustomerSeed("Valentina Castaño",   "+57 314 678 1290", "valec@gmail.com",      "1988-07-09", true),
        new CustomerSeed("Isabella Trujillo",   "+57 315 220 4471", "isatru@gmail.com",     "1995-11-23", true),
        new CustomerSeed("Daniela Ospina",      "+57 318 552 3344", "danios@gmail.com",     "1997-02-04", false),
        new CustomerSeed("Camila Restrepo",     "+57 313 778 9912", "camires@gmail.com",    "1990-09-30", true),
        new CustomerSeed("Andrea Patiño S.",    "+57 320 411 5678", "andreaps@gmail.com",   "1993-05-18", true),
        new CustomerSeed("Salomé Gutiérrez",    "+57 311 998 7654", "salomeg@gmail.com",    "1986-12-01", false),
        new CustomerSeed("Laura Bernal",        "+57 315 220 1133", "laurab@gmail.com",     "2000-08-12", true),
        new CustomerSeed("Juana Saldarriaga",   "+57 319 663 4477", "juanasa@gmail.com",    "1984-06-25", true),
        new CustomerSeed("Verónica Arango",     "+57 312 887 2210", "veroar@gmail.com",     "1991-04-07", false),
        new CustomerSeed("Mariana Vélez",       "+57 317 552 8899", "marivel@gmail.com",    "1996-10-19", true),
        new CustomerSeed("Manuela Lozano",      "+57 313 102 6655", "manulo@gmail.com",     "1989-01-28", true),
        new CustomerSeed("Sofía Hernández",     "+57 314 998 1122", "sofiher@gmail.com",    "1994-11-11", true),
        new CustomerSeed("Catalina Mora",       "+57 320 776 3344", "catamo@gmail.com",     "1998-07-22", false),
        new CustomerSeed("Diana Cárdenas",      "+57 318 224 9988", "diacar@gmail.com",     "1985-03-09", true),
        new CustomerSeed("Paula Quintero",      "+57 311 552 0099", "pauqui@gmail.com",     "1993-09-17", true),
        new CustomerSeed("Tatiana Mendoza",     "+57 316 778 4422", "tatime@gmail.com",     "1995-01-05", true),
        new CustomerSeed("Natalia Acevedo",     "+57 319 220 7711", "natace@gmail.com",     "1987-08-30", false),
        new CustomerSeed("Lorena Jiménez",      "+57 312 663 8855", "lorjim@gmail.com",     "1990-12-12", true),
        new CustomerSeed("Alejandra Buitrago",  "+57 317 998 3322", "alebui@gmail.com",     "1996-04-21", true),
        new CustomerSeed("Gabriela Salazar",    "+57 315 411 9966", "gabsal@gmail.com",     "1992-06-15", false),
    };

    // Citas distribuidas en el día. Modelado después del mockup `Agenda de Hoy.html`.
    // Cada tupla: (cliente, estilista, servicio, hora local, minuto).
    private static readonly AppointmentSeed[] Appointments = new[]
    {
        // Columna Carolina Rodríguez (Estilista senior)
        new AppointmentSeed("María González",      "Carolina Rodríguez", "Corte + cepillado",       9,  0),
        new AppointmentSeed("Valentina Castaño",   "Carolina Rodríguez", "Tinte raíz + corte",     10,  0),
        new AppointmentSeed("Daniela Ospina",      "Carolina Rodríguez", "Alisado brasilero",      11,  0),
        new AppointmentSeed("Camila Restrepo",     "Carolina Rodríguez", "Corte + cepillado",      13,  0),
        new AppointmentSeed("Sofía Hernández",     "Carolina Rodríguez", "Mechas californianas",   15,  0),
        new AppointmentSeed("Paula Quintero",      "Carolina Rodríguez", "Cepillado",              17, 30),

        // Columna Andrea Patiño (Color & balayage)
        new AppointmentSeed("Isabella Trujillo",   "Andrea Patiño",      "Balayage + tratamiento",  9,  0),
        new AppointmentSeed("Andrea Patiño S.",    "Andrea Patiño",      "Color completo + corte", 11, 30),
        new AppointmentSeed("Mariana Vélez",       "Andrea Patiño",      "Retoque raíz",           14,  0),
        new AppointmentSeed("Catalina Mora",       "Andrea Patiño",      "Decoloración + matiz",   15, 30),

        // Columna Lina Mejía (Manicure & pedicure)
        new AppointmentSeed("Laura Bernal",        "Lina Mejía",         "Manicure semipermanente", 9,  0),
        new AppointmentSeed("Juana Saldarriaga",   "Lina Mejía",         "Pedicure spa",           10,  0),
        new AppointmentSeed("Verónica Arango",     "Lina Mejía",         "Pedicure + esmaltado",   11,  0),
        new AppointmentSeed("Salomé Gutiérrez",    "Lina Mejía",         "Manicure semipermanente",12,  0),
        new AppointmentSeed("Manuela Lozano",      "Lina Mejía",         "Pedicure + esmaltado",   14,  0),
        new AppointmentSeed("Diana Cárdenas",      "Lina Mejía",         "Manicure semipermanente",16,  0),
        new AppointmentSeed("Alejandra Buitrago",  "Lina Mejía",         "Pedicure spa",           17, 30),

        // Columna Juliana Ríos (Cejas & pestañas)
        new AppointmentSeed("Tatiana Mendoza",     "Juliana Ríos",       "Depilación cejas",       10,  0),
        new AppointmentSeed("Natalia Acevedo",     "Juliana Ríos",       "Lifting + tinte pestañas",11, 30),
        new AppointmentSeed("Lorena Jiménez",      "Juliana Ríos",       "Extensiones pestañas pelo a pelo", 15, 0),
        new AppointmentSeed("Gabriela Salazar",    "Juliana Ríos",       "Cera bigote + cejas",    17,  0),
    };

    // ===== Helpers idempotentes =====

    private async Task<Dictionary<string, Service>> EnsureServicesAsync(
        Guid tenantId, SeedDemoDataResponse response, CancellationToken ct)
    {
        var existing = await _db.Services
            .Where(s => s.TenantId == tenantId)
            .ToDictionaryAsync(s => s.Name, ct);

        var result = new Dictionary<string, Service>(StringComparer.Ordinal);
        foreach (var seed in Services)
        {
            if (existing.TryGetValue(seed.Name, out var existingService))
            {
                result[seed.Name] = existingService;
                response.ServicesSkipped++;
                continue;
            }

            var service = Service.Create(
                tenantId: tenantId,
                name: seed.Name,
                category: seed.Category,
                durationMinutes: seed.Minutes,
                price: Money.Create(seed.Price),
                commission: Percentage.Create(40m),
                requiresDeposit: false,
                depositPercentage: Percentage.Zero);

            _db.Services.Add(service);
            result[seed.Name] = service;
            response.ServicesCreated++;
        }

        if (response.ServicesCreated > 0)
            await _db.SaveChangesAsync(ct);

        return result;
    }

    private async Task<Dictionary<string, Stylist>> EnsureStylistsAsync(
        Guid tenantId,
        Dictionary<string, Service> serviceMap,
        SeedDemoDataResponse response,
        CancellationToken ct)
    {
        var existing = await _db.Stylists
            .Include(s => s.StylistServices)
            .Where(s => s.TenantId == tenantId)
            .ToDictionaryAsync(s => s.FullName, ct);

        var result = new Dictionary<string, Stylist>(StringComparer.Ordinal);

        foreach (var seed in Stylists)
        {
            Stylist stylist;
            if (existing.TryGetValue(seed.Name, out var existingStylist))
            {
                stylist = existingStylist;
                response.StylistsSkipped++;
            }
            else
            {
                stylist = Stylist.Create(
                    tenantId: tenantId,
                    fullName: seed.Name,
                    role: seed.Role);
                _db.Stylists.Add(stylist);
                response.StylistsCreated++;
            }
            result[seed.Name] = stylist;
        }

        // Persistir nuevos estilistas antes de asignar servicios (necesitan Id)
        if (response.StylistsCreated > 0)
            await _db.SaveChangesAsync(ct);

        // Asignar TODOS los servicios a TODOS los estilistas (idempotente).
        // En un salón real cada estilista tiene un subconjunto, pero para
        // el demo así cualquier cita seed encuentra al estilista capaz.
        var utcNow = _clock.UtcNow;
        var anyAssignmentAdded = false;
        foreach (var stylist in result.Values)
        {
            foreach (var service in serviceMap.Values)
            {
                if (stylist.StylistServices.Any(ss => ss.ServiceId == service.Id)) continue;
                stylist.AssignService(service.Id, utcNow);
                anyAssignmentAdded = true;
            }
        }
        if (anyAssignmentAdded) await _db.SaveChangesAsync(ct);

        return result;
    }

    private async Task<Dictionary<string, Customer>> EnsureCustomersAsync(
        Guid tenantId, SeedDemoDataResponse response, CancellationToken ct)
    {
        // Index existentes por nombre exacto (para matchear los seeds) y por
        // phone (para no chocar con el unique index del backend).
        var existing = await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);
        var byName = existing.ToDictionary(c => c.FullName, c => c);
        var byPhone = existing.Where(c => !string.IsNullOrWhiteSpace(c.Phone))
                              .ToDictionary(c => c.Phone, c => c);

        var result = new Dictionary<string, Customer>(StringComparer.Ordinal);
        foreach (var seed in Customers)
        {
            if (byName.TryGetValue(seed.Name, out var existingByName))
            {
                result[seed.Name] = existingByName;
                response.CustomersSkipped++;
                continue;
            }
            if (byPhone.TryGetValue(seed.Phone, out var existingByPhone))
            {
                result[seed.Name] = existingByPhone;
                response.CustomersSkipped++;
                continue;
            }

            var customer = Customer.Create(
                tenantId: tenantId,
                fullName: seed.Name,
                phone: seed.Phone,
                email: seed.Email,
                birthday: DateOnly.Parse(seed.Birthday),
                acceptsMarketing: seed.Marketing);
            _db.Customers.Add(customer);
            result[seed.Name] = customer;
            response.CustomersCreated++;
        }

        if (response.CustomersCreated > 0)
            await _db.SaveChangesAsync(ct);

        return result;
    }

    private async Task EnsureAppointmentsAsync(
        Guid tenantId,
        DateOnly targetDate,
        Dictionary<string, Stylist> stylistMap,
        Dictionary<string, Service> serviceMap,
        Dictionary<string, Customer> customerMap,
        SeedDemoDataResponse response,
        CancellationToken ct)
    {
        // Citas YA existentes en el día y tenant — para evitar duplicar
        // si el seed se ejecuta dos veces.
        var dayStartUtc = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue),
            ColombiaOffset).UtcDateTime;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var existingAppointments = await _db.Appointments
            .Where(a => a.TenantId == tenantId
                     && a.StartAt >= dayStartUtc
                     && a.StartAt < dayEndUtc)
            .ToListAsync(ct);

        var utcNow = _clock.UtcNow;

        foreach (var seed in Appointments)
        {
            if (!customerMap.TryGetValue(seed.CustomerName, out var customer)) continue;
            if (!stylistMap.TryGetValue(seed.StylistName, out var stylist)) continue;
            if (!serviceMap.TryGetValue(seed.ServiceName, out var service)) continue;

            // Hora local Colombia → UTC. ColombiaOffset es -5, así que
            // local 9 AM → UTC 14:00.
            var localStart = targetDate.ToDateTime(new TimeOnly(seed.Hour, seed.Minute));
            var startAtUtc = new DateTimeOffset(localStart, ColombiaOffset).UtcDateTime;
            var endAtUtc = startAtUtc.AddMinutes(service.DurationMinutes);

            // Skip si ya existe esta misma cita (mismo cliente + stylist + start)
            if (existingAppointments.Any(a =>
                a.CustomerId == customer.Id
                && a.StylistId == stylist.Id
                && a.StartAt == startAtUtc))
            {
                response.AppointmentsSkipped++;
                continue;
            }

            // Skip si el slot del stylist ya está ocupado (evita overlap)
            if (existingAppointments.Any(a =>
                a.StylistId == stylist.Id
                && a.OverlapsWith(startAtUtc, endAtUtc)))
            {
                response.AppointmentsSkipped++;
                continue;
            }

            // Si la hora ya pasó (TargetDate es hoy y el slot es muy temprano),
            // el factory rechazaría. Skip silenciosamente.
            if (startAtUtc <= utcNow)
            {
                response.AppointmentsSkipped++;
                continue;
            }

            try
            {
                var appointment = Appointment.Create(
                    tenantId: tenantId,
                    customerId: customer.Id,
                    stylistId: stylist.Id,
                    serviceId: service.Id,
                    startAtUtc: startAtUtc,
                    endAtUtc: endAtUtc,
                    priceSnapshot: service.Price,
                    depositPercentage: Percentage.Zero,
                    requiresDeposit: false,
                    channel: AppointmentChannel.Reception,
                    notes: null,
                    utcNow: utcNow,
                    holdDuration: TimeSpan.FromHours(3),
                    holdMinBeforeAppointment: TimeSpan.FromMinutes(30));

                _db.Appointments.Add(appointment);
                existingAppointments.Add(appointment);  // para evitar overlap entre seeds
                response.AppointmentsCreated++;
            }
            catch
            {
                // Cualquier error del dominio = skip silencioso (no rompe el batch)
                response.AppointmentsSkipped++;
            }
        }

        if (response.AppointmentsCreated > 0)
            await _db.SaveChangesAsync(ct);
    }
}
