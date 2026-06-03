-- Seed demo data para tenant ZAFIRA (gabrielam200409@gmail.com)
-- Fecha objetivo: 2026-06-03 (hora local Colombia UTC-5)
-- Idempotente: usa NOT EXISTS para no duplicar.

BEGIN;

-- =====================================================================
-- VARIABLES (tenant + fecha)
-- =====================================================================
-- Tenant ID hardcoded — extraído del usuario gabrielam200409@gmail.com
-- Cambiarlo si vas a correr el script para otro salón.

-- =====================================================================
-- 1. SERVICIOS
-- =====================================================================
INSERT INTO services (id, tenant_id, name, category, duration_minutes, price, commission_percentage, deposit_percentage, requires_deposit, is_active, created_at)
SELECT gen_random_uuid(), '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid, x.name, x.category, x.minutes, x.price, 40.0, 0.0, false, true, now()
FROM (VALUES
  ('Corte + cepillado',                   0,  60,  60000),
  ('Tinte raíz + corte',                  0,  90, 160000),
  ('Balayage + tratamiento',              0, 120, 220000),
  ('Alisado brasilero',                   0,  90, 350000),
  ('Mechas californianas',                0,  90, 295000),
  ('Manicure semipermanente',             1,  45,  45000),
  ('Pedicure spa',                        1,  60,  55000),
  ('Depilación cejas',                    4,  30,  25000),
  ('Lifting + tinte pestañas',            2,  90,  90000),
  ('Color completo + corte',              0,  90, 160000),
  ('Retoque raíz',                        0,  45,  70000),
  ('Cepillado',                           0,  30,  35000),
  ('Pedicure + esmaltado',                1,  45,  40000),
  ('Decoloración + matiz',                0, 120, 280000),
  ('Extensiones pestañas pelo a pelo',    2,  90, 120000),
  ('Cera bigote + cejas',                 4,  30,  20000)
) AS x(name, category, minutes, price)
WHERE NOT EXISTS (
  SELECT 1 FROM services s
  WHERE s.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
    AND s.name = x.name
);

-- =====================================================================
-- 2. ESTILISTAS
-- =====================================================================
INSERT INTO stylists (id, tenant_id, full_name, role, status, created_at)
SELECT gen_random_uuid(), '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid, x.name, x.role, 0, now()
FROM (VALUES
  ('Carolina Rodríguez', 'Estilista senior'),
  ('Andrea Patiño',      'Color & balayage'),
  ('Lina Mejía',         'Manicure & pedicure'),
  ('Juliana Ríos',       'Cejas & pestañas')
) AS x(name, role)
WHERE NOT EXISTS (
  SELECT 1 FROM stylists s
  WHERE s.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
    AND s.full_name = x.name
);

-- =====================================================================
-- 3. STYLIST_SERVICES — asignar todos los servicios a todos los estilistas
-- =====================================================================
INSERT INTO stylist_services (stylist_id, service_id, tenant_id, assigned_at)
SELECT st.id, sv.id, '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid, now()
FROM stylists st
CROSS JOIN services sv
WHERE st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
  AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
  AND st.full_name IN ('Carolina Rodríguez', 'Andrea Patiño', 'Lina Mejía', 'Juliana Ríos')
  AND NOT EXISTS (
    SELECT 1 FROM stylist_services ss
    WHERE ss.stylist_id = st.id AND ss.service_id = sv.id
  );

-- =====================================================================
-- 4. CUSTOMERS
-- =====================================================================
INSERT INTO customers (id, tenant_id, full_name, phone, email, birthday, accepts_marketing, is_active, created_at)
SELECT gen_random_uuid(), '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid, x.name, x.phone, x.email, x.birthday::date, x.marketing, true, now()
FROM (VALUES
  ('María González',      '+57 311 245 7782', 'mariag@gmail.com',     '1992-03-14', true),
  ('Valentina Castaño',   '+57 314 678 1290', 'valec@gmail.com',      '1988-07-09', true),
  ('Isabella Trujillo',   '+57 315 220 4471', 'isatru@gmail.com',     '1995-11-23', true),
  ('Daniela Ospina',      '+57 318 552 3344', 'danios@gmail.com',     '1997-02-04', false),
  ('Camila Restrepo',     '+57 313 778 9912', 'camires@gmail.com',    '1990-09-30', true),
  ('Andrea Patiño S.',    '+57 320 411 5678', 'andreaps@gmail.com',   '1993-05-18', true),
  ('Salomé Gutiérrez',    '+57 311 998 7654', 'salomeg@gmail.com',    '1986-12-01', false),
  ('Laura Bernal',        '+57 315 220 1133', 'laurab@gmail.com',     '2000-08-12', true),
  ('Juana Saldarriaga',   '+57 319 663 4477', 'juanasa@gmail.com',    '1984-06-25', true),
  ('Verónica Arango',     '+57 312 887 2210', 'veroar@gmail.com',     '1991-04-07', false),
  ('Mariana Vélez',       '+57 317 552 8899', 'marivel@gmail.com',    '1996-10-19', true),
  ('Manuela Lozano',      '+57 313 102 6655', 'manulo@gmail.com',     '1989-01-28', true),
  ('Sofía Hernández',     '+57 314 998 1122', 'sofiher@gmail.com',    '1994-11-11', true),
  ('Catalina Mora',       '+57 320 776 3344', 'catamo@gmail.com',     '1998-07-22', false),
  ('Diana Cárdenas',      '+57 318 224 9988', 'diacar@gmail.com',     '1985-03-09', true),
  ('Paula Quintero',      '+57 311 552 0099', 'pauqui@gmail.com',     '1993-09-17', true),
  ('Tatiana Mendoza',     '+57 316 778 4422', 'tatime@gmail.com',     '1995-01-05', true),
  ('Natalia Acevedo',     '+57 319 220 7711', 'natace@gmail.com',     '1987-08-30', false),
  ('Lorena Jiménez',      '+57 312 663 8855', 'lorjim@gmail.com',     '1990-12-12', true),
  ('Alejandra Buitrago',  '+57 317 998 3322', 'alebui@gmail.com',     '1996-04-21', true),
  ('Gabriela Salazar',    '+57 315 411 9966', 'gabsal@gmail.com',     '1992-06-15', false)
) AS x(name, phone, email, birthday, marketing)
WHERE NOT EXISTS (
  SELECT 1 FROM customers c
  WHERE c.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
    AND (c.full_name = x.name OR c.phone = x.phone)
);

