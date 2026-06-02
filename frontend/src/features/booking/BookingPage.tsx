import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { Button, Card, Input } from '@/components/ui'
import {
  getPublicServices,
  getPublicStylists,
  publicBook,
  type PublicBookingResponse,
  type PublicService,
  type PublicStylist,
} from '@/api/publicBooking'
import { extractApiError } from '@/lib/extractApiError'

/**
 * Portal público anónimo de booking. URL: /booking/:tenantSlug.
 * Wizard de 3 pasos: Servicio → Estilista+Hora → Tus datos.
 */
export function BookingPage() {
  const { tenantSlug = '' } = useParams<{ tenantSlug: string }>()
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [service, setService] = useState<PublicService | null>(null)
  const [stylist, setStylist] = useState<PublicStylist | null>(null)
  const [startAtUtc, setStartAtUtc] = useState<string>('')
  const [client, setClient] = useState({ name: '', phone: '', email: '' })
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<PublicBookingResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  const servicesQ = useQuery({
    queryKey: ['public-services', tenantSlug],
    queryFn: () => getPublicServices(tenantSlug),
    enabled: !!tenantSlug,
  })
  const stylistsQ = useQuery({
    queryKey: ['public-stylists', tenantSlug],
    queryFn: () => getPublicStylists(tenantSlug),
    enabled: !!tenantSlug,
  })

  // Estilistas filtrados: solo los que hacen el servicio elegido
  const availableStylists = useMemo(
    () => stylistsQ.data?.filter(s => !service || s.serviceIds.includes(service.id)) ?? [],
    [stylistsQ.data, service],
  )

  // Si el estilista seleccionado deja de poder hacer el nuevo service, lo resetea
  useEffect(() => {
    if (stylist && service && !stylist.serviceIds.includes(service.id)) setStylist(null)
  }, [service, stylist])

  async function submit() {
    if (!service || !stylist || !startAtUtc) return
    setError(null)
    setSubmitting(true)
    try {
      const resp = await publicBook(tenantSlug, {
        serviceId: service.id,
        stylistId: stylist.id,
        startAtUtc,
        clientName: client.name,
        clientPhone: client.phone,
        clientEmail: client.email || undefined,
      })
      setResult(resp)
    } catch (e) {
      setError(extractApiError(e, 'No se pudo agendar la cita.'))
    } finally {
      setSubmitting(false)
    }
  }

  if (result) return <SuccessScreen result={result} />

  return (
    <div className="mx-auto max-w-3xl space-y-4 p-6">
      <header className="text-center">
        <h1 className="font-serif text-3xl text-brand-700">Agendá tu cita</h1>
        <p className="text-sm text-warm-500">Salón: {tenantSlug}</p>
      </header>

      <Stepper current={step} />

      {(servicesQ.isLoading || stylistsQ.isLoading) && (
        <p className="text-center text-warm-500">Cargando catálogo…</p>
      )}
      {(servicesQ.error || stylistsQ.error) && (
        <p className="text-center text-terra-700">No se pudo cargar el salón. Verificá el link.</p>
      )}

      {servicesQ.data && stylistsQ.data && (
        <>
          {step === 1 && (
            <ServiceStep
              services={servicesQ.data}
              selected={service}
              onSelect={s => { setService(s); setStep(2) }}
            />
          )}

          {step === 2 && service && (
            <StylistAndTimeStep
              service={service}
              stylists={availableStylists}
              selectedStylist={stylist}
              startAtUtc={startAtUtc}
              onSelectStylist={setStylist}
              onChangeTime={setStartAtUtc}
              onBack={() => setStep(1)}
              onNext={() => setStep(3)}
            />
          )}

          {step === 3 && service && stylist && (
            <ClientStep
              service={service}
              stylist={stylist}
              startAtUtc={startAtUtc}
              client={client}
              onChangeClient={setClient}
              onBack={() => setStep(2)}
              onSubmit={submit}
              submitting={submitting}
              error={error}
            />
          )}
        </>
      )}
    </div>
  )
}

// ===== Stepper =====

