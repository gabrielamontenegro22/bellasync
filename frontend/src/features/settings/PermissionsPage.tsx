import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shield, Scissors, Eye, Settings } from 'lucide-react'
import { extractApiError } from '@/lib/extractApiError'
import {
  getReceptionPermissions, updateReceptionPermissions,
  type ReceptionPermissions,
} from '@/api/admin'
import {
  SettingsHeader, SettingsBlock, SaveBar, SettingsField,
  inputCls, ToggleRow,
} from './_primitives'
import { cls } from '@/lib/cls'

const KEY = 'receptionPermissions'

const DEFAULTS: ReceptionPermissions = {
  expenseCapCop: 100_000,
  canCancelWithMoney: true,
  canCloseCash: false,
  canEditStylists: false,
  canEditServices: false,
  canEditInventory: false,
  canViewReports: false,
  canViewCommissions: false,
  canEditSchedule: false,
  canEditPaymentPolicy: false,
  canEditSalonInfo: false,
}

/**
 * `/configuracion/permisos` — la admin decide qué puede hacer recepción
 * sin pedirle autorización para cada cosa. Cada salón es distinto: en
 * unos recepción es solo "agendar y cobrar"; en otros maneja plata,
 * equipo, y configuración del local.
 *
 * 4 bloques temáticos:
 *  1. Operación diaria — cap egresos, cancelar con plata, cerrar caja
 *  2. Catálogo del salón — estilistas, servicios
 *  3. Información sensible — reportes, comisiones (KPIs financieros)
 *  4. Configuración del salón — horario, política de pagos, info pública
 *
 * Los handlers de cada operación leen estos settings en cada request.
 */
