import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Calendar, Wallet, Banknote, AlertTriangle, ArrowRight, Clock,
  TrendingUp, Sparkles,
} from 'lucide-react'
import { AppShell } from '@/components/layout/AppShell'
import { useAuth } from '@/features/auth/useAuth'
import { getDashboardSummary, type DashboardSummary } from '@/api/dashboard'
import { cls } from '@/lib/cls'

/**
 * Home post-login del SalonAdmin (y Receptionist también lo ve, con la
 * misma data pero el sidebar ya está filtrado a sus items).
 *
 * Una pantalla, todo de un vistazo:
 *   - Saludo personalizado + nombre del salón
 *   - Banner de acciones pendientes (vouchers, cierre caja) si las hay
 *   - 4 stat-cards clickeables → llevan a su módulo
 *   - Próxima cita
 *   - 4 quick links
 *
 * Polling cada 60s para que los números no envejezcan mientras la
 * pestaña queda abierta.
 */
export function Dashboard() {
  const { user } = useAuth()
  const navigate = useNavigate()

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: getDashboardSummary,
    refetchInterval: 60_000,
  })

  if (!user) return null
  const firstName = user.fullName.split(' ')[0]

  return (
    <AppShell>
      <div className="px-6 lg:px-10 py-8 max-w-6xl mx-auto">
        {/* Header */}
        <div className="mb-7">
          <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
            {user.tenantName?.toUpperCase() ?? 'BELLASYNC'}
          </div>
          <h1 className="font-serif text-[40px] lg:text-[46px] leading-[1.02] tracking-tight text-warm-800 mt-1">
            Buen día, {firstName}
          </h1>
          <p className="text-[14px] text-warm-600 mt-2.5">{formatToday()}</p>
        </div>

        {isLoading || !data ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-28 rounded-xl bg-warm-100 animate-pulse" />
              ))}
            </div>
            <div className="h-44 rounded-xl bg-warm-100 animate-pulse" />
          </div>
        ) : (
          <DashboardContent data={data} onNav={navigate} />
        )}
      </div>
    </AppShell>
  )
}

function DashboardContent({
  data, onNav,
}: {
  data: DashboardSummary
  onNav: (path: string) => void
}) {
  return (
    <>
      <PendingActionsBanner data={data} onNav={onNav} />

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 mb-6">
        <StatCard
          label="Citas hoy"
          value={data.todayAppointmentsCount.toString()}
          sub={`${data.todayCompletedCount} completadas`}
          icon={<Calendar size={18} />}
          tone="brand"
          onClick={() => onNav('/agenda')}
        />
        <StatCard
          label="Ingresos hoy"
          value={fmt(data.todayRevenue)}
          sub={data.todayRevenue > 0 ? 'Efectivo + transferencias' : 'Sin movimientos'}
          icon={<Banknote size={18} />}
          tone="gold"
          onClick={() => onNav('/caja')}
        />
        <StatCard
          label="Esta semana"
          value={fmt(data.weekRevenue)}
          sub={`${data.weekAppointmentsCount} citas`}
          icon={<TrendingUp size={18} />}
          tone="warm"
          onClick={() => onNav('/reportes')}
        />
        <StatCard
          label="Pendientes"
          value={data.pendingVouchersCount.toString()}
          sub="Comprobantes por validar"
          icon={<Wallet size={18} />}
          tone={data.pendingVouchersCount > 0 ? 'terra' : 'warm'}
          onClick={() => onNav('/validacion')}
        />
      </div>

      <NextAppointmentCard
        next={data.nextAppointment}
        pendingCount={data.todayPendingCount}
        onNav={onNav}
      />

      <div className="mt-7">
        <div className="text-[12.5px] font-semibold text-warm-800 mb-3">
          Accesos rápidos
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <QuickLink to="/agenda" label="Nueva cita" icon={<Calendar size={16} />} onNav={onNav} />
          <QuickLink to="/clientes" label="Clientes" icon={<Sparkles size={16} />} onNav={onNav} />
          <QuickLink to="/validacion" label="Validar pagos" icon={<Wallet size={16} />} onNav={onNav} />
          <QuickLink to="/caja" label="Cerrar caja" icon={<Banknote size={16} />} onNav={onNav} />
        </div>
      </div>
    </>
  )
}

