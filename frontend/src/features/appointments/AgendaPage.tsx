import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Calendar, CheckCircle, Clock, AlertCircle,
  ChevronLeft, ChevronRight, MessageCircle, Bell, X, ArrowRight, Plus,
  AlertTriangle, Droplet, Heart, Sparkles, Wallet,
} from 'lucide-react'
import { type AppointmentResponse } from '@/api/appointments'
import { seedDemoData } from '@/api/admin'
import { extractApiError } from '@/lib/extractApiError'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { useCustomerAppointments } from '@/features/customers/hooks'
import { useCustomerPayments } from '@/features/payments/hooks'
import { getPaymentBadge } from '@/features/payments/paymentBadge'
import {
  fmtCop, fmtMonth, initialsOf, toneOf, whatsappLink,
} from '@/features/customers/lib/customerLook'
import {
  useAgenda,
  useCancelAppointment,
  useCompleteAppointment,
  useConfirmAppointment,
  useMarkNoShow,
  useStartAppointment,
} from './hooks'
import { DatePicker } from '@/components/ui'
import { NewAppointmentModal } from './components/NewAppointmentModal'
import { AgendaTimeline } from './components/AgendaTimeline'
import { RescheduleModal } from './components/RescheduleModal'
import { RegisterPaymentModal } from '@/features/payments/components/RegisterPaymentModal'

/**
 * Agenda del día — replica el layout de `mockups/Agendamiento_de_citas/app.jsx`.
 *
 * Layout (de arriba a abajo):
 *  - Header con eyebrow del salón + fecha en serif + day nav pills
 *  - Banner amber si hay pagos urgentes pendientes
 *  - Metric cards (4 cards: total / pendientes / confirmadas / no-show)
 *  - Timeline grid: columnas por estilista × eje vertical de horas
 *  - Panel detalle a la derecha (cita seleccionada)
 *  - FAB "+ Nueva cita" abajo-derecha
 */
