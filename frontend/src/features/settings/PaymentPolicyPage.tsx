import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Clock, RotateCcw } from 'lucide-react'
import { extractApiError } from '@/lib/extractApiError'
import { getPaymentPolicy, updatePaymentPolicy, type PaymentPolicy } from '@/api/admin'
import {
  SettingsHeader, SettingsBlock, SaveBar, NumberRow,
} from './_primitives'

const KEY = 'payment-policy'

/**
 * `/configuracion/pagos` — la admin del salón configura cuánto tiempo
 * reservar un cupo cuando la cliente agenda con anticipo pendiente, y
 * hasta cuándo se devuelve el anticipo si la cliente cancela.
 *
 * Versión 2 del UX: los nombres técnicos del backend ("HoldDurationHours",
 * "HoldMinBeforeAppointmentMinutes", "MinAdvanceMinutes",
 * "CancellationWindowHours") se exponen con lenguaje de admin de salón
 * — "tiempo para enviar el comprobante", "cierre del cupo antes de la
 * cita", "anticipación mínima", "plazo para cancelar y recuperar el
 * anticipo".
 *
 * El resumen abajo recalcula dinámicamente ejemplos numéricos con los
 * valores actuales del form (no del backend) para que la admin vea de
 * inmediato qué pasaría si cambia un slider. Antes de guardar, los
 * ejemplos ya reflejan la decisión.
 *
 * Bloques visuales:
 *  1. "Reserva del cupo" — los 3 tiempos que afectan agendar/pagar.
 *  2. "Cancelaciones" — solo la ventana de devolución.
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
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl space-y-5">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Política de pagos"
          desc="Decidí cuánto tiempo le das a tus clientas para enviar el comprobante de anticipo, y hasta cuándo pueden cancelar sin perderlo. Cada salón es distinto — un spa relajado puede dar 24h, una peluquería express solo 1h."
        />

        {/* ── BLOQUE 1: Reserva del cupo ─────────────────────────── */}
        <SettingsBlock icon={<Clock size={16} />} title="Reserva del cupo">
          <NumberRow
            label="Tiempo para enviar el comprobante"
            hint="Después de agendar, la cliente tiene este tiempo para mandar la foto del pago. Si no llega, el cupo se libera automáticamente para otra cliente."
            suffix="horas"
            value={form.holdDurationHours}
            onChange={(v) => setField('holdDurationHours', v)}
            min={1}
            max={48}
          />
          <NumberRow
            label="Cerrar la reserva antes de la cita"
            hint="Si la cita está muy cerca, el cupo se libera con este margen aunque no se cumpla el tiempo de arriba. Evita reservar hasta el último segundo."
            suffix="min antes"
            value={form.holdMinBeforeAppointmentMinutes}
            onChange={(v) => setField('holdMinBeforeAppointmentMinutes', v)}
            min={0}
            max={240}
            step={5}
          />
          <NumberRow
            label="Anticipación mínima para agendar online"
            hint="Las clientas no pueden reservar con menos de este tiempo. Vos sí podés agendar walk-ins desde el panel."
            suffix="min antes"
            value={form.minAdvanceMinutes}
            onChange={(v) => setField('minAdvanceMinutes', v)}
            min={0}
            max={1440}
            step={5}
          />
        </SettingsBlock>

        {/* ── BLOQUE 2: Cancelaciones ──────────────────────────── */}
        <SettingsBlock icon={<RotateCcw size={16} />} title="Cancelaciones con anticipo pagado">
          <NumberRow
            label="Plazo para cancelar y recuperar el anticipo"
            hint="Si cancelan con esta anticipación o más, se devuelve el anticipo. Si cancelan más sobre la hora, se considera perdido (vos podés cambiar la decisión caso por caso)."
            suffix="horas antes"
            value={form.cancellationWindowHours}
            onChange={(v) => setField('cancellationWindowHours', v)}
            min={0}
            max={168}
          />
        </SettingsBlock>

        {/* Resumen dinámico con ejemplos concretos */}
        <PolicySummary policy={form} />
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

/**
 * Resumen visual que traduce los 4 parámetros a 3 escenarios concretos
 * con números reales. La admin lee "si una clienta hace X, le pasa Y"
 * en vez de tener que combinar las reglas en su cabeza.
 *
 * Cubre 3 casos típicos:
 *  - Agenda con bastante anticipación → siempre tiene el hold completo.
 *  - Agenda con poco tiempo → se le acorta automáticamente.
 *  - Cancela con anticipo pagado → devolución / pérdida según ventana.
 */
