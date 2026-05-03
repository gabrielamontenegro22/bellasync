# BellaSync

Plataforma SaaS multi-tenant para gestión integral de salones de belleza.

## Stack

- **Backend**: .NET 8, Entity Framework Core, PostgreSQL 16
- **Frontend**: React + Vite + TypeScript + Tailwind CSS (próximamente)
- **Auth**: JWT con multi-tenancy por TenantId

## Arquitectura

Clean Architecture con 4 capas:

- `BellaSync.Domain` — entidades, enums, interfaces puras
- `BellaSync.Application` — casos de uso, DTOs, validaciones
- `BellaSync.Infrastructure` — EF Core, repositorios, servicios externos
- `BellaSync.WebApi` — controllers HTTP, middlewares

## Estado del desarrollo

- [x] Setup inicial Clean Architecture
- [ ] Multi-tenancy + Auth JWT
- [ ] Catálogo de servicios
- [ ] Motor de agendamiento
- [ ] Validación de pagos
- [ ] Inventario
- [ ] WhatsApp Business API
