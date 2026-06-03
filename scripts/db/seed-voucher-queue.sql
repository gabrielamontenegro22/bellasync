-- Pobla la cola de validación con vouchers variados para que la
-- recepcionista vea una pantalla rica como el mockup. Crea citas
-- nuevas en fechas/horarios libres, marca cada servicio asociado
-- como requires_deposit, y crea su voucher en estado Pending.
--
-- Distribución de urgencias (basado en now()):
--   - 2 urgent (cita dentro de 6h)
--   - 3 tomorrow (cita en 24-36h)
--   - 3 week (cita en 3-7 días)
--
-- Todos los vouchers quedan Pending para que se vean en la cola.

BEGIN;

-- =====================================================================
-- 1. Marcar varios servicios como requires_deposit
-- =====================================================================
UPDATE services
SET requires_deposit = true, deposit_percentage = 50.0, updated_at = now()
WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND name IN (
    'Mechas californianas',
    'Alisado brasilero',
    'Color completo + corte',
    'Decoloración + matiz',
    'Lifting + tinte pestañas',
    'Extensiones pestañas pelo a pelo'
  );

UPDATE services
SET requires_deposit = true, deposit_percentage = 30.0, updated_at = now()
WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND name = 'Manicure semipermanente';


-- =====================================================================
-- Helper inline: crear cita Pending + voucher Pending
-- (la duplicación es intencional — un SQL plano sin funciones)
-- =====================================================================

-- ── URGENT 1: Mariana Vélez · Mechas californianas · HOY +3h
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '3 hours',
        now() + interval '3 hours' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '2 hours 30 minutes'),
        now() - interval '14 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Mariana Vélez'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Carolina Rodríguez'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Mechas californianas'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Bancolombia', 'BC9924817', 'Mariana M. Vélez R.', '+57 317 552 8899',
    now() - interval '14 minutes', 0, now()
FROM new_appt na;


-- ── URGENT 2: Daniela Ospina · Alisado brasilero · HOY +5h
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '5 hours',
        now() + interval '5 hours' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '4 hours 30 minutes'),
        now() - interval '38 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Daniela Ospina'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Lina Mejía'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Alisado brasilero'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Nequi', 'NQ48201773', 'D. Ospina P.', '+57 318 552 3344',
    now() - interval '38 minutes', 0, now()
FROM new_appt na;


-- ── TOMORROW 1: Andrea Patiño S. · Color completo · +28h
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '28 hours',
        now() + interval '28 hours' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '27 hours 30 minutes'),
        now() - interval '62 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Andrea Patiño S.'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Andrea Patiño'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Color completo + corte'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Davivienda', 'DAV771928', 'Andrea Patiño Sierra', '+57 320 411 5678',
    now() - interval '62 minutes', 0, now()
FROM new_appt na;


-- ── TOMORROW 2: Salomé Gutiérrez · Decoloración + matiz · +32h
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '32 hours',
        now() + interval '32 hours' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '31 hours 30 minutes'),
        now() - interval '95 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Salomé Gutiérrez'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Carolina Rodríguez'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Decoloración + matiz'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Daviplata', 'DP9938174', 'S. Gutiérrez', '+57 311 998 7654',
    now() - interval '95 minutes', 0, now()
FROM new_appt na;


-- ── TOMORROW 3: Laura Bernal · Lifting pestañas · +34h
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '34 hours',
        now() + interval '34 hours' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '33 hours 30 minutes'),
        now() - interval '140 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Laura Bernal'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Juliana Ríos'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Lifting + tinte pestañas'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Bancolombia', 'BC2087719', 'S. Castaño Mejía', '+57 315 220 1133',
    now() - interval '140 minutes', 0, now()
FROM new_appt na;


-- ── WEEK 1: Verónica Arango · Manicure semipermanente · +3 días
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '3 days',
        now() + interval '3 days' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 30.0, sv.price * 0.3,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '3 days' - interval '30 minutes'),
        now() - interval '210 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Verónica Arango'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Lina Mejía'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Manicure semipermanente'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Nequi', 'NQ4471028', 'V. Arango T.', '+57 312 887 2210',
    now() - interval '210 minutes', 0, now()
FROM new_appt na;


-- ── WEEK 2: Tatiana Mendoza · Extensiones pestañas · +5 días
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, hold_expires_at, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        now() + interval '5 days',
        now() + interval '5 days' + (sv.duration_minutes || ' minutes')::interval,
        sv.price, 50.0, sv.price * 0.5,
        0, 1, 0,
        LEAST(now() + interval '3 hours', now() + interval '5 days' - interval '30 minutes'),
        now() - interval '285 minutes'
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Tatiana Mendoza'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Juliana Ríos'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Extensiones pestañas pelo a pelo'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id, na.deposit_amount,
    'Davivienda', 'DV5520017', 'T. Mendoza', '+57 316 778 4422',
    now() - interval '285 minutes', 0, now()
FROM new_appt na;


-- =====================================================================
-- Verificación
-- =====================================================================
SELECT
    c.full_name AS cliente,
    s.name AS servicio,
    to_char(a.start_at AT TIME ZONE 'America/Bogota', 'DD Mon HH24:MI') AS cita,
    EXTRACT(EPOCH FROM (a.start_at - now())) / 3600 AS horas_hasta_cita,
    v.bank,
    v.reported_amount,
    v.status AS voucher_status
FROM payment_vouchers v
JOIN appointments a ON v.appointment_id = a.id
JOIN customers c ON a.customer_id = c.id
JOIN services s ON a.service_id = s.id
WHERE v.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND v.status = 0
ORDER BY a.start_at;

COMMIT;
