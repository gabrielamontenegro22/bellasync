import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Clock, Wallet } from 'lucide-react'
import { Button, Card, Input } from '@/components/ui'
import { extractApiError } from '@/lib/extractApiError'
import { getPaymentPolicy, updatePaymentPolicy, type PaymentPolicy } from '@/api/admin'

const KEY = 'payment-policy'

/**
 * `/configuracion/pagos` — la admin del salón configura cuánto tiempo
 * reservar un cupo cuando la cliente agenda con anticipo pendiente.
 *
 * Valores que afectan:
 *  - holdDurationHours: máximo tiempo que el cupo queda esperando pago.
 *  - holdMinBeforeAppointmentMinutes: cuándo el hold deja de aplicar
 *    (cerca de la hora de la cita, no tiene sentido seguir esperando).
 *  - minAdvanceMinutes: cuánto antes una cliente puede agendar
 *    (evita que pidan citas "para ahora mismo" sin tiempo de prepararse).
 *
 * Estos tres parámetros antes eran globales del SaaS (appsettings.json);
 * ahora viven en columnas del Tenant y este formulario los edita.
 *
 * El BackgroundService cancela citas con hold vencido cada 5 min, así
 * que cambios acá aplican casi de inmediato a citas que ya estén creadas.
 */
export function PaymentPolicyPage() {
  const qc = useQueryClient()
  const { data, isLoading, error } = useQuery({
    queryKey: [KEY],
    queryFn: getPaymentPolicy,
  })

  // Form state — se inicializa con la data del backend cuando llega.
  const [form, setForm] = useState<PaymentPolicy>({
    holdDurationHours: 3,
    holdMinBeforeAppointmentMinutes: 30,
    minAdvanceMinutes: 30,
  })
  const [savedMessage, setSavedMessage] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  // Hidratar cuando llega data
  useEffect(() => {
    if (data) setForm(data)
  }, [data])

  const save = useMutation({
    mutationFn: (req: PaymentPolicy) => updatePaymentPolicy(req),
    onSuccess: (saved) => {
      qc.setQueryData([KEY], saved)
      setSubmitError(null)
      setSavedMessage('Política guardada. Las próximas citas la usarán.')
      // El mensaje desaparece después de 4s para no quedar pegado.
      setTimeout(() => setSavedMessage(null), 4000)
    },
    onError: (e) => setSubmitError(extractApiError(e, 'No se pudo guardar.')),
  })

  const isDirty = !!data && (
    data.holdDurationHours !== form.holdDurationHours
    || data.holdMinBeforeAppointmentMinutes !== form.holdMinBeforeAppointmentMinutes
    || data.minAdvanceMinutes !== form.minAdvanceMinutes
  )

  function submit() {
    setSubmitError(null)
    save.mutate(form)
  }

  function reset() {
    if (data) setForm(data)
    setSubmitError(null)
  }

  return (
    <div className="p-5 lg:p-8 max-w-3xl">
      {/* Header */}
      <div className="mb-6">
        <div className="text-[11px] tracking-[0.18em] uppercase text-warm-400 font-medium">
          Configuración
        </div>
        <h1 className="font-serif text-[32px] lg:text-[36px] leading-tight text-warm-800 mt-1">
          Política de pagos
        </h1>
        <p className="text-[13.5px] text-warm-600 mt-2 max-w-2xl">
          Cuánto tiempo reservas el cupo cuando una cliente agenda con
          anticipo pendiente. Si no envía comprobante en este tiempo, la
          cita se cancela sola y el cupo queda libre para otra clienta.
        </p>
      </div>

      {isLoading && (
        <div className="text-[13px] text-warm-500">Cargando configuración…</div>
      )}
      {error && (
        <div className="text-[13px] text-terra-500">No se pudo cargar la configuración.</div>
      )}

      {data && (
        <div className="space-y-4">
          {/* Tiempo máximo del hold */}
          <Card className="p-5">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-lg bg-brand-50 border border-brand-100 text-brand-700 flex items-center justify-center flex-shrink-0">
                <Clock size={18} />
              </div>
              <div className="flex-1 min-w-0">
                <label className="block text-[14px] font-medium text-warm-800">
                  Tiempo máximo del cupo reservado
                </label>
                <p className="text-[12.5px] text-warm-500 mt-1">
                  Si la cliente agenda y no envía comprobante en este tiempo,
                  la cita se cancela y el cupo se libera. Rango permitido: 1 a 48 horas.
                </p>
                <div className="mt-3 flex items-center gap-2">
                  <Input
                    type="number"
                    min={1}
                    max={48}
                    value={form.holdDurationHours}
                    onChange={e => setForm(f => ({ ...f, holdDurationHours: Number(e.target.value) }))}
                    className="w-24"
                  />
                  <span className="text-[13px] text-warm-600">horas</span>
                </div>
              </div>
            </div>
          </Card>

          {/* Hold deja de aplicar cuánto antes de la cita */}
          <Card className="p-5">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-lg bg-gold-50 border border-gold-200 text-gold-600 flex items-center justify-center flex-shrink-0">
                <Wallet size={18} />
              </div>
              <div className="flex-1 min-w-0">
                <label className="block text-[14px] font-medium text-warm-800">
                  Margen antes de la cita
                </label>
                <p className="text-[12.5px] text-warm-500 mt-1">
                  Aún si el tiempo máximo no se cumplió, el cupo se libera este
                  tantos minutos antes de la cita. Evita citas "fantasma" a último
                  minuto. Rango: 0 a 240 minutos.
                </p>
                <div className="mt-3 flex items-center gap-2">
                  <Input
                    type="number"
                    min={0}
                    max={240}
                    step={5}
                    value={form.holdMinBeforeAppointmentMinutes}
                    onChange={e => setForm(f => ({ ...f, holdMinBeforeAppointmentMinutes: Number(e.target.value) }))}
                    className="w-24"
                  />
                  <span className="text-[13px] text-warm-600">minutos antes</span>
                </div>
              </div>
            </div>
          </Card>

          {/* Anticipación mínima para agendar */}
          <Card className="p-5">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-lg bg-warm-100 border border-warm-200 text-warm-700 flex items-center justify-center flex-shrink-0">
                <CheckCircle size={18} />
              </div>
              <div className="flex-1 min-w-0">
                <label className="block text-[14px] font-medium text-warm-800">
                  Anticipación mínima para agendar
                </label>
                <p className="text-[12.5px] text-warm-500 mt-1">
                  Tiempo mínimo que tiene que faltar para que una cliente pueda
                  reservar una cita. Evita pedidos "para ya". La administradora
                  puede saltar esta regla para walk-ins. Rango: 0 a 1440 minutos (24h).
                </p>
                <div className="mt-3 flex items-center gap-2">
                  <Input
                    type="number"
                    min={0}
                    max={1440}
                    step={5}
                    value={form.minAdvanceMinutes}
                    onChange={e => setForm(f => ({ ...f, minAdvanceMinutes: Number(e.target.value) }))}
                    className="w-24"
                  />
                  <span className="text-[13px] text-warm-600">minutos antes</span>
                </div>
              </div>
            </div>
          </Card>

          {/* Resumen actual destacado */}
          <div className="rounded-xl border border-warm-150 bg-warm-50/60 p-4 text-[12.5px] text-warm-700">
            <strong className="text-warm-800">Cómo va a quedar:</strong> la cliente que
            agende un balayage hoy a las 14:00 (anticipo pendiente) tiene hasta{' '}
            <strong className="tabular-nums">{form.holdDurationHours}h</strong> para enviar el
            comprobante, o hasta <strong className="tabular-nums">{form.holdMinBeforeAppointmentMinutes} min</strong>{' '}
            antes de la cita (lo que llegue primero). Las clientas no pueden agendar
            con menos de <strong className="tabular-nums">{form.minAdvanceMinutes} min</strong> de anticipación.
          </div>

          {savedMessage && (
            <p className="rounded-md bg-brand-50 border border-brand-100 p-2.5 text-[13px] text-brand-800">
              ✓ {savedMessage}
            </p>
          )}
          {submitError && (
            <p className="rounded-md bg-terra-100 border border-terra-300 p-2.5 text-[13px] text-terra-500">
              {submitError}
            </p>
          )}

          <div className="flex gap-2 pt-2">
            <Button
              variant="secondary"
              onClick={reset}
              disabled={!isDirty || save.isPending}
            >
              Deshacer
            </Button>
            <Button
              onClick={submit}
              loading={save.isPending}
              disabled={!isDirty || save.isPending}
            >
              Guardar cambios
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