export function AgendaPage() {
  // ?date=YYYY-MM-DD permite deeplink desde el CRM ("Ver en agenda").
  const [searchParams, setSearchParams] = useSearchParams()
  const dateParam = searchParams.get('date')
  const today = formatLocalDate(new Date())
  const date = dateParam && /^\d{4}-\d{2}-\d{2}$/.test(dateParam) ? dateParam : today
  const setDate = (d: string) => {
    setSearchParams(d === today ? {} : { date: d }, { replace: true })
  }

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showNewModal, setShowNewModal] = useState(false)
  const [bannerDismissed, setBannerDismissed] = useState(false)
  /**
   * Filtro activo en las cards de stats. 'all' = mostrar todas;
   * los otros corresponden 1:1 a las cards. Click sobre una card
   * activa la deselecciona (vuelve a 'all'). El default es 'all'
   * — ninguna card resaltada al abrir la pantalla.
   */
  const [activeFilter, setActiveFilter] = useState<AgendaFilter>('all')
  const navigate = useNavigate()
  const { data, isLoading, error } = useAgenda(date)

  // Filtramos en frontend: las agendas tienen typicamente <30 citas/día,
  // ir al backend por cada cambio de filtro sería overkill.
  const filteredAppointments = useMemo(
    () => (data ? filterAppointments(data.appointments, activeFilter) : []),
    [data, activeFilter],
  )

  // Si la cita seleccionada queda fuera del filtro, la deseleccionamos
  // (sino se rompe el panel detalle que apunta a una cita no visible).
  useEffect(() => {
    if (selectedId && !filteredAppointments.some(a => a.id === selectedId)) {
      setSelectedId(null)
    }
  }, [selectedId, filteredAppointments])

  const selected = data?.appointments.find(a => a.id === selectedId) ?? null
  const urgentCount = data?.metrics.pendingValidation ?? 0
  const showBanner = !bannerDismissed && urgentCount > 0

  return (
    // Layout fijo del viewport: el contenedor entero NO scrollea.
    // Solo el contenido del timeline scrollea internamente; el panel
    // detalle queda anclado a viewport-top y viewport-bottom con su
    // propio footer fijo.
    <div className="flex h-full min-h-0 bg-warm-50 relative overflow-hidden">
      {/* Columna principal: header + banner + metrics fijos arriba, timeline scrollable */}
      <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
        <Header
          date={date}
          onDateChange={d => { setDate(d); setSelectedId(null) }}
        />

        {showBanner && (
          <UrgentBanner
            count={urgentCount}
            onDismiss={() => setBannerDismissed(true)}
            onGo={() => navigate('/validacion')}
          />
        )}

        <Metrics
          metrics={data?.metrics}
          activeFilter={activeFilter}
          onFilterChange={(f) => setActiveFilter(f === activeFilter ? 'all' : f)}
        />

        {/* Única región scrollable de la columna principal */}
        <div className="flex-1 min-h-0 overflow-y-auto px-5 lg:px-8 pb-10">
          {isLoading && (
            <div className="py-10 text-center text-[13px] text-warm-500">
              Cargando agenda…
            </div>
          )}
          {error && (
            <div className="py-10 text-center text-[13px] text-terra-500">
              No se pudo cargar la agenda.
            </div>
          )}

          {data && !isLoading && (
            <>
              {/* Indicador del filtro activo: pill arriba del timeline con
                  el conteo y un X para limpiarlo rápido. Solo aparece
                  cuando hay filtro distinto de 'all'. */}
              {activeFilter !== 'all' && (
                <div className="mb-3 flex items-center gap-2 text-[12px]">
                  <span className="text-warm-500">Mostrando</span>
                  <span className="px-2 py-0.5 rounded-md bg-warm-800 text-white font-medium">
                    {FILTER_LABELS[activeFilter]}
                    <span className="opacity-70 ml-1.5 tabular-nums">
                      {filteredAppointments.length}
                    </span>
                  </span>
                  <button
                    type="button"
                    onClick={() => setActiveFilter('all')}
                    className="text-warm-500 hover:text-warm-800 underline underline-offset-2"
                  >
                    limpiar
                  </button>
                </div>
              )}
              <AgendaTimeline
                appointments={filteredAppointments}
                date={date}
                selectedId={selectedId}
                onSelect={a => setSelectedId(a.id)}
              />
            </>
          )}
        </div>
      </div>

      {/* Panel detalle — SOLO cuando hay cita seleccionada.
          - Desktop (≥lg): empuja el timeline (panel sólido 420px a la derecha)
          - Mobile/tablet (<lg): se monta como overlay sobre el timeline para
            no aplastar la grilla. En iPad llega hasta 420px desde la derecha
            con backdrop; en mobile (<sm) ocupa todo el ancho.
          Antes este panel era `hidden lg:block`, así que en iPad/mobile el
          click en una cita no mostraba absolutamente nada — bug real. */}
      {selected && (
        <>
          {/* Backdrop solo en <lg para cerrar al tocar fuera. */}
          <div
            onClick={() => setSelectedId(null)}
            className="lg:hidden fixed inset-0 bg-warm-900/30 z-30 anim-fade"
            aria-hidden="true"
          />
          <aside
            className={cls(
              'bg-white border-l border-warm-150 shadow-panel overflow-hidden animate-slide',
              // Mobile/tablet: overlay fixed desde la derecha
              'fixed inset-y-0 right-0 z-40 w-full sm:w-[420px]',
              // Desktop: empuja el timeline en flujo normal
              'lg:static lg:w-[420px] lg:flex-shrink-0 lg:z-auto',
            )}
          >
            <DetailPanel appointment={selected} onClose={() => setSelectedId(null)} />
          </aside>
        </>
      )}

      {/* FAB Nueva cita — fixed abajo-derecha.
          - Desktop: si hay panel detalle, se mueve a su izquierda (right-[440px])
          - Mobile/iPad: si hay panel detalle (que es overlay), el FAB se oculta
            porque el detalle cubre toda la pantalla y el FAB encima sería ruido.
          z-20 < panel(z-40) para que el panel quede arriba. */}
      <button
        type="button"
        onClick={() => setShowNewModal(true)}
        className={cls(
          'fixed bottom-6 z-20 flex items-center gap-2 pl-4 pr-5 py-3.5 rounded-full bg-brand-700 hover:bg-brand-800 text-white shadow-pop font-medium text-[14px] transition-all',
          selected ? 'hidden lg:flex right-6 lg:right-[440px]' : 'right-6',
        )}
      >
        <Plus size={18} strokeWidth={2.25} />
        Nueva cita
      </button>

      {showNewModal && (
        <NewAppointmentModal defaultDate={date} onClose={() => setShowNewModal(false)} />
      )}
    </div>
  )
}

// ===== HEADER =====

