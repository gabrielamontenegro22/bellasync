import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Shield } from 'lucide-react'
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

/**
 * `/configuracion/permisos` — la admin del salón ajusta qué puede
 * hacer recepción sin pedirle autorización para cada cosa.
 *
 * Cada salón es distinto: en uno la recepcionista compra los tintes y
 * cierra caja porque la admin no pasa por el local; en otro recepción
 * solo agenda y cobra. Esta página es donde la admin afina esos
 * comportamientos según su nivel de confianza con su equipo.
 *
 * Los handlers de Application leen estos settings en cada operación
 * (RegisterExpense, CancelAppointment, CreateCashClosing) y rechazan
 * con 403 si no aplican.
 */
export function PermissionsPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: [KEY],
    queryFn: getReceptionPermissions,
  })

  const initial: ReceptionPermissions = useMemo(
    () => data ?? {
      expenseCapCop: 100_000,
      canCancelWithMoney: true,
      canCloseCash: false,
    },
    [data],
  )

  const [form, setForm] = useState<ReceptionPermissions>(initial)
  // Manejamos el input del cap como string para que el usuario pueda
  // tipear, borrar todo (= sin límite = null), o poner 0 (= bloqueado).
  // Convertimos a number/null al guardar.
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
      // Invalidamos también el modal de egresos para que tome el cap nuevo.
      qc.invalidateQueries({ queryKey: ['receptionPermissions'] })
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

  // Construye el form actual leyendo capText. Compara contra initial
  // para detectar cambios sin guardar.
  const formNow: ReceptionPermissions = {
    expenseCapCop: capText.trim() === '' ? null : Math.max(0, parseInt(capText.replace(/[^0-9]/g, '')) || 0),
    canCancelWithMoney: form.canCancelWithMoney,
    canCloseCash: form.canCloseCash,
  }
  const isDirty = JSON.stringify(formNow) !== JSON.stringify(initial)

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  // Helper para describir lo que está configurado en lenguaje humano.
  // Aparece arriba del SaveBar para que la admin vea el impacto antes
  // de guardar.
  const capDesc = formNow.expenseCapCop === null
    ? 'sin límite (puede registrar lo que sea)'
    : formNow.expenseCapCop === 0
      ? 'no puede registrar egresos'
      : `hasta $${formNow.expenseCapCop.toLocaleString('es-CO')} COP por egreso`

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Permisos del equipo"
          desc="Definí qué puede hacer la recepción sin pedirte autorización. Si tu recepcionista es de confianza y maneja plata del salón, dale más permisos. Si recién empieza, dejalo restringido."
        />

        <SettingsBlock icon={<Shield size={16} />} title="Recepción">
          {/* Cap de egresos */}
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
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-warm-400 text-[14px]">
                $
              </span>
              <input
                type="text"
                inputMode="numeric"
                value={capText}
                onChange={(e) => {
                  setCapText(e.target.value)
                  setSaved(false)
                  setSubmitError(null)
                }}
                placeholder="Sin límite"
                className={cls(inputCls, 'pl-7 tabular-nums')}
              />
            </div>
          </SettingsField>

          {/* Cancelar citas con plata */}
          <ToggleRow
            title="Puede cancelar citas con anticipo cobrado"
            desc="Cuando la cliente avisa que no puede ir y ya pagó. Recepción debe escribir un motivo obligatorio (devolver / aplicar a otra cita / política estricta) para que vos sepas qué hacer con el dinero después."
            on={form.canCancelWithMoney}
            onChange={(v) => {
              setForm(f => ({ ...f, canCancelWithMoney: v }))
              setSaved(false); setSubmitError(null)
            }}
          />

          {/* Cerrar caja */}
          <ToggleRow
            title="Puede firmar el cierre de caja del día"
            desc="Si pasás por el salón cada noche, dejalo OFF (vos firmás). Si no, activá para que tu recepción cierre el día (igual queda registrado 'Cerrado por X' en el historial)."
            on={form.canCloseCash}
            onChange={(v) => {
              setForm(f => ({ ...f, canCloseCash: v }))
              setSaved(false); setSubmitError(null)
            }}
          />
        </SettingsBlock>

        {/* Resumen humano */}
        <div className="mt-2 rounded-xl border border-warm-150 bg-warm-50/60 p-4 text-[12.5px] text-warm-700 leading-relaxed">
          <strong className="text-warm-800">Cómo va a quedar:</strong> tu recepción
          podrá registrar egresos {capDesc};{' '}
          {formNow.canCancelWithMoney
            ? 'cancelar citas con anticipo (con nota explicativa obligatoria)'
            : 'NO podrá cancelar citas con anticipo (deberá pedirte a vos)'};
          {' '}y{' '}
          {formNow.canCloseCash
            ? 'firmar el cierre de caja del día.'
            : 'NO podrá firmar el cierre de caja (vos lo hacés).'}
        </div>
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