function Stepper({ current }: { current: number }) {
  const steps = [
    { n: 1, label: 'Servicio' },
    { n: 2, label: 'Estilista y hora' },
    { n: 3, label: 'Tus datos' },
  ]
  return (
    <ol className="flex items-center justify-center gap-2 text-sm">
      {steps.map((s, i) => (
        <span key={s.n} className="flex items-center gap-2">
          <span className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${
            current >= s.n ? 'bg-brand-700 text-white' : 'bg-warm-200 text-warm-600'
          }`}>{s.n}</span>
          <span className={current === s.n ? 'font-semibold text-brand-700' : 'text-warm-500'}>
            {s.label}
          </span>
          {i < steps.length - 1 && <span className="text-warm-300">›</span>}
        </span>
      ))}
    </ol>
  )
}

// ===== Paso 1: Servicio =====

function ServiceStep({
  services, selected, onSelect,
}: { services: PublicService[]; selected: PublicService | null; onSelect: (s: PublicService) => void }) {
  if (services.length === 0)
    return <Card className="p-6 text-center"><p className="text-warm-500">Sin servicios disponibles.</p></Card>

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
      {services.map(s => (
        <button
          key={s.id}
          type="button"
          onClick={() => onSelect(s)}
          className={`rounded-lg border p-4 text-left transition ${
            selected?.id === s.id ? 'border-brand-500 bg-brand-50' : 'border-warm-200 bg-white hover:border-brand-300'
          }`}
        >
          <div className="flex items-start justify-between">
            <p className="font-serif text-lg text-brand-700">{s.name}</p>
            <span className="text-sm font-semibold text-warm-700">{formatMoney(s.price)}</span>
          </div>
          {s.description && <p className="mt-1 text-sm text-warm-600">{s.description}</p>}
          <p className="mt-2 text-xs text-warm-500">
            {s.durationMinutes} min · {s.category}
            {s.requiresDeposit && ` · Anticipo ${s.depositPercentage}%`}
          </p>
        </button>
      ))}
    </div>
  )
}

// ===== Paso 2: Estilista + Hora =====

function StylistAndTimeStep({
  service, stylists, selectedStylist, startAtUtc, onSelectStylist, onChangeTime, onBack, onNext,
}: {
  service: PublicService
  stylists: PublicStylist[]
  selectedStylist: PublicStylist | null
  startAtUtc: string
  onSelectStylist: (s: PublicStylist) => void
  onChangeTime: (iso: string) => void
  onBack: () => void
  onNext: () => void
}) {
  return (
    <Card className="space-y-4 p-4">
      <p className="text-sm text-warm-500">
        Servicio elegido: <strong className="text-warm-900">{service.name}</strong> · {service.durationMinutes} min
      </p>

      <div>
        <p className="mb-2 text-xs uppercase tracking-wide text-warm-500">Estilista</p>
        {stylists.length === 0
          ? <p className="text-sm text-terra-700">No hay estilistas que realicen este servicio. Probá con otro.</p>
          : (
            <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
              {stylists.map(st => (
                <button
                  key={st.id}
                  type="button"
                  onClick={() => onSelectStylist(st)}
                  className={`rounded-lg border p-3 text-left transition ${
                    selectedStylist?.id === st.id ? 'border-brand-500 bg-brand-50' : 'border-warm-200 bg-white hover:border-brand-300'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    {st.color && (
                      <span className="inline-block h-3 w-3 rounded-full" style={{ background: st.color }} />
                    )}
                    <p className="font-medium text-warm-900">{st.fullName}</p>
                  </div>
                  <p className="text-xs text-warm-500">{st.role}</p>
                </button>
              ))}
            </div>
          )}
      </div>

      <div>
        <p className="mb-2 text-xs uppercase tracking-wide text-warm-500">Fecha y hora</p>
        <input
          type="datetime-local"
          className="w-full rounded-md border border-warm-200 p-2 text-sm"
          value={startAtUtc.slice(0, 16)}
          onChange={e => onChangeTime(e.target.value ? new Date(e.target.value).toISOString() : '')}
        />
        <p className="mt-1 text-xs text-warm-500">
          La cita debe agendarse con al menos 30 minutos de anticipación.
        </p>
      </div>

      <div className="flex gap-2">
        <Button variant="secondary" onClick={onBack}>← Atrás</Button>
        <Button fullWidth onClick={onNext} disabled={!selectedStylist || !startAtUtc}>
          Siguiente
        </Button>
      </div>
    </Card>
  )
}

// ===== Paso 3: Tus datos =====

function ClientStep({
  service, stylist, startAtUtc, client, onChangeClient, onBack, onSubmit, submitting, error,
}: {
  service: PublicService
  stylist: PublicStylist
  startAtUtc: string
  client: { name: string; phone: string; email: string }
  onChangeClient: (c: { name: string; phone: string; email: string }) => void
  onBack: () => void
  onSubmit: () => void
  submitting: boolean
  error: string | null
}) {
  const total = service.price
  const deposit = service.requiresDeposit ? service.depositAmount : 0

  return (
    <Card className="space-y-4 p-4">
      <div className="rounded-md bg-brand-50 p-3 text-sm">
        <p className="font-semibold text-brand-700">Resumen</p>
        <p className="text-warm-700">{service.name} con {stylist.fullName}</p>
        <p className="text-warm-700">{new Date(startAtUtc).toLocaleString('es-CO')}</p>
        <p className="mt-1 text-warm-900">Total: {formatMoney(total)}</p>
        {deposit > 0 && (
          <p className="text-gold-600">Anticipo a transferir: {formatMoney(deposit)}</p>
        )}
      </div>

      <Input label="Nombre completo" value={client.name} onChange={e => onChangeClient({ ...client, name: e.target.value })} />
      <Input label="Teléfono (WhatsApp)" value={client.phone} onChange={e => onChangeClient({ ...client, phone: e.target.value })} />
      <Input label="Email (opcional)" type="email" value={client.email} onChange={e => onChangeClient({ ...client, email: e.target.value })} />

      {error && <p className="rounded-md bg-terra-100 p-2 text-sm text-terra-700">{error}</p>}

      <div className="flex gap-2">
        <Button variant="secondary" onClick={onBack}>← Atrás</Button>
        <Button fullWidth onClick={onSubmit} loading={submitting} disabled={!client.name || !client.phone}>
          Confirmar reserva
        </Button>
      </div>
    </Card>
  )
}

// ===== Success =====

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
          <p className="font-semibold text-gold-600">Acción pendiente: transferí el anticipo</p>
          <p className="text-sm text-warm-700">
            Tu cupo está reservado hasta{' '}
            <strong>{result.holdExpiresAt ? new Date(result.holdExpiresAt).toLocaleString('es-CO') : '—'}</strong>.
          </p>
          <div className="rounded-md bg-white p-3 text-sm">
            <p><strong>Monto:</strong> {formatMoney(result.depositAmount)}</p>
            <p><strong>Banco:</strong> Bancolombia (cuenta del salón)</p>
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
          </p>
        </Card>
      )}
    </div>
  )
}

function formatMoney(amount: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount)
}
