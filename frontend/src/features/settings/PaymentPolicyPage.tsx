import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Clock } from 'lucide-react'
import { extractApiError } from '@/lib/extractApiError'
import { getPaymentPolicy, updatePaymentPolicy, type PaymentPolicy } from '@/api/admin'
import {
  SettingsHeader, SettingsBlock, SaveBar, NumberRow,
} from './_primitives'

const KEY = 'payment-policy'

/**
 * `/configuracion/pagos` — la admin del salón configura cuánto tiempo
 * reservar un cupo cuando la cliente agenda con anticipo pendiente.
 *
 * Mantiene los mismos 3 parámetros del backend
 * (holdDurationHours, holdMinBeforeAppointmentMinutes, minAdvanceMinutes)
 * pero presenta el formulario al estilo del mockup config-servicios:
 * eyebrow + título serif gigante + bloque único "Tiempos y reglas"
 * con NumberStepper en cada fila + SaveBar abajo.
 *
 * El BackgroundService cancela citas con hold vencido cada 5 min, así
 * que cambios acá aplican casi de inmediato.
 */
export function PaymentPolicyPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: [KEY],
    queryFn: getPaymentPolicy,
  })

  const initial: PaymentPolicy = useMemo(
    () => data ?? {
      holdDurationHours: 3,
      holdMinBeforeAppointmentMinutes: 30,
      minAdvanceMinutes: 30,
      cancellationWindowHours: 2,
    },
    [data],
  )

  const [form, setForm] = useState<PaymentPolicy>(initial)
  useEffect(() => { setForm(initial) }, [initial])

  const [saved, setSaved] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: (req: PaymentPolicy) => updatePaymentPolicy(req),
    onSuccess: (r) => {
      qc.setQueryData([KEY], r)
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

  const isDirty = JSON.stringify(form) !== JSON.stringify(initial)

  const setField = <K extends keyof PaymentPolicy>(k: K, v: PaymentPolicy[K]) => {
    setForm(f => ({ ...f, [k]: v }))
    setSaved(false)
    setSubmitError(null)
  }

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Política de pagos"
          desc="Cuánto tiempo reservas el cupo cuando una cliente agenda con anticipo pendiente. Si no envía comprobante en este tiempo, la cita se cancela sola y el cupo queda libre para otra clienta."
        />

        <SettingsBlock icon={<Clock size={16} />} title="Tiempos y reglas">
          <NumberRow
            label="Tiempo máximo del cupo reservado"
            suffix="horas"
            value={form.holdDurationHours}
            onChange={(v) => setField('holdDurationHours', v)}
            min={1}
            max={48}
          />
          <NumberRow
            label="Margen antes de la cita"
            suffix="min antes"
            value={form.holdMinBeforeAppointmentMinutes}
            onChange={(v) => setField('holdMinBeforeAppointmentMinutes', v)}
            min={0}
            max={240}
            step={5}
          />
          <NumberRow
            label="Anticipación mínima para agendar"
            suffix="min antes"
            value={form.minAdvanceMinutes}
            onChange={(v) => setField('minAdvanceMinutes', v)}
            min={0}
            max={1440}
            step={5}
          />
          <NumberRow
            label="Ventana para cancelar con devolución de anticipo"
            suffix="horas antes"
            value={form.cancellationWindowHours}
            onChange={(v) => setField('cancellationWindowHours', v)}
            min={0}
            max={168}
          />
        </SettingsBlock>

        {/* Resumen actual destacado */}
        <div className="mt-2 rounded-xl border border-warm-150 bg-warm-50/60 p-4 text-[12.5px] text-warm-700 leading-relaxed space-y-2">
          <p>
            <strong className="text-warm-800">Cómo va a quedar:</strong> la cliente que
            agende un balayage hoy a las 14:00 (anticipo pendiente) tiene hasta{' '}
            <strong className="tabular-nums">{form.holdDurationHours}h</strong> para enviar
            el comprobante, o hasta{' '}
            <strong className="tabular-nums">{form.holdMinBeforeAppointmentMinutes} min</strong>{' '}
            antes de la cita (lo que llegue primero). Las clientas no pueden agendar con
            menos de{' '}
            <strong className="tabular-nums">{form.minAdvanceMinutes} min</strong> de
            anticipación.
          </p>
          <p>
            <strong className="text-warm-800">Cancelación con devolución:</strong>{' '}
            si la cliente ya pagó el anticipo y cancela{' '}
            <strong className="tabular-nums">
              hasta {form.cancellationWindowHours}h antes
            </strong>{' '}
            de la cita, se le devuelve. Si cancela dentro de esa ventana, el anticipo se
            considera perdido (vos o tu recepción con permiso pueden override caso por caso).
          </p>
        </div>
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        saving={mut.isPending}
        error={submitError}
        onSave={() => mut.mutate(form)}
        onDiscard={() => { setForm(initial); setSaved(false); setSubmitError(null) }}
      />
    </div>
  )
}