function Header({
  date, onDateChange,
}: {
  date: string
  onDateChange: (d: string) => void
}) {
  const { user } = useAuth()
  const today = formatLocalDate(new Date())
  const isToday = date === today
  const yesterday = formatLocalDate(addDays(parseLocalDate(date), -1))
  const tomorrow = formatLocalDate(addDays(parseLocalDate(date), 1))
  const isAdmin = user?.role === 'SalonAdmin'

  // Seed de datos demo — solo SalonAdmin lo ve. Llama al endpoint backend
  // que crea idempotentemente estilistas/servicios/clientes/citas para la
  // fecha que está viendo el usuario. Al terminar, invalida la query de la
  // agenda para que se refresque.
  const qc = useQueryClient()
  const seed = useMutation({
    mutationFn: () => seedDemoData(date),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['agenda'] })
      qc.invalidateQueries({ queryKey: ['stylists'] })
      qc.invalidateQueries({ queryKey: ['services'] })
      qc.invalidateQueries({ queryKey: ['customers'] })
      window.alert(
        `Datos demo cargados para ${data.targetDate}:\n` +
        `· Estilistas: +${data.stylistsCreated} (${data.stylistsSkipped} ya existían)\n` +
        `· Servicios:  +${data.servicesCreated} (${data.servicesSkipped} ya existían)\n` +
        `· Clientes:   +${data.customersCreated} (${data.customersSkipped} ya existían)\n` +
        `· Citas:      +${data.appointmentsCreated} (${data.appointmentsSkipped} omitidas)`,
      )
    },
    onError: (e) => {
      window.alert(extractApiError(e, 'No se pudieron cargar los datos demo.'))
    },
  })

  return (
    <header className="px-5 lg:px-8 pt-5 lg:pt-7 pb-4">
      <div className="flex items-start gap-4">
        <div className="flex-1 min-w-0">
          <div className="text-[12px] uppercase tracking-[0.12em] text-warm-500 font-medium">
            <span className="text-brand-700">{user?.tenantName ?? 'Salón'}</span>
            <span className="text-warm-300 mx-2">•</span>
            <span>Agenda del día</span>
          </div>
          <div className="mt-1 flex items-baseline gap-3 flex-wrap">
            <h1 className="font-serif text-[34px] lg:text-[40px] leading-[1.05] text-warm-800 tracking-tight">
              {fmtDateLong(parseLocalDate(date))}
            </h1>
            {isToday && (
              <span className="text-[11.5px] font-medium text-brand-700 bg-brand-50 px-2 py-0.5 rounded-full uppercase tracking-wider">
                Hoy
              </span>
            )}
          </div>
        </div>

        {/* Botón demo — solo lo ve el SalonAdmin. Idempotente: se puede
            apretar varias veces sin riesgo. Útil mientras Gabriela explora
            la app antes de capturar sus datos reales. */}
        {isAdmin && (
          <button
            type="button"
            onClick={() => seed.mutate()}
            disabled={seed.isPending}
            title="Crear estilistas, servicios, clientes y citas demo para este día"
            className="hidden md:flex items-center gap-1.5 px-3 py-2 rounded-lg bg-white border border-gold-200 text-gold-600 hover:bg-gold-50 text-[12.5px] font-medium shrink-0"
          >
            <Sparkles size={14} />
            {seed.isPending ? 'Cargando…' : 'Cargar datos demo'}
          </button>
        )}
      </div>

      {/* day nav */}
      <div className="mt-5 flex items-center gap-1.5 text-[13.5px] flex-wrap">
        <button
          type="button"
          onClick={() => onDateChange(yesterday)}
          className="flex items-center gap-1.5 px-2.5 py-1.5 text-warm-600 hover:text-warm-800 hover:bg-warm-100 rounded-md"
        >
          <ChevronLeft size={16} /> <span>Ayer</span>
          <span className="text-warm-400 text-[12px] ml-1 tabular-nums">{fmtDateShort(parseLocalDate(yesterday))}</span>
        </button>
        <button
          type="button"
          onClick={() => onDateChange(today)}
          className={cls(
            'px-3 py-1.5 rounded-md font-medium',
            isToday
              ? 'bg-warm-800 text-warm-50'
              : 'bg-white border border-warm-200 text-warm-700 hover:border-warm-300',
          )}
        >
          Hoy
        </button>
        <button
          type="button"
          onClick={() => onDateChange(tomorrow)}
          className="flex items-center gap-1.5 px-2.5 py-1.5 text-warm-600 hover:text-warm-800 hover:bg-warm-100 rounded-md"
        >
          <span className="text-warm-400 text-[12px] mr-1 tabular-nums">{fmtDateShort(parseLocalDate(tomorrow))}</span>
          <span>Mañana</span> <ChevronRight size={16} />
        </button>
        <div className="ml-2 hidden md:block">
          {/* DatePicker custom (ver components/ui/DatePicker.tsx) — reemplaza
              el input nativo que rompía el look junto a los pills Ayer/Hoy/Mañana. */}
          <DatePicker value={date} onChange={onDateChange} size="sm" />
        </div>
      </div>
    </header>
  )
}

// ===== URGENT BANNER =====

function UrgentBanner({
  count, onDismiss, onGo,
}: {
  count: number
  onDismiss: () => void
  onGo: () => void
}) {
  return (
    <div className="mx-5 lg:mx-8 mt-1 mb-4 rounded-xl border border-gold-200 bg-gold-50 px-4 py-3 flex items-center gap-3">
      <div className="w-8 h-8 rounded-lg bg-white flex items-center justify-center text-gold-600 border border-gold-200 flex-shrink-0">
        <Bell size={16} />
      </div>
      <div className="flex-1 text-[13.5px] min-w-0">
        <span className="font-semibold text-warm-800">
          {count} {count === 1 ? 'pago urgente pendiente' : 'pagos urgentes pendientes'} de validar
        </span>
        <span className="text-warm-600"> — citas de hoy aún sin comprobante confirmado.</span>
      </div>
      <button
        type="button"
        onClick={onGo}
        className="flex items-center gap-1 text-[13px] font-medium text-brand-800 hover:text-brand-900 shrink-0"
      >
        Ir a la cola <ArrowRight size={14} />
      </button>
      <button
        type="button"
        onClick={onDismiss}
        className="p-1 text-warm-400 hover:text-warm-700 rounded shrink-0"
        aria-label="Descartar"
      >
        <X size={15} />
      </button>
    </div>
  )
}