-- =====================================================================
-- 5. APPOINTMENTS — 21 citas para 2026-06-03 (hora Colombia UTC-5)
-- =====================================================================
-- Cada cita: start UTC = fecha + hora local + 5h (porque UTC-5)
-- Ejemplo: 2026-06-03 09:00 Colombia = 2026-06-03 14:00 UTC

INSERT INTO appointments (
  id, tenant_id, customer_id, stylist_id, service_id,
  start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
  status, deposit_status, channel, created_at
)
SELECT
  gen_random_uuid(),
  '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
  cu.id, st.id, sv.id,
  -- start_at UTC: combine date + local time + add 5h offset
  (('2026-06-03 ' || x.hour_local || ':' || lpad(x.min_local::text, 2, '0') || ':00')::timestamp AT TIME ZONE 'America/Bogota') AS start_at,
  (('2026-06-03 ' || x.hour_local || ':' || lpad(x.min_local::text, 2, '0') || ':00')::timestamp AT TIME ZONE 'America/Bogota') + (sv.duration_minutes || ' minutes')::interval AS end_at,
  sv.price, 0.0, 0.0,
  1,  -- status: Confirmed
  0,  -- deposit_status: NotRequired
  0,  -- channel: Reception
  now()
FROM (VALUES
  -- Carolina Rodríguez (Estilista senior)
  ('María González',      'Carolina Rodríguez', 'Corte + cepillado',                 9,  0),
  ('Valentina Castaño',   'Carolina Rodríguez', 'Tinte raíz + corte',               10,  0),
  ('Daniela Ospina',      'Carolina Rodríguez', 'Alisado brasilero',                11,  0),
  ('Camila Restrepo',     'Carolina Rodríguez', 'Corte + cepillado',                13,  0),
  ('Sofía Hernández',     'Carolina Rodríguez', 'Mechas californianas',             15,  0),
  ('Paula Quintero',      'Carolina Rodríguez', 'Cepillado',                        17, 30),

  -- Andrea Patiño (Color & balayage)
  ('Isabella Trujillo',   'Andrea Patiño',      'Balayage + tratamiento',            9,  0),
  ('Andrea Patiño S.',    'Andrea Patiño',      'Color completo + corte',           11, 30),
  ('Mariana Vélez',       'Andrea Patiño',      'Retoque raíz',                     14,  0),
  ('Catalina Mora',       'Andrea Patiño',      'Decoloración + matiz',             15, 30),

  -- Lina Mejía (Manicure & pedicure)
  ('Laura Bernal',        'Lina Mejía',         'Manicure semipermanente',           9,  0),
  ('Juana Saldarriaga',   'Lina Mejía',         'Pedicure spa',                     10,  0),
  ('Verónica Arango',     'Lina Mejía',         'Pedicure + esmaltado',             11,  0),
  ('Salomé Gutiérrez',    'Lina Mejía',         'Manicure semipermanente',          12,  0),
  ('Manuela Lozano',      'Lina Mejía',         'Pedicure + esmaltado',             14,  0),
  ('Diana Cárdenas',      'Lina Mejía',         'Manicure semipermanente',          16,  0),
  ('Alejandra Buitrago',  'Lina Mejía',         'Pedicure spa',                     17, 30),

  -- Juliana Ríos (Cejas & pestañas)
  ('Tatiana Mendoza',     'Juliana Ríos',       'Depilación cejas',                 10,  0),
  ('Natalia Acevedo',     'Juliana Ríos',       'Lifting + tinte pestañas',         11, 30),
  ('Lorena Jiménez',      'Juliana Ríos',       'Extensiones pestañas pelo a pelo', 15,  0),
  ('Gabriela Salazar',    'Juliana Ríos',       'Cera bigote + cejas',              17,  0)
) AS x(customer_name, stylist_name, service_name, hour_local, min_local)
JOIN customers cu ON cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = x.customer_name
JOIN stylists st  ON st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = x.stylist_name
JOIN services sv  ON sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name      = x.service_name
WHERE NOT EXISTS (
  SELECT 1 FROM appointments a
  WHERE a.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
    AND a.customer_id = cu.id
    AND a.stylist_id  = st.id
    AND a.start_at = (('2026-06-03 ' || x.hour_local || ':' || lpad(x.min_local::text, 2, '0') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
);

-- =====================================================================
-- Resumen
-- =====================================================================
SELECT 'Servicios totales:' AS tipo, count(*)::text AS valor FROM services WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND is_active
UNION ALL SELECT 'Estilistas activos:', count(*)::text FROM stylists WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND status <> 2
UNION ALL SELECT 'Clientes activos:',   count(*)::text FROM customers WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND is_active
UNION ALL SELECT 'Citas 2026-06-03:',   count(*)::text FROM appointments WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid
   AND start_at >= ('2026-06-03 00:00:00'::timestamp AT TIME ZONE 'America/Bogota')
   AND start_at <  ('2026-06-04 00:00:00'::timestamp AT TIME ZONE 'America/Bogota');

COMMIT;