function PolicySummary({ policy }: { policy: PaymentPolicy }) {
  const hold = policy.holdDurationHours          // horas
  const margin = policy.holdMinBeforeAppointmentMinutes  // min
  const minAdvance = policy.minAdvanceMinutes    // min
  const cancelWin = policy.cancellationWindowHours  // horas

  // Punto de cruce: si la cita está a más de (hold + margin) → gana hold.
  // Si está más cerca → gana margen.
  const crossoverMin = hold * 60 + margin

  return (
    <div className="rounded-xl border border-warm-150 bg-warm-50/60 p-4 space-y-3 text-[13px] text-warm-700 leading-relaxed">
      <div className="text-[11px] uppercase tracking-wide text-warm-500 font-medium">
        Cómo va a funcionar para tus clientas
      </div>

      <div className="space-y-2.5">
        {/* CASO 1: cita lejana (mañana) */}
        <ExampleRow
          emoji="📅"
          title={
            <>
              <strong>Agenda hoy para mañana:</strong>{' '}
              tiene <strong className="tabular-nums">{fmtDuration(hold * 60)}</strong>{' '}
              para enviar el comprobante. Si no llega, el cupo se libera solo.
            </>
          }
        />

        {/* CASO 2: cita cercana (límite del crossover) */}
        <ExampleRow
          emoji="⏰"
          title={
            <>
              <strong>Agenda con poco tiempo</strong> (cita en menos de{' '}
              <span className="tabular-nums">{fmtDuration(crossoverMin)}</span>):
              tiene menos tiempo — el cupo se cierra siempre{' '}
              <strong className="tabular-nums">{margin} min antes</strong> de la cita.
            </>
          }
        />

        {/* CASO 3: anticipación mínima */}
        <ExampleRow
          emoji="🚫"
          title={
            <>
              <strong>No pueden agendar online</strong> con menos de{' '}
              <strong className="tabular-nums">{minAdvance} min</strong> de anticipación.
              {' '}Vos sí podés crear walk-ins desde el panel.
            </>
          }
        />

        {/* Separador visual entre reserva y cancelaciones */}
        <div className="h-px bg-warm-200/70 my-1" />

        {cancelWin === 0 ? (
          /* Política estricta: ventana 0 = nunca devolver */
          <ExampleRow
            emoji="❌"
            title={
              <>
                <strong>Política estricta:</strong> el anticipo{' '}
                <strong>nunca se devuelve automáticamente</strong> al
                cancelar. Vos o tu recepción (con permiso) pueden cambiar
                la decisión caso por caso desde el modal de cancelar.
              </>
            }
          />
        ) : (
          <>
            {/* CASO 4: cancelación con devolución */}
            <ExampleRow
              emoji="✅"
              title={
                <>
                  <strong>Si ya pagaron y cancelan</strong> con{' '}
                  <strong className="tabular-nums">{cancelWin}h o más</strong>{' '}
                  de anticipación → se le <strong>devuelve el anticipo</strong>.
                </>
              }
            />

            {/* CASO 5: cancelación tardía */}
            <ExampleRow
              emoji="❌"
              title={
                <>
                  <strong>Si cancelan más sobre la hora</strong> (menos de{' '}
                  <strong className="tabular-nums">{cancelWin}h</strong>) →
                  el anticipo <strong>se pierde</strong>. Vos o tu recepción
                  (con permiso) pueden cambiar la decisión caso por caso.
                </>
              }
            />
          </>
        )}
      </div>
    </div>
  )
}

function ExampleRow({ emoji, title }: { emoji: string; title: React.ReactNode }) {
  return (
    <div className="flex gap-2.5">
      <div className="shrink-0 text-[15px] leading-[1.4]" aria-hidden>{emoji}</div>
      <div className="flex-1">{title}</div>
    </div>
  )
}

/**
 * Formatea minutos a "Xh" / "Xh Ymin" / "X min" según el caso. Para que
 * los ejemplos del resumen lean naturales independiente de qué slider
 * se mueva.
 */
function fmtDuration(totalMinutes: number): string {
  if (totalMinutes <= 0) return '0 min'
  const h = Math.floor(totalMinutes / 60)
  const m = totalMinutes % 60
  if (h === 0) return `${m} min`
  if (m === 0) return `${h}h`
  return `${h}h ${m}min`
}