export function PermissionsPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: [KEY],
    queryFn: getReceptionPermissions,
  })

  const initial: ReceptionPermissions = useMemo(() => data ?? DEFAULTS, [data])

  const [form, setForm] = useState<ReceptionPermissions>(initial)
  // El cap se maneja como string para permitir vacío (= null = sin límite)
  // y "0" (= bloqueado). Se convierte a number/null al guardar.
  const [capText, setCapText] = useState<string>(() =>
    initial.expenseCapCop === null ? '' : String(initial.expenseCapCop),
  )
  useEffect(() => {
    setForm(initial)
    setCapText(initial.expenseCapCop === null ? '' : String(initial.expenseCapCop))
  }, [initial])

  const [saved, setSaved] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: (req: ReceptionPermissions) => updateReceptionPermissions(req),
    onSuccess: (r) => {
      qc.setQueryData([KEY], r)
      qc.invalidateQueries({ queryKey: [KEY] })
      setSubmitError(null)
      setSaved(true)
    },
    onError: (e) => setSubmitError(extractApiError(e, 'No se pudo guardar.')),
  })

  useEffect(() => {
    if (!saved) return
    const t = setTimeout(() => setSaved(false), 3000)
    return () => clearTimeout(t)
  }, [saved])

  const setField = <K extends keyof ReceptionPermissions>(k: K, v: ReceptionPermissions[K]) => {
    setForm(f => ({ ...f, [k]: v }))
    setSaved(false); setSubmitError(null)
  }

  // Construye el form actual con el cap parseado.
  const formNow: ReceptionPermissions = {
    ...form,
    expenseCapCop: capText.trim() === ''
      ? null
      : Math.max(0, parseInt(capText.replace(/[^0-9]/g, '')) || 0),
  }
  const isDirty = JSON.stringify(formNow) !== JSON.stringify(initial)

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl space-y-5">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Permisos del equipo"
          desc="Definí qué puede hacer la recepción sin pedirte autorización. Si tu recepcionista es de confianza y maneja plata del salón, dale más permisos. Si recién empieza, dejalo restringido."
        />

        {/* ── BLOQUE 1: Operación diaria ─────────────────────────── */}
        <SettingsBlock icon={<Shield size={16} />} title="Operación diaria">
          <SettingsField
            label="Tope para registrar egresos"
            hint={
              <>
                Dejá <strong>vacío</strong> para sin límite (recepción confiable),
                escribí <strong>0</strong> para que NO pueda registrar egresos,
                o poné un monto en COP. La admin nunca tiene tope.
              </>
            }
          >
            <div className="relative max-w-[200px]">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-warm-400 text-[14px]">$</span>
              <input
                type="text"
                inputMode="numeric"
                value={capText}
                onChange={(e) => { setCapText(e.target.value); setSaved(false); setSubmitError(null) }}
                placeholder="Sin límite"
                className={cls(inputCls, 'pl-7 tabular-nums')}
              />
            </div>
          </SettingsField>

          <ToggleRow
            title="Puede cancelar citas con anticipo cobrado"
            desc="Cuando la cliente avisa que no puede ir y ya pagó. Recepción debe escribir motivo obligatorio (devolver / aplicar a otra cita / política estricta) para que vos sepas qué hacer con el dinero después."
            on={form.canCancelWithMoney}
            onChange={(v) => setField('canCancelWithMoney', v)}
          />

          <ToggleRow
            title="Puede firmar el cierre de caja del día"
            desc="Si pasás por el salón cada noche, dejalo OFF (vos firmás). Si no, activá para que tu recepción cierre el día (queda registrado quién firmó)."
            on={form.canCloseCash}
            onChange={(v) => setField('canCloseCash', v)}
          />
        </SettingsBlock>

        {/* ── BLOQUE 2: Catálogo ─────────────────────────────────── */}
        <SettingsBlock icon={<Scissors size={16} />} title="Catálogo del salón">
          <ToggleRow
            title="Puede crear y editar estilistas"
            desc="Incluye agregar al equipo, editar datos de contacto, desactivar y marcar días libres / vacaciones. Si está OFF, recepción solo puede VER el equipo."
            on={form.canEditStylists}
            onChange={(v) => setField('canEditStylists', v)}
          />

          <ToggleRow
            title="Puede crear y editar servicios"
            desc="Crear servicios nuevos, editar precios, duración, política de anticipo. Activá si recepción coordina el catálogo. Cambiar precios sin admin requiere mucha confianza."
            on={form.canEditServices}
            onChange={(v) => setField('canEditServices', v)}
          />

          <ToggleRow
            title="Puede llevar el inventario"
            desc="Crear productos del salón (tintes, esmaltes, accesorios), editar costos y stock mínimo, y registrar entradas/salidas/ajustes. Activá si recepción recibe proveedores y descuenta uso del día."
            on={form.canEditInventory}
            onChange={(v) => setField('canEditInventory', v)}
          />
        </SettingsBlock>

        {/* ── BLOQUE 3: Información sensible ─────────────────────── */}
        <SettingsBlock icon={<Eye size={16} />} title="Información sensible">
          <ToggleRow
            title="Puede ver Reportes"
            desc="Facturación del salón, KPIs financieros, tendencias. Es información de negocio sensible — recepción típicamente no la necesita para su trabajo diario."
            on={form.canViewReports}
            onChange={(v) => setField('canViewReports', v)}
          />

          <ToggleRow
            title="Puede ver Comisiones"
            desc="Cuánto le toca a cada estilista, liquidaciones del mes. También información sensible — si lo activás, recepción ve sueldos del equipo."
            on={form.canViewCommissions}
            onChange={(v) => setField('canViewCommissions', v)}
          />
        </SettingsBlock>

        {/* ── BLOQUE 4: Configuración del salón ──────────────────── */}
        <SettingsBlock icon={<Settings size={16} />} title="Configuración del salón">
          <ToggleRow
            title="Puede editar el horario del salón"
            desc="Días que abre, franjas horarias, hora de almuerzo, festivos. Útil activar si recepción ajusta horarios estacionales sin pedirte cada cambio."
            on={form.canEditSchedule}
            onChange={(v) => setField('canEditSchedule', v)}
          />

          <ToggleRow
            title="Puede editar la política de pagos"
            desc="Cuánto tiempo el cupo queda reservado esperando anticipo, anticipación mínima para agendar. Son decisiones de negocio."
            on={form.canEditPaymentPolicy}
            onChange={(v) => setField('canEditPaymentPolicy', v)}
          />

          <ToggleRow
            title="Puede editar la información pública del salón"
            desc="Nombre, dirección, teléfono, logo, descripción que ven las clientas en el portal de booking. Cambios acá afectan la imagen del salón."
            on={form.canEditSalonInfo}
            onChange={(v) => setField('canEditSalonInfo', v)}
          />
        </SettingsBlock>
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        saving={mut.isPending}
        error={submitError}
        onSave={() => mut.mutate(formNow)}
        onDiscard={() => {
          setForm(initial)
          setCapText(initial.expenseCapCop === null ? '' : String(initial.expenseCapCop))
          setSaved(false); setSubmitError(null)
        }}
      />
    </div>
  )
}
