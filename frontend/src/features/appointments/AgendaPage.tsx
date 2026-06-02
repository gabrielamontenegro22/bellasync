import { useState } from 'react'
import { type AppointmentResponse, type AppointmentStatus } from '@/api/appointments'
import { Badge, Button, Card } from '@/components/ui'
import {
  useAgenda,
  useCancelAppointment,
  useCompleteAppointment,
  useConfirmAppointment,
  useMarkNoShow,
  useStartAppointment,
} from './hooks'

/** Agenda del día. Lista de citas + panel lateral con detalle de la seleccionada. */
export function AgendaPage() {
  const [date, setDate] = useState(() => formatLocalDate(new Date()))
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const { data, isLoading, error } = useAgenda(date)

  const selected = data?.appointments.find(a => a.id === selectedId) ?? null

  return (
    <div className="flex h-full gap-4 p-4">
      <main className="flex-1 space-y-4 overflow-auto">
        <Header date={date} onDateChange={d => { setDate(d); setSelectedId(null) }} />
        <Metrics metrics={data?.metrics} />

        {isLoading && <p className="text-sm text-warm-500">Cargando agenda…</p>}
        {error && <p className="text-sm text-terra-700">No se pudo cargar la agenda.</p>}

        {data && data.appointments.length === 0 && (
          <Card className="p-8 text-center">
            <p className="text-warm-500">No hay citas para este día.</p>
          </Card>
        )}

        {data && data.appointments.length > 0 && (
          <div className="space-y-2">
            {data.appointments.map(a => (
              <AppointmentRow
                key={a.id}
                appointment={a}
                selected={selectedId === a.id}
                onClick={() => setSelectedId(a.id)}
              />
            ))}
          </div>
        )}
      </main>

      <aside className="w-96 flex-shrink-0">
        {selected
          ? <DetailPanel appointment={selected} onClose={() => setSelectedId(null)} />
          : <EmptyDetail />}
      </aside>
    </div>
  )
}

// ===== Subcomponentes =====

function Header({ date, onDateChange }: { date: string; onDateChange: (d: string) => void }) {
  const today = formatLocalDate(new Date())
  const yesterday = formatLocalDate(addDays(new Date(date), -1))
  const tomorrow = formatLocalDate(addDays(new Date(date), 1))

  return (
    <Card className="flex flex-wrap items-center gap-2 p-3">
      <h1 className="font-serif text-2xl text-brand-700 mr-auto">Agenda</h1>
      <Button variant="ghost" size="sm" onClick={() => onDateChange(yesterday)}>← Anterior</Button>
      <Button variant={date === today ? 'primary' : 'ghost'} size="sm" onClick={() => onDateChange(today)}>
        Hoy
      </Button>
      <Button variant="ghost" size="sm" onClick={() => onDateChange(tomorrow)}>Siguiente →</Button>
      <input
        type="date"
        value={date}
        onChange={e => onDateChange(e.target.value)}
        className="rounded-md border border-warm-200 px-2 py-1 text-sm"
      />
    </Card>
  )
}

function Metrics({ metrics }: { metrics: { total: number; pendingValidation: number; confirmed: number; noShow: number } | undefined }) {
  const m = metrics ?? { total: 0, pendingValidation: 0, confirmed: 0, noShow: 0 }
  return (
    <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
      <MetricCard label="Total" value={m.total} />
      <MetricCard label="Pendientes pago" value={m.pendingValidation} accent={m.pendingValidation > 0 ? 'gold' : 'neutral'} />
      <MetricCard label="Confirmadas" value={m.confirmed} />
      <MetricCard label="No-show" value={m.noShow} accent={m.noShow > 0 ? 'terra' : 'neutral'} />
    </div>
  )
}

function MetricCard({ label, value, accent }: { label: string; value: number; accent?: 'gold' | 'terra' | 'neutral' }) {
  const accentClass =
    accent === 'gold' ? 'text-gold-600' :
    accent === 'terra' ? 'text-terra-500' :
    'text-brand-700'
  return (
    <Card className="p-3">
      <p className="text-xs uppercase tracking-wide text-warm-500">{label}</p>
      <p className={`text-2xl font-serif ${accentClass}`}>{value}</p>
    </Card>
  )
}

