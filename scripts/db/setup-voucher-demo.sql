-- Setup de datos para validar el flujo Voucher + Payment integrado.
-- Tenant: Bella Spa Neiva
--
-- Crea:
--   1. Servicio Balayage marcado con anticipo 50%
--   2. Cita JUEVES 4 jun 10am — Sofía Hernández + Andrea Patiño + Balayage
--      con voucher YA VALIDATED (la recepcionista ya aprobó el anticipo).
--      → En el agenda debe verse "Total $220k / − Anticipo Validado $110k / Falta $110k"
--   3. Cita JUEVES 4 jun 14:00 — Catalina Mora + Andrea Patiño + Balayage
--      con voucher en estado PENDING (la cliente subió comprobante,
--      la recepcionista no ha decidido).
--      → En el agenda debe verse "Total $220k / − Anticipo Esperando $110k / Esperando anticipo"
--      → En /configuracion/validacion debe aparecer en la cola.
--      → Cuando la recepcionista valida, la cita pasa a Confirmed y
--        el cuadro cambia a "Validado".

BEGIN;

-- =====================================================================
-- 1. Marcar Balayage como servicio con anticipo 50%
-- =====================================================================
UPDATE services
SET requires_deposit = true,
    deposit_percentage = 50.0,
    updated_at = now()
WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND name = 'Balayage + tratamiento';

-- =====================================================================
-- 2. Cita VALIDADA — Sofía Hernández, 4 jun 10:00
-- =====================================================================
-- Insertamos directamente en estado Confirmed (status=1) +
-- DepositStatus = Validated (2). En condiciones normales el flujo sería
-- Pending → Validated, pero acá saltamos al estado final.
WITH new_appt AS (
    INSERT INTO appointments (
        id, tenant_id, customer_id, stylist_id, service_id,
        start_at, end_at, price_snapshot, deposit_percentage, deposit_amount,
        status, deposit_status, channel, created_at
    )
    SELECT
        gen_random_uuid(),
        '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
        cu.id, st.id, sv.id,
        ('2026-06-04 10:00:00'::timestamp AT TIME ZONE 'America/Bogota'),
        ('2026-06-04 10:00:00'::timestamp AT TIME ZONE 'America/Bogota') + (sv.duration_minutes || ' minutes')::interval,
        sv.price,
        50.0,
        sv.price * 0.5,
        1,  -- Confirmed
        2,  -- Validated
        0,  -- Reception
        now()
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Sofía Hernández'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Andrea Patiño'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Balayage + tratamiento'
    RETURNING id, deposit_amount
)
INSERT INTO payment_vouchers (
    id, tenant_id, appointment_id, reported_amount,
    bank, reference_number, sender_name, sender_phone,
    received_at, status, decided_at, decision_notes, created_at
)
SELECT
    gen_random_uuid(),
    '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid,
    na.id,
    na.deposit_amount,
    'Bancolombia',
    'TRF-891234',
    'Sofía Hernández',
    '+57 314 998 1122',
    now() - interval '2 hours',
    1,  -- Validated
    now() - interval '1 hour',
    'Pago verificado contra extracto Bancolombia.',
    now()
FROM new_appt na;

-- =====================================================================
-- 3. Cita PENDING — Catalina Mora, 4 jun 14:00 — con voucher Pending
-- =====================================================================
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
        ('2026-06-04 14:00:00'::timestamp AT TIME ZONE 'America/Bogota'),
        ('2026-06-04 14:00:00'::timestamp AT TIME ZONE 'America/Bogota') + (sv.duration_minutes || ' minutes')::interval,
        sv.price,
        50.0,
        sv.price * 0.5,
        0,  -- Pending
        1,  -- AwaitingPayment
        0,
        -- hold = ahora + 3h o startAt - 30min, el que llegue primero
        LEAST(
          now() + interval '3 hours',
          ('2026-06-04 14:00:00'::timestamp AT TIME ZONE 'America/Bogota') - interval '30 minutes'
        ),
        now()
    FROM customers cu, stylists st, services sv
    WHERE cu.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND cu.full_name = 'Catalina Mora'
      AND st.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND st.full_name = 'Andrea Patiño'
      AND sv.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'::uuid AND sv.name = 'Balayage + tratamiento'
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
    na.id,
    na.deposit_amount,
    'Nequi',
    'M2026060312345',
    'Catalina Mora',
    '+57 320 776 3344',
    now() - interval '30 minutes',
    0,  -- Pending
    now()
FROM new_appt na;

-- =====================================================================
-- Verificación
-- =====================================================================
SELECT
    c.full_name AS cliente,
    to_char(a.start_at AT TIME ZONE 'America/Bogota', 'DD Mon HH24:MI') AS horario,
    a.status AS appt_status,
    a.deposit_status AS dep_status,
    v.status AS voucher_status,
    a.price_snapshot,
    a.deposit_amount
FROM appointments a
JOIN customers c ON a.customer_id = c.id
LEFT JOIN payment_vouchers v ON v.appointment_id = a.id
WHERE a.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND a.start_at::date = '2026-06-04'
ORDER BY a.start_at;

COMMIT;