// ===== METRICS =====

/**
 * Filtros que las cards pueden activar. 'all' = sin filtro (estado
 * default). Las otras 3 corresponden 1:1 con los criterios visuales
 * de las cards Esperan/Confirmadas/No-show.
 */
export type AgendaFilter = 'all' | 'pending' | 'confirmed' | 'noShow'

export const FILTER_LABELS: Record<AgendaFilter, string> = {
  all:        'Todas',
  pending:    'Esperan comprobante',
  confirmed:  'Confirmadas',
  noShow:     'No-show',
}

/**
 * Aplica el filtro al array de citas. Definiciones:
 *  - pending   = Pending con depositStatus=AwaitingPayment (la cliente
 *                reservó pero no llegó comprobante validado todavía).
 *  - confirmed = Confirmed | InProgress | Completed (la cita "avanzó":
 *                pagó anticipo o no requería, ya está en curso o
 *                terminó). Hacemos esta agrupación generosa porque la
 *                card "confirmadas" es lo que la admin lee como "todo
 *                lo que funcionó bien".
 *  - noShow    = NoShow.
 */
export function filterAppointments(
  appointments: AppointmentResponse[],
  filter: AgendaFilter,
): AppointmentResponse[] {
  switch (filter) {
    case 'all':
      return appointments
    case 'pending':
      return appointments.filter(
        a => a.status === 'Pending' && a.depositStatus === 'AwaitingPayment',
      )
    case 'confirmed':
      return appointments.filter(
        a => a.status === 'Confirmed' || a.status === 'InProgress' || a.status === 'Completed',
      )
    case 'noShow':
      return appointments.filter(a => a.status === 'NoShow')
  }
}

function Metrics({
  metrics, activeFilter, onFilterChange,
}: {
  metrics: { total: number; pendingValidation: number; confirmed: number; noShow: number } | undefined
  activeFilter: AgendaFilter
  onFilterChange: (f: AgendaFilter) => void
}) {
  const m = metrics ?? { total: 0, pendingValidation: 0, confirmed: 0, noShow: 0 }
  const cards: Array<{
    key: AgendaFilter
    label: string
    hint: string
    icon: typeof Calendar
    accent: string
    /** Ring usado cuando la card está activa — color sutil que combina con el icon accent. */
    ringActive: string
  }> = [
    { key: 'all',       label: `${m.total} citas hoy`,              hint: 'Programadas',         icon: Calendar,    accent: 'bg-brand-50 text-brand-700 border-brand-100',  ringActive: 'ring-brand-300' },
    { key: 'pending',   label: `${m.pendingValidation} pendientes`, hint: 'Esperan comprobante', icon: Clock,       accent: 'bg-gold-50 text-gold-600 border-gold-200',     ringActive: 'ring-gold-300' },
    { key: 'confirmed', label: `${m.confirmed} confirmadas`,        hint: 'Pagadas o en curso',  icon: CheckCircle, accent: 'bg-brand-50 text-brand-700 border-brand-100',  ringActive: 'ring-brand-300' },
    { key: 'noShow',    label: `${m.noShow} no-show`,               hint: 'Cliente no asistió',  icon: AlertCircle, accent: 'bg-terra-100 text-terra-500 border-terra-300', ringActive: 'ring-terra-300' },
  ]
  return (
    <div className="px-5 lg:px-8 grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
      {cards.map(c => {
        const active = activeFilter === c.key
        return (
          <button
            key={c.key}
            type="button"
            onClick={() => onFilterChange(c.key)}
            title={active ? 'Quitar filtro' : `Filtrar por ${c.hint.toLowerCase()}`}
            className={cls(
              'bg-white border border-warm-150 rounded-xl px-4 py-3.5 flex items-center gap-3',
              'shadow-softer transition hover:border-warm-300 hover:shadow-soft text-left',
              active && cls('ring-2 ring-offset-1', c.ringActive),
            )}
          >
            <div className={cls(
              'w-9 h-9 rounded-lg flex items-center justify-center border',
              c.accent,
            )}>
              <c.icon size={18} />
            </div>
            <div className="min-w-0">
              <div className="text-[15px] font-semibold text-warm-800 leading-tight">{c.label}</div>
              <div className="text-[11.5px] text-warm-500 mt-0.5 leading-tight">{c.hint}</div>
            </div>
          </button>
        )
      })}
    </div>
  )
}

// ===== DETAIL PANEL =====