function AppointmentRow({
  appointment, selected, onClick,
}: { appointment: AppointmentResponse; selected: boolean; onClick: () => void }) {
  const time = formatLocalTime(appointment.startAt) + '–' + formatLocalTime(appointment.endAt)
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-center gap-3 rounded-lg border p-3 text-left transition ${
        selected ? 'border-brand-500 bg-brand-50' : 'border-warm-200 bg-white hover:border-brand-300'
      }`}
    >
      <span className="font-mono text-sm text-brand-700 w-32">{time}</span>
      <span className="flex-1">
        <p className="font-medium text-warm-900">{appointment.customerName}</p>
        <p className="text-xs text-warm-500">{appointment.serviceName} · {appointment.stylistName}</p>
      </span>
      <StatusBadge status={appointment.status} depositStatus={appointment.depositStatus} />
    </button>
  )
}

function StatusBadge({ status, depositStatus }: { status: AppointmentStatus; depositStatus: string }) {
  const map: Record<AppointmentStatus, { label: string; tone: 'gold' | 'brand' | 'terra' | 'neutral' }> = {
    Pending: { label: depositStatus === 'AwaitingPayment' ? 'Pago pendiente' : 'Pendiente', tone: 'gold' },
    Confirmed: { label: 'Confirmada', tone: 'brand' },
    InProgress: { label: 'En curso', tone: 'gold' },
    Completed: { label: 'Completada', tone: 'neutral' },
    Cancelled: { label: 'Cancelada', tone: 'neutral' },
    NoShow: { label: 'No asistió', tone: 'terra' },
  }
  const cfg = map[status]
  return <Badge tone={cfg.tone}>{cfg.label}</Badge>
}

function DetailPanel({ appointment, onClose }: { appointment: AppointmentResponse; onClose: () => void }) {
  const confirm = useConfirmAppointment()
  const cancel = useCancelAppointment()
  const start = useStartAppointment()
  const complete = useCompleteAppointment()
  const noShow = useMarkNoShow()

  const canConfirm = appointment.status === 'Pending' && appointment.depositStatus !== 'AwaitingPayment'
  const canStart = appointment.status === 'Confirmed'
  const canComplete = appointment.status === 'InProgress'
  const canCancel = appointment.status === 'Pending' || appointment.status === 'Confirmed'
  const canNoShow = appointment.status === 'Confirmed' || appointment.status === 'Pending'

  return (
    <Card className="sticky top-0 max-h-[calc(100vh-2rem)] overflow-auto p-4">
      <div className="flex items-start justify-between">
        <div>
          <p className="font-serif text-xl text-brand-700">{appointment.customerName}</p>
          <p className="text-xs text-warm-500">{appointment.customerPhone}</p>
        </div>
        <button onClick={onClose} className="text-warm-400 hover:text-warm-600" aria-label="Cerrar">✕</button>
      </div>

      <hr className="my-3 border-warm-200" />

      <dl className="space-y-2 text-sm">
        <Row label="Servicio">{appointment.serviceName}</Row>
        <Row label="Estilista">{appointment.stylistName}</Row>
        <Row label="Inicio">{formatLocalDateTime(appointment.startAt)}</Row>
        <Row label="Duración">{appointment.durationMinutes} min</Row>
        <Row label="Precio">{formatMoney(appointment.priceSnapshot)}</Row>
        {appointment.depositStatus !== 'NotRequired' && (
          <Row label="Anticipo">
            {formatMoney(appointment.depositAmount)} ({appointment.depositStatus})
          </Row>
        )}
        {appointment.notes && <Row label="Notas">{appointment.notes}</Row>}
      </dl>

      <div className="mt-4 grid grid-cols-2 gap-2">
        {canConfirm && <Button onClick={() => confirm.mutate(appointment.id)} disabled={confirm.isPending}>Confirmar</Button>}
        {canStart && <Button onClick={() => start.mutate(appointment.id)} disabled={start.isPending}>Iniciar</Button>}
        {canComplete && <Button onClick={() => complete.mutate(appointment.id)} disabled={complete.isPending}>Completar</Button>}
        {canNoShow && <Button variant="ghost" onClick={() => noShow.mutate(appointment.id)} disabled={noShow.isPending}>No-show</Button>}
        {canCancel && (
          <Button
            variant="ghost"
            onClick={() => {
              const reason = window.prompt('Razón de la cancelación (opcional):') ?? undefined
              cancel.mutate({ id: appointment.id, reason })
            }}
            disabled={cancel.isPending}
          >
            Cancelar
          </Button>
        )}
      </div>
    </Card>
  )
}

function EmptyDetail() {
  return (
    <Card className="sticky top-0 p-6 text-center">
      <p className="text-warm-500">Selecciona una cita para ver el detalle.</p>
    </Card>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <dt className="text-warm-500">{label}</dt>
      <dd className="text-right text-warm-900">{children}</dd>
    </div>
  )
}

// ===== Helpers =====

function formatLocalDate(d: Date): string {
  return d.toISOString().slice(0, 10)
}
function addDays(d: Date, n: number): Date {
  const r = new Date(d); r.setDate(r.getDate() + n); return r
}
function formatLocalTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false })
}
function formatLocalDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
}
function formatMoney(amount: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
    .format(amount)
}
