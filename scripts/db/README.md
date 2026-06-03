# Scripts SQL de desarrollo

Utilidades one-shot para poblar o reparar datos en una BD local de
desarrollo. **No** son migraciones — el esquema lo maneja EF Core
(`dotnet ef database update`).

## Cómo correrlos

```bash
PGPASSWORD=bella2026 psql -h localhost -U bellasync_admin -d bellasync \
  -f scripts/db/<archivo>.sql
```

> El `tenant_id` está hardcoded en cada script. Antes de correrlo en
> otro salón, hacer `sed -i 's/TENANT-VIEJO/TENANT-NUEVO/g'`.

## Inventario

### `seed-demo.sql`
Pobla un tenant con datos demo de un día completo: 4 estilistas, 16
servicios, 21 clientes y 21 citas distribuidas entre 9 AM y 6 PM.
Idempotente: usa `WHERE NOT EXISTS` para no duplicar si ya hay datos
con el mismo nombre/teléfono/slot.

Mismo seed que el endpoint `POST /api/Admin/seed-demo-data` (que en
producción es la vía recomendada por respetar las validaciones de
dominio). Este SQL es más rápido para una BD local recién creada.

### `fix-overlaps.sql`
Repara overlaps de citas que el seed pudo crear. Necesario porque el
seed SQL puro NO pasa por la validación de overlap del handler — esa
validación vive en Application, no como CHECK constraint en BD.

Si te encuentras con dos citas del mismo estilista que se pisan en
tiempo, este script las separa.

## Verificar overlaps

```sql
SELECT st.full_name, c1.full_name AS a, c2.full_name AS b,
       a1.start_at, a2.start_at
FROM appointments a1
JOIN appointments a2 ON a1.stylist_id = a2.stylist_id AND a1.id < a2.id
JOIN stylists st ON a1.stylist_id = st.id
JOIN customers c1 ON a1.customer_id = c1.id
JOIN customers c2 ON a2.customer_id = c2.id
WHERE a1.start_at < a2.end_at AND a2.start_at < a1.end_at;
```
