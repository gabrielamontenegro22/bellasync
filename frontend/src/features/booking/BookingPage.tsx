import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { Button, Card, Input } from '@/components/ui'
import { publicBook, type PublicBookingResponse } from '@/api/publicBooking'

/**
 * Portal público anónimo. URL: /booking/:tenantSlug
 *
 * MVP simplificado: el usuario escribe manualmente los IDs de servicio
 * y estilista (en producción habrá un selector que consume endpoints
 * públicos /services y /stylists del salón). Foco está en el flujo:
 * pedir → confirmar → mostrar instrucciones de pago.
 *
 * Cuando agreguemos los endpoints públicos GET /api/PublicBooking/{slug}/services
 * y /stylists, esto se reemplaza por un wizard real.
 */
export function BookingPage() {
  const { tenantSlug = '' } = useParams<{ tenantSlug: string }>()
  const [step, setStep] = useState<1 | 2>(1)
  const [form, setForm] = useState({
    serviceId: '',
    stylistId: '',
    startAtUtc: '',
    clientName: '',
    clientPhone: '',
    clientEmail: '',
  })
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<PublicBookingResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  function update<K extends keyof typeof form>(key: K, value: typeof form[K]) {
    setForm(prev => ({ ...prev, [key]: value }))
  }

  async function submit() {
    setError(null)
    setSubmitting(true)
    try {
      const resp = await publicBook(tenantSlug, {
        serviceId: form.serviceId,
        stylistId: form.stylistId,
        startAtUtc: form.startAtUtc,
        clientName: form.clientName,
        clientPhone: form.clientPhone,
        clientEmail: form.clientEmail || undefined,
      })
      setResult(resp)
    } catch (e: any) {
      setError(
        e?.response?.data?.detail
        ?? e?.response?.data?.title
        ?? 'No se pudo agendar la cita.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  if (result) return <SuccessScreen result={result} />

  return (
    <div className="mx-auto max-w-2xl space-y-4 p-6">
      <header className="text-center">
        <h1 className="font-serif text-3xl text-brand-700">Agendá tu cita</h1>
        <p className="text-sm text-warm-500">Salón: {tenantSlug}</p>
      </header>

      <ol className="flex justify-center gap-4 text-sm">
        <li className={step === 1 ? 'font-semibold text-brand-700' : 'text-warm-500'}>1. Servicio</li>
        <li className={step === 2 ? 'font-semibold text-brand-700' : 'text-warm-500'}>2. Tus datos</li>
      </ol>

      {step === 1 && (
        <Card className="space-y-3 p-4">
          <p className="text-xs text-warm-500">
            (MVP: pegá los IDs manualmente. Los selectores con foto vienen pronto.)
          </p>
          <Input label="Service ID" value={form.serviceId} onChange={e => update('serviceId', e.target.value)} />
          <Input label="Stylist ID" value={form.stylistId} onChange={e => update('stylistId', e.target.value)} />
          <Input
            label="Fecha y hora (UTC)"
            type="datetime-local"
            value={form.startAtUtc.slice(0, 16)}
            onChange={e => update('startAtUtc', e.target.value + ':00.000Z')}
          />
          <Button
            fullWidth
            onClick={() => setStep(2)}
            disabled={!form.serviceId || !form.stylistId || !form.startAtUtc}
          >
            Siguiente
          </Button>
        </Card>
      )}

      {step === 2 && (
        <Card className="space-y-3 p-4">
          <Input label="Nombre completo" value={form.clientName} onChange={e => update('clientName', e.target.value)} />
          <Input label="Teléfono (WhatsApp)" value={form.clientPhone} onChange={e => update('clientPhone', e.target.value)} />
          <Input label="Email (opcional)" type="email" value={form.clientEmail} onChange={e => update('clientEmail', e.target.value)} />

          {error && <p className="rounded-md bg-terra-100 p-2 text-sm text-terra-700">{error}</p>}

          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => setStep(1)}>← Atrás</Button>
            <Button
              fullWidth
              onClick={submit}
              loading={submitting}
              disabled={!form.clientName || !form.clientPhone}
            >
              Confirmar reserva
            </Button>
          </div>
        </Card>
      )}
    </div>
  )
}

function SuccessScreen({ result }: { result: PublicBookingResponse }) {
  return (
    <div className="mx-auto max-w-2xl space-y-4 p-6">
      <Card className="p-6 text-center">
        <p className="text-5xl">✓</p>
        <h1 className="mt-2 font-serif text-2xl text-brand-700">¡Tu cita está solicitada!</h1>
        <p className="mt-2 text-sm text-warm-500">
          {result.serviceName} con {result.stylistName} · {new Date(result.startAt).toLocaleString('es-CO')}
        </p>
      </Card>

      {result.requiresDeposit && (
        <Card className="space-y-3 border-l-4 border-l-gold-400 bg-gold-50 p-4">
          <p className="font-semibold text-gold-700">Acción pendiente: transferí el anticipo</p>
          <p className="text-sm text-warm-700">
            Tu cupo está reservado hasta{' '}
            <strong>{result.holdExpiresAt ? new Date(result.holdExpiresAt).toLocaleString('es-CO') : '—'}</strong>.
            Después de eso, si no validamos el pago, la cita se cancela automáticamente.
          </p>
          <div className="rounded-md bg-white p-3 text-sm">
            <p><strong>Monto:</strong> {formatMoney(result.depositAmount)}</p>
            <p><strong>Banco:</strong> Bancolombia Ahorros</p>
            <p><strong>Titular:</strong> Bella Aurora Salón SAS</p>
            <p><strong>Cuenta:</strong> XXXX-XXXX-XXXX (pendiente configurar por salón)</p>
            <p><strong>Concepto:</strong> Tu nombre + fecha cita</p>
          </div>
          <p className="text-sm text-warm-700">
            Una vez transferido, enviá el comprobante por WhatsApp al número del salón.
          </p>
        </Card>
      )}

      {!result.requiresDeposit && (
        <Card className="border-l-4 border-l-brand-500 bg-brand-50 p-4">
          <p className="text-sm text-brand-700">
            ¡Tu cita está <strong>confirmada</strong>! No necesitás pagar anticipo.
            Te esperamos a la hora acordada.
          </p>
        </Card>
      )}
    </div>
  )
}

function formatMoney(amount: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount)
}