const STATUS_LOOK = {
  Pending:    { label: 'Pendiente',  dot: 'bg-gold-400',  tag: 'bg-gold-50 text-gold-600 border-gold-200' },
  Confirmed:  { label: 'Confirmada', dot: 'bg-brand-500', tag: 'bg-brand-50 text-brand-800 border-brand-100' },
  InProgress: { label: 'En curso',   dot: 'bg-gold-500',  tag: 'bg-gold-50 text-gold-600 border-gold-200' },
  Completed:  { label: 'Completada', dot: 'bg-warm-400',  tag: 'bg-warm-100 text-warm-600 border-warm-200' },
  Cancelled:  { label: 'Cancelada',  dot: 'bg-warm-300',  tag: 'bg-warm-50 text-warm-500 border-warm-200' },
  NoShow:     { label: 'No asistió', dot: 'bg-terra-500', tag: 'bg-terra-100 text-terra-500 border-terra-300' },
} as const

function DetailPanel({ appointment, onClose }: { appointment: AppointmentResponse; onClose: () => void }) {
  const confirm = useConfirmAppointment()
  const cancel = useCancelAppointment()
  const start = useStartAppointment()
  const complete = useCompleteAppointment()
  const noShow = useMarkNoShow()
  const [showReschedule, setShowReschedule] = useState(false)
  const [showPayment, setShowPayment] = useState(false)

  // Historial reciente del cliente — últimas 3 citas completadas (excluyendo
  // la actual). Sirve para que la estilista vea de un vistazo qué le ha hecho
  // antes a la clienta sin tener que abrir el CRM.
  const { data: history = [] } = useCustomerAppointments(appointment.customerId)
  const recentVisits = history
    .filter(a => a.status === 'Completed' && a.id !== appointment.id)
    .slice(0, 3)

  // Pagos asociados a ESTA cita. useCustomerPayments trae todos los del
  // cliente; los filtramos por appointmentId. Es un poco wasteful pero
  // evita un endpoint extra y comparte caché con el tab Pagos del CRM.
  const { data: customerPayments = [] } = useCustomerPayments(appointment.customerId)
  const appointmentPayments = customerPayments.filter(p => p.appointmentId === appointment.id)
  const totalPaid = appointmentPayments.reduce((acc, p) => acc + p.total, 0)

  const tone = toneOf(appointment.customerId)
  const isAwaitingPayment = appointment.depositStatus === 'AwaitingPayment' && appointment.status === 'Pending'
  const status = STATUS_LOOK[appointment.status]
  const statusLabel = isAwaitingPayment ? 'Pago pendiente' : status.label
  const statusTag = isAwaitingPayment ? STATUS_LOOK.Pending.tag : status.tag
  const statusDot = isAwaitingPayment ? STATUS_LOOK.Pending.dot : status.dot

  // Cuando cambias de cita seleccionada, forzar el body a posición 0 para
  // que NUNCA se vea scrolleado al fondo al abrirlo (lo que pasaba antes
  // por interacción con el scroll del documento padre).
  const bodyRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (bodyRef.current) bodyRef.current.scrollTop = 0
  }, [appointment.id])

  const canConfirm = appointment.status === 'Pending' && appointment.depositStatus !== 'AwaitingPayment'
  const canStart = appointment.status === 'Confirmed'
  const canComplete = appointment.status === 'InProgress'
  const canCancel = appointment.status === 'Pending' || appointment.status === 'Confirmed'
  const canNoShow = appointment.status === 'Confirmed' || appointment.status === 'Pending'

  return (
    <div className="flex flex-col h-full">
      {/* header del panel — compacto para que todo el detalle quepa sin scroll */}
      <div className="px-5 pt-5 pb-4 border-b border-warm-150">
        <div className="flex items-center justify-between mb-4">
          <div className="text-[11px] uppercase tracking-[0.14em] text-warm-400 font-medium">
            Detalle de la cita
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 -mr-1.5 text-warm-500 hover:text-warm-800 hover:bg-warm-100 rounded-md"
            aria-label="Cerrar"
          >
            ✕
          </button>
        </div>

        <div className="flex items-center gap-3.5">
          <div className={cls(
            'w-12 h-12 rounded-full flex items-center justify-center font-medium text-[14px] ring-2 ring-white shadow-soft flex-shrink-0 placeholder-stripes',
          )}>
            <span className={cls('px-1.5 rounded font-serif text-[15px]', tone.fg, 'bg-white/85')}>
              {initialsOf(appointment.customerName)}
            </span>
          </div>
          <div className="min-w-0 flex-1">
            <div className="text-[18px] font-semibold text-warm-800 leading-tight truncate">
              {appointment.customerName}
            </div>
            <div className="text-[12.5px] text-warm-500 mt-0.5 tabular-nums flex items-center gap-1.5">
              <MessageCircle size={12} className="text-warm-400" />
              {appointment.customerPhone}
            </div>
          </div>
        </div>

        <div className="mt-4 flex items-center gap-2 flex-wrap">
          <span className={cls(
            'text-[11px] font-semibold px-2.5 py-1 rounded-md uppercase tracking-wider flex items-center gap-1.5 border',
            statusTag,
          )}>
            <span className={cls('w-1.5 h-1.5 rounded-full', statusDot)} />
            {statusLabel}
          </span>
          <span className="text-[12px] text-warm-500 tabular-nums font-medium">
            {formatLocalTime(appointment.startAt)} – {formatLocalTime(appointment.endAt)}
          </span>
        </div>
      </div>

      {/* body scrollable — solo si el viewport no acomoda todo el contenido.
          Padding y spacing compactos para minimizar la probabilidad de
          necesitar scroll en monitores promedio. */}
      <div ref={bodyRef} className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
        <Section title="Servicio">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="text-[15px] font-medium text-warm-800">{appointment.serviceName}</div>
              <div className="text-[12.5px] text-warm-500 mt-1">
                con <span className="text-warm-700">{appointment.stylistName}</span> · {appointment.durationMinutes} min
              </div>
            </div>
            <div className="font-serif text-[20px] text-warm-800 tabular-nums shrink-0 leading-none">
              {fmtCop(appointment.priceSnapshot)}
            </div>
          </div>
        </Section>

        {/* Estado del dinero de la cita — el cuadro completo:
              Total servicio
              − Anticipo (validado / esperando)
              − Pagado en sitio (suma de Payments registrados)
              = Falta cobrar (o "Cobro completo ✓" si 0)
            Es la vista que la recepcionista mira para saber si tiene
            que cobrar algo más antes de despedir a la cliente. */}
        <Section title="Pago">
          {(() => {
            const total = appointment.priceSnapshot
            const anticipoValidado = appointment.validatedDepositAmount
            const cobradoEnSitio = totalPaid
            const cubierto = anticipoValidado + cobradoEnSitio
            const falta = Math.max(0, total - cubierto)
            const cobroCompleto = falta === 0 && cubierto > 0
            const esperandoAnticipo = appointment.depositStatus === 'AwaitingPayment'

            return (
              <div className="rounded-lg border border-warm-150 bg-warm-50/40 divide-y divide-warm-150">
                {/* Total */}
                <div className="flex justify-between items-center px-3.5 py-2 text-[13px]">
                  <span className="text-warm-600">Total servicio</span>
                  <span className="text-warm-800 tabular-nums font-medium">{fmtCop(total)}</span>
                </div>

                {/* Anticipo — solo si requiere anticipo */}
                {appointment.depositStatus !== 'NotRequired' && (
                  <div className="flex justify-between items-center px-3.5 py-2 text-[13px]">
                    <span className="flex items-center gap-2 text-warm-600">
                      − Anticipo
                      <span className={cls(
                        'text-[10px] font-semibold px-1.5 py-0.5 rounded uppercase tracking-wider border',
                        appointment.depositStatus === 'Validated'
                          ? 'bg-brand-50 text-brand-700 border-brand-100'
                          : 'bg-gold-50 text-gold-600 border-gold-200',
                      )}>
                        {appointment.depositStatus === 'Validated' ? 'Validado' : 'Esperando'}
                      </span>
                    </span>
                    <span className="text-warm-800 tabular-nums font-medium">
                      {fmtCop(anticipoValidado || appointment.depositAmount)}
                    </span>
                  </div>
                )}

                {/* Cobrado en sitio — solo si hay payments */}
                {cobradoEnSitio > 0 && (
                  <div className="flex justify-between items-center px-3.5 py-2 text-[13px]">
                    <span className="text-warm-600">− Cobrado en sitio</span>
                    <span className="text-warm-800 tabular-nums font-medium">{fmtCop(cobradoEnSitio)}</span>
                  </div>
                )}

                {/* Falta cobrar — destacado */}
                <div className={cls(
                  'flex justify-between items-center px-3.5 py-2.5',
                  cobroCompleto
                    ? 'bg-brand-50/60 text-brand-800'
                    : falta > 0
                      ? 'bg-gold-50/60 text-gold-700'
                      : 'text-warm-600',
                )}>
                  <span className="text-[12px] uppercase tracking-wide font-semibold">
                    {cobroCompleto ? '✓ Cobro completo'
                      : esperandoAnticipo && cobradoEnSitio === 0 ? 'Esperando anticipo'
                        : 'Falta cobrar'}
                  </span>
                  {!cobroCompleto && (
                    <span className="font-serif text-[18px] tabular-nums">{fmtCop(falta)}</span>
                  )}
                </div>
              </div>
            )
          })()}
        </Section>

        {/* Pagos recibidos para esta cita. Si no hay, se muestra hint sutil
            para invitar a registrar el primero. Confirma visualmente que el
            pago se guardó (evita el bug del doble-registro por inseguridad). */}
        <Section title={`Pagos recibidos${appointmentPayments.length > 0 ? ` (${appointmentPayments.length})` : ''}`}>
          {appointmentPayments.length === 0 ? (
            <div className="text-[12.5px] text-warm-500 italic">
              Sin pagos registrados todavía.
            </div>
          ) : (
            <div className="space-y-1.5">
              {appointmentPayments.map(p => {
                const badge = getPaymentBadge(p.method, p.provider)
                return (
                  <div
                    key={p.id}
                    className="flex items-center justify-between py-1.5 border-b border-warm-100 last:border-0"
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      <span className={cls(
                        'text-[10.5px] font-semibold px-1.5 py-0.5 rounded uppercase tracking-wider',
                        badge.className,
                      )}>
                        {badge.label}
                      </span>
                      {p.reference && (
                        <span className="font-mono text-[11px] text-warm-500 truncate">
                          {p.reference}
                        </span>
                      )}
                    </div>
                    <span className="text-[13px] text-warm-800 tabular-nums font-medium shrink-0">
                      {fmtCop(p.total)}
                    </span>
                  </div>
                )
              })}
              <div className="flex items-center justify-between pt-2 mt-1 border-t border-warm-200">
                <span className="text-[11.5px] uppercase tracking-wide text-warm-500 font-medium">
                  Total recibido
                </span>
                <span className="font-serif text-[16px] text-warm-800 tabular-nums">
                  {fmtCop(totalPaid)}
                </span>
              </div>
            </div>
          )}
        </Section>

        {/* Ficha técnica resumida — alergias inferidas de las notas + última fórmula */}
        <Section title="Ficha técnica">
          <ul className="space-y-2 text-[12.5px]">
            <li className="flex items-start gap-2.5">
              <AlertTriangle size={14} className="mt-0.5 text-gold-500 flex-shrink-0" />
              <div className="min-w-0">
                <div className="text-warm-500 text-[11.5px]">Alergias / advertencias</div>
                <div className="text-warm-800">
                  {appointment.notes && /alerg|formol|amon[ií]aco|reacci|sens|paraben/i.test(appointment.notes)
                    ? appointment.notes
                    : 'Ninguna registrada.'}
                </div>
              </div>
            </li>
            <li className="flex items-start gap-2.5">
              <Droplet size={14} className="mt-0.5 text-brand-700 flex-shrink-0" />
              <div className="min-w-0">
                <div className="text-warm-500 text-[11.5px]">Última fórmula / técnica</div>
                <div className="text-warm-800">
                  {recentVisits[0]
                    ? `${recentVisits[0].serviceName} · ${fmtMonth(recentVisits[0].startAt)}`
                    : 'Sin historial todavía.'}
                </div>
              </div>
            </li>
            <li className="flex items-start gap-2.5">
              <Heart size={14} className="mt-0.5 text-terra-500 flex-shrink-0" />
              <div className="min-w-0">
                <div className="text-warm-500 text-[11.5px]">Preferencias</div>
                <div className="text-warm-800">
                  {appointment.notes || 'Sin preferencias registradas.'}
                </div>
              </div>
            </li>
          </ul>
        </Section>

        {/* Últimas visitas — sacadas del historial del cliente */}
        <Section title="Visitas recientes">
          {recentVisits.length === 0 ? (
            <div className="text-[12.5px] text-warm-500 italic">
              Es su primera cita.
            </div>
          ) : (
            <div className="space-y-1.5 text-[12.5px]">
              {recentVisits.map(v => (
                <div
                  key={v.id}
                  className="flex items-center justify-between py-1.5 border-b border-warm-100 last:border-0"
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <span className="text-warm-400 tabular-nums w-12 flex-shrink-0">
                      {fmtMonth(v.startAt).split(' ').slice(0, 2).join(' ')}
                    </span>
                    <span className="text-warm-700 truncate">{v.serviceName}</span>
                  </div>
                  <span className="text-warm-500 tabular-nums shrink-0 ml-2">
                    {fmtCop(v.priceSnapshot)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </Section>
      </div>

      {/* footer actions — 4 botones SIEMPRE visibles como en el mockup.
          La acción "Marcar completada" reemplaza dinámicamente a "Iniciar"
          o "Confirmar" según en qué etapa está la cita, para no tener 6 botones.
          Los que no aplican quedan deshabilitados (gris). */}
      <div className="border-t border-warm-150 p-3 grid grid-cols-2 gap-2">
        {/* Reagendar — solo disponible para citas que aún no empezaron */}
        <button
          type="button"
          onClick={() => setShowReschedule(true)}
          disabled={!canCancel}  // mismo gate que Cancelar: Pending/Confirmed
          title={canCancel ? 'Mover la cita a otro día u hora' : 'No se puede reagendar en este estado'}
          className={cls(
            'flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg border text-[12.5px] font-medium',
            canCancel
              ? 'bg-white border-warm-200 hover:border-warm-300 text-warm-700'
              : 'bg-white border-warm-200 text-warm-500 cursor-not-allowed opacity-60',
          )}
        >
          <CalendarReschedule /> Reagendar
        </button>

        {/* Cancelar */}
        <button
          type="button"
          onClick={() => {
            const reason = window.prompt('Razón de la cancelación (opcional):') ?? undefined
            cancel.mutate({ id: appointment.id, reason })
          }}
          disabled={!canCancel || cancel.isPending}
          className={cls(
            'flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg border text-[12.5px] font-medium',
            canCancel
              ? 'bg-white border-warm-200 hover:border-warm-300 text-warm-700'
              : 'bg-white border-warm-200 text-warm-500 cursor-not-allowed opacity-60',
          )}
        >
          <X size={13} /> Cancelar
        </button>

        {/* Acción primaria según estado: Confirmar / Iniciar / Marcar completada.
            Si la cita está Cancelled/NoShow/Completed → deshabilitado "Marcar completada". */}
        {canConfirm ? (
          <button
            type="button"
            onClick={() => confirm.mutate(appointment.id)}
            disabled={confirm.isPending}
            className="flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium"
          >
            <CheckCircle size={14} /> Confirmar
          </button>
        ) : canStart ? (
          <button
            type="button"
            onClick={() => start.mutate(appointment.id)}
            disabled={start.isPending}
            className="flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium"
          >
            Iniciar atención
          </button>
        ) : canComplete ? (
          <button
            type="button"
            onClick={() => complete.mutate(appointment.id)}
            disabled={complete.isPending}
            className="flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-warm-100 hover:bg-warm-200 text-warm-800 text-[12.5px] font-medium"
          >
            <CheckCircle size={14} /> Marcar completada
          </button>
        ) : canNoShow ? (
          <button
            type="button"
            onClick={() => noShow.mutate(appointment.id)}
            disabled={noShow.isPending}
            className="flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-white border border-warm-200 hover:border-warm-300 text-warm-700 text-[12.5px] font-medium"
          >
            <AlertCircle size={13} /> No-show
          </button>
        ) : (
          <button
            type="button"
            disabled
            className="flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-warm-100 text-warm-500 text-[12.5px] font-medium cursor-not-allowed opacity-60"
          >
            <CheckCircle size={14} /> Marcar completada
          </button>
        )}

        {/* Cobrar — disponible para citas InProgress o Completed.
            Cuando la cita ya pasó, registrar el pago es lo más importante
            que puede hacer la recepcionista; por eso va full-width arriba
            de WhatsApp.

            Si el saldo (precio − anticipo − pagos previos) ya es 0,
            mostramos "Pagado ✓" en lugar de un botón habilitado —
            cobrar dos veces no tiene sentido. */}
        {(appointment.status === 'InProgress' || appointment.status === 'Completed') && (
          (() => {
            const saldoRestante = Math.max(
              0,
              appointment.priceSnapshot - appointment.validatedDepositAmount - totalPaid,
            )
            const fullyPaid = saldoRestante === 0
            return fullyPaid ? (
              <div className="col-span-2 flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-brand-50 border border-brand-200 text-brand-800 text-[12.5px] font-medium">
                <Wallet size={14} /> Servicio pagado completo
              </div>
            ) : (
              <button
                type="button"
                onClick={() => setShowPayment(true)}
                className="col-span-2 flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-gold-500 hover:bg-gold-600 text-white text-[12.5px] font-medium"
              >
                <Wallet size={14} /> Registrar pago · falta ${saldoRestante.toLocaleString('es-CO')}
              </button>
            )
          })()
        )}

        {/* WhatsApp — siempre disponible, ocupa toda la fila */}
        <a
          href={whatsappLink(appointment.customerPhone)}
          target="_blank"
          rel="noreferrer"
          className="col-span-2 flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-lg bg-[#25D366] hover:brightness-110 text-white text-[12.5px] font-medium"
        >
          <MessageCircle size={14} /> Enviar WhatsApp
        </a>
      </div>

      {showReschedule && (
        <RescheduleModal
          appointment={appointment}
          onClose={() => setShowReschedule(false)}
        />
      )}
      {showPayment && (
        <RegisterPaymentModal
          appointment={appointment}
          alreadyPaid={totalPaid}
          onClose={() => setShowPayment(false)}
        />
      )}
    </div>
  )
}

/** Icono de "reagendar" — usa Calendar de lucide con un pequeño clock decorativo.
 *  Lucide tiene `CalendarClock` pero no lo importamos arriba para no expandir
 *  el bundle; usamos el Calendar simple. */
function CalendarReschedule() {
  return <Calendar size={13} />
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[10.5px] uppercase tracking-[0.14em] font-medium text-warm-400 mb-2">
        {title}
      </div>
      {children}
    </div>
  )
}

// ===== Helpers de fecha/hora =====

function formatLocalDate(d: Date): string {
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

function parseLocalDate(s: string): Date {
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y, m - 1, d)
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d)
  r.setDate(r.getDate() + n)
  return r
}

const MESES = ['enero','febrero','marzo','abril','mayo','junio',
               'julio','agosto','septiembre','octubre','noviembre','diciembre']
const DIAS = ['domingo','lunes','martes','miércoles','jueves','viernes','sábado']
const MESES_SHORT = ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic']

function fmtDateLong(d: Date): string {
  return `${DIAS[d.getDay()]} ${d.getDate()} de ${MESES[d.getMonth()]}`
}

function fmtDateShort(d: Date): string {
  return `${d.getDate()} ${MESES_SHORT[d.getMonth()]}`
}

function formatLocalTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-CO', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}
