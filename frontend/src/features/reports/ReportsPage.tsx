import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  BarChart3, TrendingUp, TrendingDown, Users, Wallet, Calendar,
  Sparkles, Scissors,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { DatePicker } from '@/components/ui'
import {
  getReportsSummary,
  type ReportsSummary,
  type TopServiceRow,
  type TopStylistRow,
  type WeeklyRevenuePoint,
} from '@/api/reports'

/**
 * `/reportes` — Dashboard con KPIs del período.
 *
 * Layout:
 *   - Header con período picker (presets: este mes / últimos 30 / 90 / custom)
 *   - 4 KPI cards: Ingresos · # Citas · Ticket promedio · Clientes nuevos
 *   - Grid 2x con Top Servicios + Top Estilistas
 *   - Sparkline barras: Ingresos por semana
 *   - Split nuevos vs recurrentes
 *
 * Backend single-query — un solo /api/Reports/summary para todo. Si la
 * página crece y necesita drilldown, se agregan endpoints específicos.
 */

// ───────────────────────────────────────────────────────────────────────
// Helpers
// ───────────────────────────────────────────────────────────────────────

function todayCO(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function addDaysIso(iso: string, days: number): string {
  const [y, m, d] = iso.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  date.setDate(date.getDate() + days)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function firstOfMonthIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`
}

function fmtCop(n: number): string {
  return '$' + n.toLocaleString('es-CO', { maximumFractionDigits: 0 })
}

function fmtPct(n: number): string {
  const sign = n >= 0 ? '+' : ''
  return `${sign}${n.toFixed(1)}%`
}

function formatHumanDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Intl.DateTimeFormat('es-CO', {
    day: 'numeric', month: 'short',
  }).format(new Date(y, m - 1, d)).replace(/\.$/, '')
}

// ───────────────────────────────────────────────────────────────────────
// Period presets
// ───────────────────────────────────────────────────────────────────────

const PRESETS: { id: string; label: string; range: () => [string, string] }[] = [
  {
    id: 'thisMonth',
    label: 'Este mes',
    range: () => [firstOfMonthIso(), todayCO()],
  },
  {
    id: 'last30',
    label: 'Últimos 30 días',
    range: () => [addDaysIso(todayCO(), -29), todayCO()],
  },
  {
    id: 'last90',
    label: 'Últimos 90 días',
    range: () => [addDaysIso(todayCO(), -89), todayCO()],
  },
]

// ───────────────────────────────────────────────────────────────────────

export function ReportsPage() {
  const { user } = useAuth()
  const [activePreset, setActivePreset] = useState<string>('thisMonth')
  const [from, setFrom] = useState<string>(() => firstOfMonthIso())
  const [to, setTo] = useState<string>(() => todayCO())

  const applyPreset = (id: string) => {
    const p = PRESETS.find(x => x.id === id)
    if (!p) return
    const [f, t] = p.range()
    setFrom(f)
    setTo(t)
    setActivePreset(id)
  }

  const onCustomFrom = (v: string) => { setFrom(v); setActivePreset('custom') }
  const onCustomTo = (v: string) => { setTo(v); setActivePreset('custom') }

  const { data, isLoading, error } = useQuery({
    queryKey: ['reports', 'summary', from, to],
    queryFn: () => getReportsSummary(from, to),
    enabled: !!from && !!to,
  })

  return (
    <div className="px-5 sm:px-6 lg:px-10 py-6 lg:py-8 max-w-[1200px] mx-auto">
      {/* Header */}
      <div className="mb-6">
        <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          {user?.tenantName ?? 'Salón'} · Análisis
        </div>
        <h1 className="font-serif text-[30px] sm:text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
          Reportes
        </h1>
        <p className="text-[13px] text-warm-500 mt-1.5">
          Visión general de la operación. Período: <span className="text-warm-700 font-medium">{formatHumanDate(from)} → {formatHumanDate(to)}</span>
        </p>
      </div>

      {/* Period picker — pills + 2 date pickers */}
      <div className="flex items-center gap-2 flex-wrap mb-6">
        {PRESETS.map(p => (
          <button
            key={p.id}
            type="button"
            onClick={() => applyPreset(p.id)}
            className={cls(
              'px-3 py-1.5 rounded-md text-[12.5px] font-medium transition',
              activePreset === p.id
                ? 'bg-warm-900 text-white'
                : 'text-warm-600 hover:text-warm-800 hover:bg-warm-100',
            )}
          >
            {p.label}
          </button>
        ))}
        <div className="flex items-center gap-1.5 ml-2">
          <DatePicker value={from} onChange={onCustomFrom} max={to} size="sm" />
          <span className="text-warm-400 text-[12px]">a</span>
          <DatePicker value={to} onChange={onCustomTo} min={from} size="sm" />
        </div>
      </div>

      {/* Estados */}
      {isLoading && (
        <div className="text-center py-20 text-[13px] text-warm-500">Cargando reportes…</div>
      )}
      {error && (
        <div className="text-center py-20 text-[13px] text-terra-500">
          No se pudo cargar el reporte.
        </div>
      )}

      {data && <Dashboard data={data} />}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Dashboard (rendered solo cuando data está disponible)
// ───────────────────────────────────────────────────────────────────────

function Dashboard({ data }: { data: ReportsSummary }) {
  return (
    <>
      {/* 4 KPI cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
        <KpiCard
          label="Ingresos"
          value={fmtCop(data.totalRevenue)}
          changePct={data.revenueChangePct}
          icon={<Wallet size={16} strokeWidth={1.8} />}
          tone="brand"
        />
        <KpiCard
          label="Citas"
          value={data.appointmentsCount.toLocaleString('es-CO')}
          sublabel="atendidas + agendadas"
          icon={<Calendar size={16} strokeWidth={1.8} />}
        />
        <KpiCard
          label="Ticket promedio"
          value={fmtCop(data.averageTicket)}
          sublabel="por cita"
          icon={<TrendingUp size={16} strokeWidth={1.8} />}
        />
        <KpiCard
          label="Clientes nuevos"
          value={data.newCustomersCount.toLocaleString('es-CO')}
          sublabel="primera visita"
          icon={<Users size={16} strokeWidth={1.8} />}
          tone="gold"
        />
      </div>

      {/* Tendencia semanal */}
      <WeeklyTrend points={data.weeklyRevenue} />

      {/* Grid: Top servicios + Top estilistas */}
      <div className="grid lg:grid-cols-2 gap-4 mt-5">
        <TopServicesCard rows={data.topServices} />
        <TopStylistsCard rows={data.topStylists} />
      </div>

      {/* Nuevos vs recurrentes */}
      <NewVsReturningCard
        newCount={data.newCustomerAppointments}
        returningCount={data.returningCustomerAppointments}
      />
    </>
  )
}

// ───────────────────────────────────────────────────────────────────────
// KPI Card
// ───────────────────────────────────────────────────────────────────────

function KpiCard({
  label, value, sublabel, changePct, icon, tone = 'default',
}: {
  label: string
  value: string
  sublabel?: string
  changePct?: number | null
  icon: React.ReactNode
  tone?: 'default' | 'brand' | 'gold'
}) {
  const toneCls = tone === 'brand'
    ? 'bg-brand-700 text-white'
    : tone === 'gold'
    ? 'bg-gold-50 border border-gold-200'
    : 'bg-white border border-warm-150'

  const labelCls = tone === 'brand'
    ? 'text-brand-100'
    : 'text-warm-500'

  const valueCls = tone === 'brand'
    ? 'text-white'
    : 'text-warm-800'

  const iconCls = tone === 'brand'
    ? 'text-brand-200'
    : tone === 'gold'
    ? 'text-gold-600'
    : 'text-warm-400'

  return (
    <div className={cls('rounded-2xl p-4 sm:p-5 shadow-softer', toneCls)}>
      <div className="flex items-start justify-between">
        <div className={cls('text-[10.5px] tracking-[0.18em] uppercase font-medium', labelCls)}>
          {label}
        </div>
        <span className={iconCls}>{icon}</span>
      </div>
      <div className={cls('font-serif text-[26px] sm:text-[32px] tabular-nums leading-none mt-2', valueCls)}>
        {value}
      </div>
      {sublabel && (
        <div className={cls('text-[11.5px] mt-1.5', tone === 'brand' ? 'text-brand-100' : 'text-warm-500')}>
          {sublabel}
        </div>
      )}
      {changePct !== undefined && changePct !== null && (
        <div className={cls(
          'text-[11.5px] mt-1.5 flex items-center gap-1 tabular-nums font-medium',
          changePct >= 0
            ? (tone === 'brand' ? 'text-brand-100' : 'text-brand-700')
            : (tone === 'brand' ? 'text-gold-200' : 'text-terra-500'),
        )}>
          {changePct >= 0 ? <TrendingUp size={11} /> : <TrendingDown size={11} />}
          {fmtPct(changePct)} <span className="font-normal opacity-75">vs período anterior</span>
        </div>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Weekly trend (bar chart CSS)
// ───────────────────────────────────────────────────────────────────────

function WeeklyTrend({ points }: { points: WeeklyRevenuePoint[] }) {
  const maxRev = useMemo(
    () => Math.max(1, ...points.map(p => p.revenue)),
    [points],
  )

  return (
    <div className="bg-white rounded-2xl border border-warm-150 shadow-softer p-4 sm:p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <BarChart3 size={16} className="text-brand-700" strokeWidth={1.8} />
          <h3 className="font-serif text-[17px] text-warm-800">Ingresos por semana</h3>
        </div>
        <span className="text-[11px] text-warm-400">
          {points.length} {points.length === 1 ? 'semana' : 'semanas'}
        </span>
      </div>

      {points.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin ingresos en el período.
        </div>
      ) : (
        <div className="flex items-end gap-1.5 h-32 sm:h-40">
          {points.map(p => {
            const heightPct = Math.max(2, (p.revenue / maxRev) * 100)
            return (
              <div
                key={p.weekStart}
                className="flex-1 flex flex-col items-center gap-1.5 group min-w-0"
                title={`Semana del ${formatHumanDate(p.weekStart)}: ${fmtCop(p.revenue)}`}
              >
                <div className="flex-1 w-full flex items-end">
                  <div
                    className="w-full bg-brand-600 rounded-t-md group-hover:bg-brand-700 transition"
                    style={{ height: `${heightPct}%` }}
                  />
                </div>
                <div className="text-[9.5px] text-warm-400 truncate w-full text-center">
                  {formatHumanDate(p.weekStart)}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Top servicios
// ───────────────────────────────────────────────────────────────────────

function TopServicesCard({ rows }: { rows: TopServiceRow[] }) {
  const maxRev = useMemo(
    () => Math.max(1, ...rows.map(r => r.revenue)),
    [rows],
  )

  return (
    <div className="bg-white rounded-2xl border border-warm-150 shadow-softer overflow-hidden">
      <div className="px-5 py-4 border-b border-warm-150 flex items-center gap-2">
        <Sparkles size={15} className="text-brand-700" strokeWidth={1.8} />
        <h3 className="font-serif text-[17px] text-warm-800">Top servicios</h3>
      </div>
      {rows.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin servicios cobrados en el período.
        </div>
      ) : (
        <ul className="divide-y divide-warm-100">
          {rows.map((r, i) => (
            <li key={r.serviceId} className="px-5 py-3">
              <div className="flex items-baseline justify-between gap-3">
                <div className="flex items-baseline gap-2 min-w-0">
                  <span className="text-[10.5px] tabular-nums text-warm-400 font-medium w-4 flex-shrink-0">
                    {i + 1}
                  </span>
                  <span className="text-[13px] text-warm-800 truncate">{r.serviceName}</span>
                </div>
                <span className="text-[13px] font-medium text-warm-800 tabular-nums whitespace-nowrap">
                  {fmtCop(r.revenue)}
                </span>
              </div>
              {/* Barra de progreso relativa */}
              <div className="mt-1.5 h-1 bg-warm-100 rounded-full overflow-hidden ml-6">
                <div
                  className="h-full bg-brand-500 rounded-full"
                  style={{ width: `${(r.revenue / maxRev) * 100}%` }}
                />
              </div>
              <div className="text-[11px] text-warm-500 mt-1 ml-6 tabular-nums">
                {r.appointmentsCount} {r.appointmentsCount === 1 ? 'cita' : 'citas'}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Top estilistas
// ───────────────────────────────────────────────────────────────────────

function TopStylistsCard({ rows }: { rows: TopStylistRow[] }) {
  return (
    <div className="bg-white rounded-2xl border border-warm-150 shadow-softer overflow-hidden">
      <div className="px-5 py-4 border-b border-warm-150 flex items-center gap-2">
        <Scissors size={15} className="text-brand-700" strokeWidth={1.8} />
        <h3 className="font-serif text-[17px] text-warm-800">Top estilistas</h3>
      </div>
      {rows.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin actividad de estilistas en el período.
        </div>
      ) : (
        <ul className="divide-y divide-warm-100">
          {rows.map((r, i) => (
            <li key={r.stylistId} className="px-5 py-3 flex items-center gap-3">
              <span className="text-[10.5px] tabular-nums text-warm-400 font-medium w-4 flex-shrink-0">
                {i + 1}
              </span>
              <span
                className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-semibold flex-shrink-0"
                style={{
                  backgroundColor: r.stylistColor ? `${r.stylistColor}33` : '#ece7df',
                  color: r.stylistColor ?? '#7c6d54',
                }}
              >
                {initialsOf(r.stylistName)}
              </span>
              <div className="flex-1 min-w-0">
                <div className="text-[13px] text-warm-800 truncate">{r.stylistName}</div>
                <div className="text-[11px] text-warm-500 tabular-nums">
                  {r.appointmentsCount} {r.appointmentsCount === 1 ? 'cita' : 'citas'}
                </div>
              </div>
              <span className="text-[13px] font-medium text-warm-800 tabular-nums whitespace-nowrap">
                {fmtCop(r.revenue)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Nuevos vs recurrentes
// ───────────────────────────────────────────────────────────────────────

function NewVsReturningCard({ newCount, returningCount }: { newCount: number; returningCount: number }) {
  const total = newCount + returningCount
  if (total === 0) return null

  const newPct = (newCount / total) * 100
  const returningPct = (returningCount / total) * 100

  return (
    <div className="bg-white rounded-2xl border border-warm-150 shadow-softer p-5 mt-5">
      <div className="flex items-center gap-2 mb-3">
        <Users size={15} className="text-brand-700" strokeWidth={1.8} />
        <h3 className="font-serif text-[17px] text-warm-800">Nuevos vs recurrentes</h3>
      </div>
      <div className="text-[11.5px] text-warm-500 mb-2 tabular-nums">
        {total} citas — {newCount} de clientes nuevos, {returningCount} de recurrentes
      </div>
      <div className="flex h-3 rounded-full overflow-hidden bg-warm-100">
        <div
          className="bg-gold-400"
          style={{ width: `${newPct}%` }}
          title={`Nuevos: ${newCount} (${newPct.toFixed(0)}%)`}
        />
        <div
          className="bg-brand-600"
          style={{ width: `${returningPct}%` }}
          title={`Recurrentes: ${returningCount} (${returningPct.toFixed(0)}%)`}
        />
      </div>
      <div className="flex justify-between text-[11px] text-warm-500 mt-2 tabular-nums">
        <span className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-gold-400" />
          Nuevos {newPct.toFixed(0)}%
        </span>
        <span className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-brand-600" />
          Recurrentes {returningPct.toFixed(0)}%
        </span>
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────

function initialsOf(name: string): string {
  return name.split(' ').slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('')
}
