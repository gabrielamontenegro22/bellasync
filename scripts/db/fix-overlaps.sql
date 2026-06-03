-- Arregla el overlap detectado en Carolina Rodríguez del 2026-06-03:
-- Daniela Ospina estaba a 11:00 pero pisaba con Valentina Castaño (10-11:30).
-- La movemos a 11:30 para que arranque cuando termina Valentina.

UPDATE appointments
SET start_at = ('2026-06-03 11:30:00'::timestamp AT TIME ZONE 'America/Bogota'),
    end_at   = ('2026-06-03 13:00:00'::timestamp AT TIME ZONE 'America/Bogota'),
    updated_at = now()
WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND customer_id = (
    SELECT id FROM customers
    WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
      AND full_name = 'Daniela Ospina'
  )
  AND stylist_id = (
    SELECT id FROM stylists
    WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
      AND full_name = 'Carolina Rodríguez'
  )
  AND start_at >= ('2026-06-03 00:00:00'::timestamp AT TIME ZONE 'America/Bogota')
  AND start_at <  ('2026-06-04 00:00:00'::timestamp AT TIME ZONE 'America/Bogota');

-- También damos un poco de gap entre Daniela (termina 13:00) y Camila Restrepo (empieza 13:00):
-- Movemos Camila a 13:30 para que no queden pegadas back-to-back.
UPDATE appointments
SET start_at = ('2026-06-03 13:30:00'::timestamp AT TIME ZONE 'America/Bogota'),
    end_at   = ('2026-06-03 14:30:00'::timestamp AT TIME ZONE 'America/Bogota'),
    updated_at = now()
WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND customer_id = (
    SELECT id FROM customers
    WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
      AND full_name = 'Camila Restrepo'
  )
  AND stylist_id = (
    SELECT id FROM stylists
    WHERE tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
      AND full_name = 'Carolina Rodríguez'
  )
  AND start_at >= ('2026-06-03 00:00:00'::timestamp AT TIME ZONE 'America/Bogota')
  AND start_at <  ('2026-06-04 00:00:00'::timestamp AT TIME ZONE 'America/Bogota');

-- Verificación: re-correr la detección de overlaps debería devolver 0 filas
SELECT 'Overlaps restantes:' AS verificacion, COUNT(*) AS cantidad
FROM appointments a1
JOIN appointments a2 ON a1.stylist_id = a2.stylist_id AND a1.id < a2.id
WHERE a1.tenant_id = '56c56a9f-1f60-4bca-8a37-0c5fc7edd112'
  AND a1.start_at >= ('2026-06-03 00:00:00'::timestamp AT TIME ZONE 'America/Bogota')
  AND a1.start_at <  ('2026-06-04 00:00:00'::timestamp AT TIME ZONE 'America/Bogota')
  AND a1.start_at < a2.end_at
  AND a2.start_at < a1.end_at;