function PendingActionsBanner({
  data, onNav,
}: {
  data: DashboardSummary
  onNav: (path: string) => void
}) {
  const items: { label: string; path: string; icon: typeof Wallet }[] = []
  if (data.pendingVouchersCount > 0) {
    items.push({
      label: `${data.pendingVouchersCount} comprobante${data.pendingVouchersCount === 1 ? '' : 's'} por validar`,
      path: '/validacion',
      icon: Wallet,
    })
  }
  if (data.cashClosingPending) {
    items.push({
      label: 'Caja del día sin cerrar',
      path: '/caja',
      icon: Banknote,
    })
  }
  if (items.length === 0) return null

  return (
    <div className="mb-6 rounded-xl bg-gold-50 border border-gold-200 p-4">
      <div className="flex items-center gap-2 mb-2">
        <AlertTriangle size={16} className="text-gold-600" />
        <span className="text-[12.5px] font-semibold text-gold-700 tracking-wide uppercase">
          Acciones pendientes
        </span>
      </div>
      <div className="space-y-1.5">
        {items.map((it) => {
          const Icon = it.icon
          return (
            <button
              key={it.path}
              type="button"
              onClick={() => onNav(it.path)}
              className="w-full flex items-center justify-between gap-3 text-left text-[13px] text-warm-800 hover:text-brand-700 transition group"
            >
              <span className="flex items-center gap-2">
                <Icon size={14} className="text-warm-500 group-hover:text-brand-700 transition" />
                {it.label}
              </span>
              <ArrowRight size={14} className="text-warm-400 group-hover:text-brand-700 transition" />
            </button>
          )
        })}
      </div>
    </div>
  )
}

function StatCard({
  label, value, sub, icon, tone, onClick,
}: {
  label: string
  value: string
  sub: string
  icon: React.ReactNode
  tone: 'brand' | 'gold' | 'warm' | 'terra'
  onClick: () => void
}) {
  const toneCls: Record<string, string> = {
    brand: 'text-brand-700 bg-brand-50',
    gold:  'text-gold-700  bg-gold-50',
    warm:  'text-warm-700  bg-warm-100',
    terra: 'text-terra-700 bg-terra-100',
  }
  return (
    <button
      type="button"
      onClick={onClick}
      className="text-left rounded-xl border border-warm-150 bg-white p-4 hover:shadow-soft transition"
    >
      <div className="flex items-center justify-between mb-2">
        <span className="text-[10.5px] tracking-[0.14em] uppercase text-warm-500 font-medium">
          {label}
        </span>
        <span className={cls('w-7 h-7 rounded-lg flex items-center justify-center', toneCls[tone])}>
          {icon}
        </span>
      </div>
      <div className="font-serif text-[24px] sm:text-[28px] tabular-nums text-warm-800 leading-none">
        {value}
      </div>
      <div className="text-[11.5px] text-warm-500 mt-1.5">{sub}</div>
    </button>
  )
}

function NextAppointmentCard({
  next, pendingCount, onNav,
}: {
  next: DashboardSummary['nextAppointment']
  pendingCount: number
  onNav: (path: string) => void
}) {
  if (!next) {
    return (
      <div className="rounded-xl border border-warm-150 bg-white p-5">
        <div className="text-[12.5px] font-semibold text-warm-800 mb-2 flex items-center gap-2">
          <Clock size={14} className="text-warm-400" />
          Próxima cita
        </div>
        <div className="text-[13px] text-warm-500">
          {pendingCount > 0
            ? `No hay más citas restantes hoy. ${pendingCount} en agenda total.`
            : 'No hay citas agendadas para hoy.'}
        </div>
      </div>
    )
  }

  const hhmm = (iso: string) =>
    new Date(iso).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' })

  const accent = next.stylistColor ?? '#5b8a72'

  return (
    <div className="rounded-xl border border-warm-150 bg-white p-5 hover:shadow-soft transition">
      <div className="text-[12.5px] font-semibold text-warm-800 mb-3 flex items-center gap-2">
        <Clock size={14} className="text-warm-400" />
        Próxima cita
      </div>

      <div className="flex items-start gap-4 flex-wrap">
        <div
          className="w-1.5 self-stretch min-h-[60px] rounded-full"
          style={{ backgroundColor: accent }}
        />
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline gap-2 flex-wrap">
            <div className="font-serif text-[22px] text-warm-800 truncate">
              {next.customerName}
            </div>
            <div className="text-[12.5px] text-warm-500">
              · {hhmm(next.startAt)} – {hhmm(next.endAt)}
            </div>
          </div>
          <div className="text-[13px] text-warm-600 mt-1">
            {next.serviceName} con {next.stylistName}
          </div>
        </div>
        <button
          type="button"
          onClick={() => onNav('/agenda')}
          className="text-[12.5px] font-medium text-brand-700 hover:text-brand-800 flex items-center gap-1"
        >
          Ver agenda <ArrowRight size={13} />
        </button>
      </div>
    </div>
  )
}

function QuickLink({
  to, label, icon, onNav,
}: {
  to: string
  label: string
  icon: React.ReactNode
  onNav: (path: string) => void
}) {
  return (
    <button
      type="button"
      onClick={() => onNav(to)}
      className="rounded-xl border border-warm-150 bg-white p-3 sm:p-4 flex items-center gap-2.5 text-[13px] text-warm-700 hover:text-brand-700 hover:border-warm-300 transition"
    >
      <span className="text-warm-500">{icon}</span>
      <span className="font-medium">{label}</span>
    </button>
  )
}

function formatToday(): string {
  return new Date().toLocaleDateString('es-CO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

function fmt(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-CO')
}
