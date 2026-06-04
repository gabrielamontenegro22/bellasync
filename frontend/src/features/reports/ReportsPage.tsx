import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Download, TrendingUp, TrendingDown } from 'lucide-react'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { DatePicker } from '@/components/ui'
import {
  getReportsSummary,
  type ReportsSummary,
  type TopServiceRow,
  type TopStylistRow,
  type DailyRevenuePoint,
  type PaymentMethodRow,
  type FunnelStats,
} from '@/api/reports'

/**
 * `/reportes` — Dashboard del salón. Replica el mockup Reportes.html.
 *
 * Estructura (de arriba a abajo):
 *   1. Header sticky con eyebrow + "Exportar"
 *   2. Page header: eyebrow gold + serif "Reportes" + descripción
 *   3. Range pills (Hoy / Esta semana / Este mes / Este año / Personalizado)
 *   4. 5 KPI cards (Ingresos / Citas / Ticket / No-show / Nuevas)
 *   5. 2 col: Ingresos en el tiempo (area SVG) + Ingresos por método (dona)
 *   6. 2 col: Servicios más vendidos + Desempeño por estilista (tabla)
 *   7. 2 col: Embudo de citas + Card oscura "Lectura del período"
 *
 * Todos los datos vienen del backend en UN solo GET /api/Reports/summary.
 * El frontend no hace cálculos derivados que el backend ya hace.
 */

// ───────────────────────────────────────────────────────────────────────
// Helpers de formato
// ───────────────────────────────────────────────────────────────────────

function fmtCop(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-CO')
}

function fmtK(n: number): string {
  if (n >= 1_000_000) return '$' + (n / 1_000_000).toFixed(1) + 'M'
  if (n >= 1000) return '$' + Math.round(n / 1000) + 'k'
  return '$' + Math.round(n).toLocaleString('es-CO')
}

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

function mondayOfWeekIso(): string {
  const d = new Date()
  const day = d.getDay()  // 0=Sun..6=Sat
  const diff = (day + 6) % 7  // 0=Mon..6=Sun
  d.setDate(d.getDate() - diff)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function firstOfYearIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-01-01`
}

function formatMonthYear(iso: string): string {
  const [y, m] = iso.split('-').map(Number)
  return new Intl.DateTimeFormat('es-CO', { month: 'long', year: 'numeric' })
    .format(new Date(y, m - 1, 1))
    .replace(/^./, c => c.toUpperCase())
}

// ───────────────────────────────────────────────────────────────────────
// Range presets
// ───────────────────────────────────────────────────────────────────────

type RangeId = 'hoy' | 'semana' | 'mes' | 'anio' | 'custom'

const RANGES: { id: RangeId; label: string; compute: () => [string, string] }[] = [
  { id: 'hoy',    label: 'Hoy',           compute: () => [todayCO(), todayCO()] },
  { id: 'semana', label: 'Esta semana',   compute: () => [mondayOfWeekIso(), todayCO()] },
  { id: 'mes',    label: 'Este mes',      compute: () => [firstOfMonthIso(), todayCO()] },
  { id: 'anio',   label: 'Este año',      compute: () => [firstOfYearIso(), todayCO()] },
  { id: 'custom', label: 'Personalizado', compute: () => [addDaysIso(todayCO(), -29), todayCO()] },
]

// ───────────────────────────────────────────────────────────────────────

export function ReportsPage() {
  const { user } = useAuth()
  const [rangeId, setRangeId] = useState<RangeId>('mes')
  const [from, setFrom] = useState<string>(() => firstOfMonthIso())
  const [to, setTo] = useState<string>(() => todayCO())

  const applyRange = (id: RangeId) => {
    setRangeId(id)
    if (id !== 'custom') {
      const [f, t] = RANGES.find(r => r.id === id)!.compute()
      setFrom(f)
      setTo(t)
    }
  }
  const onCustomFrom = (v: string) => { setFrom(v); setRangeId('custom') }
  const onCustomTo = (v: string) => { setTo(v); setRangeId('custom') }

  const { data, isLoading, error } = useQuery({
    queryKey: ['reports', 'summary', from, to],
    queryFn: () => getReportsSummary(from, to),
    enabled: !!from && !!to,
  })

  return (
    <div className="flex-1 min-w-0 bg-cream min-h-full">
      {/* Header sticky con breadcrumb + Exportar */}
      <header className="h-16 border-b border-warm-150 bg-cream/80 backdrop-blur sticky top-0 z-30 flex items-center px-5 lg:px-8 gap-3">
        <div className="text-[12.5px] text-warm-500">
          <span className="text-warm-700">{user?.tenantName ?? 'Salón'}</span> · Reportes
        </div>
        <button
          type="button"
          disabled
          title="Próximamente"
          className="ml-auto px-3 py-2 rounded-lg border border-warm-200 text-warm-400 hover:bg-warm-100 text-[12.5px] font-medium flex items-center gap-1.5 cursor-not-allowed"
        >
          <Download size={14} strokeWidth={1.8} /> Exportar
        </button>
      </header>

      {/* Page header */}
      <div className="px-5 lg:px-8 pt-7 lg:pt-8 pb-5">
        <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Tu salón
        </div>
        <h1 className="font-serif text-[34px] sm:text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
          Reportes
        </h1>
        <p className="text-[13.5px] text-warm-500 mt-1.5">
          Un vistazo a cómo va tu salón. {data ? formatMonthYear(data.from) : '…'}.
        </p>

        {/* Range pills */}
        <div className="mt-5 inline-flex items-center gap-1 p-1 bg-warm-100 rounded-lg flex-wrap">
          {RANGES.map(r => (
            <button
              key={r.id}
              type="button"
              onClick={() => applyRange(r.id)}
              className={cls(
                'px-3 py-1.5 rounded-md text-[12px] font-medium transition',
                rangeId === r.id
                  ? 'bg-white text-warm-800 shadow-sm'
                  : 'text-warm-500 hover:text-warm-700',
              )}
            >
              {r.label}
            </button>
          ))}
        </div>

        {/* Date pickers — solo cuando custom */}
        {rangeId === 'custom' && (
          <div className="mt-3 flex items-center gap-1.5 anim-fade">
            <DatePicker value={from} onChange={onCustomFrom} max={to} size="sm" />
            <span className="text-warm-400 text-[12px]">a</span>
            <DatePicker value={to} onChange={onCustomTo} min={from} size="sm" />
          </div>
        )}

        {/* Estados de carga/error */}
        {isLoading && (
          <div className="mt-10 text-center text-[13px] text-warm-500">Cargando reportes…</div>
        )}
        {error && (
          <div className="mt-10 text-center text-[13px] text-terra-500">
            No se pudo cargar el reporte.
          </div>
        )}
      </div>

      {data && <Dashboard data={data} />}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Dashboard
// ───────────────────────────────────────────────────────────────────────

function Dashboard({ data }: { data: ReportsSummary }) {
  return (
    <>
      {/* 5 KPIs */}
      <div className="px-5 lg:px-8 pb-4 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <KpiCard
          label="Ingresos"
          value={fmtK(data.totalRevenue)}
          deltaPct={data.revenueChangePct}
          accent="brand"
        />
        <KpiCard
          label="Citas atendidas"
          value={data.appointmentsCount.toLocaleString('es-CO')}
          deltaPct={data.appointmentsChangePct}
          accent="brand"
        />
        <KpiCard
          label="Ticket promedio"
          value={fmtCop(data.averageTicket)}
          deltaPct={data.averageTicketChangePct}
          accent="gold"
        />
        <KpiCard
          label="Tasa de no-show"
          value={data.noShowRate.toFixed(1) + '%'}
          deltaPts={data.noShowChangePts}
          invertDelta
          accent="terra"
        />
        <KpiCard
          label="Clientas nuevas"
          value={data.newCustomersCount.toLocaleString('es-CO')}
          deltaPct={data.newCustomersChangePct}
          accent="plum"
        />
      </div>

      {/* Charts row */}
      <div className="px-5 lg:px-8 pb-4 grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2">
          <RevenueChart points={data.dailyRevenue} changePct={data.revenueChangePct} />
        </div>
        <MethodsDonut rows={data.paymentMethodBreakdown} totalRevenue={data.totalRevenue} />
      </div>

      {/* Rankings */}
      <div className="px-5 lg:px-8 pb-4 grid lg:grid-cols-2 gap-4">
        <TopServicesCard rows={data.topServices} />
        <StylistPerfCard rows={data.topStylists} />
      </div>

      {/* Funnel + Insight */}
      <div className="px-5 lg:px-8 pb-12 grid lg:grid-cols-2 gap-4">
        <FunnelCard funnel={data.funnel} />
        <InsightCard eyebrow={data.insightEyebrow} text={data.insightText} />
      </div>
    </>
  )
}

// ───────────────────────────────────────────────────────────────────────
// KPI card
// ───────────────────────────────────────────────────────────────────────

type Accent = 'brand' | 'gold' | 'terra' | 'plum'

const ACCENT_ICON_CLS: Record<Accent, string> = {
  brand: 'text-brand-700 bg-brand-50',
  gold:  'text-gold-600 bg-gold-50',
  terra: 'text-terra-500 bg-terra-100/60',
  plum:  'text-[#5b3d6b] bg-[#f0e8f3]',
}

function KpiCard({
  label, value, deltaPct, deltaPts, invertDelta, accent,
}: {
  label: string
  value: string
  /** Cambio porcentual vs período anterior. null = sin comparativa. */
  deltaPct?: number | null
  /** Cambio en puntos (para no-show). Solo uno de deltaPct/deltaPts. */
  deltaPts?: number | null
  /** Si true, "bajó" es bueno (no-show). */
  invertDelta?: boolean
  accent: Accent
}) {
  const delta = deltaPct ?? deltaPts ?? null
  const good = delta == null
    ? false
    : invertDelta ? delta < 0 : delta > 0

  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-4 sm:p-5 shadow-softer">
      <div className="flex items-center justify-between">
        <div className="text-[10.5px] tracking-[0.16em] uppercase text-warm-500 font-medium">
          {label}
        </div>
        <span className={cls('w-8 h-8 rounded-lg flex items-center justify-center', ACCENT_ICON_CLS[accent])}>
          <KpiIcon accent={accent} />
        </span>
      </div>
      <div className="font-serif text-[26px] sm:text-[32px] leading-none tabular-nums text-warm-800 mt-3">
        {value}
      </div>
      {delta != null ? (
        <div className="flex items-center gap-1.5 mt-2.5 flex-wrap">
          <span className={cls(
            'inline-flex items-center gap-0.5 text-[11.5px] font-semibold px-1.5 py-0.5 rounded-md tabular-nums',
            good ? 'text-brand-700 bg-brand-50' : 'text-terra-500 bg-terra-100/60',
          )}>
            {delta >= 0 ? <TrendingUp size={11} strokeWidth={2.2} /> : <TrendingDown size={11} strokeWidth={2.2} />}
            {deltaPts != null
              ? `${delta > 0 ? '+' : ''}${delta.toFixed(1)} pts`
              : `${Math.abs(delta).toFixed(1)}%`}
          </span>
          <span className="text-[11px] text-warm-400">vs período anterior</span>
        </div>
      ) : (
        <div className="text-[11px] text-warm-400 mt-2.5">sin comparativa</div>
      )}
    </div>
  )
}

/**
 * Íconos inline — los del mockup están como SVG hardcodeado por accent,
 * acá los renderizamos con un único componente que despacha por accent.
 */
function KpiIcon({ accent }: { accent: Accent }) {
  switch (accent) {
    case 'brand':
      // Tipo "dinero" o "calendario" — usamos dollar para Ingresos, pero
      // como el accent brand también lo usa Citas, el componente padre
      // podría querer distinguir. Para simplicidad, usamos $/cal/etc
      // según accent (brand = $, gold = ticket, terra = ghost, plum = spark).
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
        </svg>
      )
    case 'gold':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z" />
          <path d="M13 5v14" />
        </svg>
      )
    case 'terra':
      // Ghost (no-show) — fantasmita
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="9" />
          <path d="M8 14s1.5 2 4 2 4-2 4-2M9 9h.01M15 9h.01" />
        </svg>
      )
    case 'plum':
      // Sparkle
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1" />
        </svg>
      )
  }
}

// ───────────────────────────────────────────────────────────────────────
// Area chart de ingresos en el tiempo
// ───────────────────────────────────────────────────────────────────────

function RevenueChart({
  points, changePct,
}: { points: DailyRevenuePoint[]; changePct: number | null }) {
  const W = 600
  const H = 200
  const pad = 8
  const [hover, setHover] = useState<number | null>(null)

  const total = useMemo(() => points.reduce((a, p) => a + p.revenue, 0), [points])
  const max = useMemo(() => Math.max(1, ...points.map(p => p.revenue)) * 1.1, [points])

  const pts = useMemo(() => {
    if (points.length === 0) return [] as [number, number][]
    if (points.length === 1) return [[W / 2, H - pad - (points[0].revenue / max) * (H - pad * 2)] as [number, number]]
    const stepX = (W - pad * 2) / (points.length - 1)
    return points.map((p, i) =>
      [pad + i * stepX, H - pad - (p.revenue / max) * (H - pad * 2)] as [number, number],
    )
  }, [points, max])

  const linePath = useMemo(() => {
    if (pts.length === 0) return ''
    return pts.map((p, i) => (i === 0 ? 'M' : 'L') + p[0].toFixed(1) + ' ' + p[1].toFixed(1)).join(' ')
  }, [pts])

  const areaPath = useMemo(() => {
    if (pts.length === 0) return ''
    return linePath + ` L ${pts[pts.length - 1][0]} ${H - pad} L ${pts[0][0]} ${H - pad} Z`
  }, [pts, linePath])

  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-softer">
      <div className="flex items-start justify-between mb-1 gap-3 flex-wrap">
        <div>
          <h3 className="font-serif text-[20px] text-warm-800 leading-tight">Ingresos en el tiempo</h3>
          <div className="text-[11.5px] text-warm-500 mt-0.5">
            {points.length} {points.length === 1 ? 'día' : 'días'} · total {fmtCop(total)}
          </div>
        </div>
        {changePct != null && (
          <span className={cls(
            'inline-flex items-center gap-0.5 text-[11.5px] font-semibold px-1.5 py-0.5 rounded-md tabular-nums',
            changePct >= 0 ? 'text-brand-700 bg-brand-50' : 'text-terra-500 bg-terra-100/60',
          )}>
            {changePct >= 0 ? <TrendingUp size={11} /> : <TrendingDown size={11} />}
            {Math.abs(changePct).toFixed(1)}%
          </span>
        )}
      </div>

      {points.length === 0 ? (
        <div className="py-12 text-center text-[13px] text-warm-500">
          Sin ingresos en el período.
        </div>
      ) : (
        <div className="relative mt-3">
          <svg
            viewBox={`0 0 ${W} ${H}`}
            className="w-full"
            style={{ height: 200 }}
            preserveAspectRatio="none"
            onMouseLeave={() => setHover(null)}
            onMouseMove={(e) => {
              const rect = e.currentTarget.getBoundingClientRect()
              const x = ((e.clientX - rect.left) / rect.width) * W
              const stepX = pts.length > 1 ? (W - pad * 2) / (pts.length - 1) : 0
              const idx = stepX > 0 ? Math.round((x - pad) / stepX) : 0
              if (idx >= 0 && idx < pts.length) setHover(idx)
            }}
          >
            <defs>
              <linearGradient id="rev-gradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#3f8a7a" stopOpacity="0.25" />
                <stop offset="100%" stopColor="#3f8a7a" stopOpacity="0" />
              </linearGradient>
            </defs>
            <path d={areaPath} fill="url(#rev-gradient)" />
            <path
              d={linePath}
              fill="none"
              stroke="#0f766e"
              strokeWidth="2"
              strokeLinejoin="round"
              vectorEffect="non-scaling-stroke"
            />
            {hover != null && pts[hover] && (
              <g>
                <line
                  x1={pts[hover][0]} y1={pad}
                  x2={pts[hover][0]} y2={H - pad}
                  stroke="#cbc3b4" strokeWidth="1" strokeDasharray="3 3"
                  vectorEffect="non-scaling-stroke"
                />
                <circle
                  cx={pts[hover][0]} cy={pts[hover][1]}
                  r="4" fill="#0f766e" stroke="#fff" strokeWidth="2"
                  vectorEffect="non-scaling-stroke"
                />
              </g>
            )}
          </svg>
          {hover != null && pts[hover] && points[hover] && (
            <div
              className="absolute -top-1 bg-warm-800 text-white text-[11px] px-2 py-1 rounded-md pointer-events-none tabular-nums whitespace-nowrap shadow-soft"
              style={{
                left: `${(pts[hover][0] / W) * 100}%`,
                transform: 'translate(-50%, -100%)',
              }}
            >
              {formatHumanDate(points[hover].date)} · {fmtCop(points[hover].revenue)}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function formatHumanDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short' })
    .format(new Date(y, m - 1, d))
    .replace(/\.$/, '')
}

// ───────────────────────────────────────────────────────────────────────
// Dona de métodos de pago
// ───────────────────────────────────────────────────────────────────────

// Color por método. Cash brand, Transfer gold, Card plum, Other terra.
const METHOD_COLORS: Record<string, string> = {
  Cash:     '#3f8a7a',
  Transfer: '#d4a72b',
  Card:     '#5d7a8a',
  Other:    '#c026a8',
}

function MethodsDonut({
  rows, totalRevenue,
}: { rows: PaymentMethodRow[]; totalRevenue: number }) {
  const R = 52
  const C = 2 * Math.PI * R
  let offsetAcc = 0
  const segs = rows
    .filter(r => r.revenue > 0)
    .map(r => {
      const frac = totalRevenue > 0 ? r.revenue / totalRevenue : 0
      const seg = {
        ...r,
        color: METHOD_COLORS[r.method] ?? '#80796a',
        dash: frac * C,
        offset: offsetAcc * C,
      }
      offsetAcc += frac
      return seg
    })

  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-softer">
      <h3 className="font-serif text-[20px] text-warm-800 leading-tight mb-4">
        Ingresos por método
      </h3>
      {segs.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin pagos en el período.
        </div>
      ) : (
        <div className="flex items-center gap-5">
          <svg viewBox="0 0 140 140" className="w-32 h-32 flex-shrink-0 -rotate-90">
            {segs.map((s, i) => (
              <circle
                key={i}
                cx="70" cy="70" r={R}
                fill="none"
                stroke={s.color}
                strokeWidth="16"
                strokeDasharray={`${s.dash} ${C - s.dash}`}
                strokeDashoffset={-s.offset}
              />
            ))}
            <text
              x="70" y="74"
              textAnchor="middle"
              transform="rotate(90 70 70)"
              style={{ fontFamily: 'serif', fontSize: '17px', fill: '#2e2b25', fontWeight: 600 }}
            >
              {fmtK(totalRevenue)}
            </text>
          </svg>
          <div className="flex-1 space-y-2 min-w-0">
            {segs.map((s, i) => (
              <div key={i} className="flex items-center gap-2 text-[12.5px]">
                <span className="w-2.5 h-2.5 rounded-sm flex-shrink-0" style={{ background: s.color }} />
                <span className="flex-1 text-warm-700 truncate">{s.label}</span>
                <span className="text-warm-500 tabular-nums">{s.percentage.toFixed(0)}%</span>
                <span className="text-warm-800 font-medium tabular-nums w-14 text-right">
                  {fmtK(s.revenue)}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Top servicios
// ───────────────────────────────────────────────────────────────────────

function TopServicesCard({ rows }: { rows: TopServiceRow[] }) {
  const max = Math.max(1, ...rows.map(r => r.revenue))
  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-softer">
      <h3 className="font-serif text-[20px] text-warm-800 leading-tight mb-4">
        Servicios más vendidos
      </h3>
      {rows.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin servicios cobrados en el período.
        </div>
      ) : (
        <div className="space-y-3.5">
          {rows.map((s, i) => (
            <div key={s.serviceId}>
              <div className="flex items-center justify-between mb-1.5 gap-3">
                <div className="flex items-center gap-2.5 min-w-0">
                  <span className="font-serif text-[14px] text-warm-400 w-4 tabular-nums flex-shrink-0">
                    {i + 1}
                  </span>
                  <span className="text-[13px] text-warm-800 truncate">{s.serviceName}</span>
                </div>
                <div className="flex items-center gap-3 flex-shrink-0">
                  <span className="text-[11.5px] text-warm-500 tabular-nums">{s.appointmentsCount}×</span>
                  <span className="text-[13px] font-medium text-warm-800 tabular-nums w-16 text-right">
                    {fmtK(s.revenue)}
                  </span>
                </div>
              </div>
              <div className="h-1.5 rounded-full bg-warm-100 overflow-hidden" style={{ marginLeft: '26px' }}>
                <div
                  className="h-full rounded-full bg-gold-400"
                  style={{ width: (s.revenue / max * 100) + '%' }}
                />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Desempeño por estilista (tabla con ocupación)
// ───────────────────────────────────────────────────────────────────────

function StylistPerfCard({ rows }: { rows: TopStylistRow[] }) {
  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-softer">
      <h3 className="font-serif text-[20px] text-warm-800 leading-tight mb-4">
        Desempeño por estilista
      </h3>
      {rows.length === 0 ? (
        <div className="py-10 text-center text-[13px] text-warm-500">
          Sin actividad de estilistas en el período.
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-[13px]">
            <thead>
              <tr className="text-[10px] tracking-[0.12em] uppercase text-warm-500 border-b border-warm-150">
                <th className="text-left font-medium pb-2">Estilista</th>
                <th className="text-right font-medium pb-2">Ingresos</th>
                <th className="text-right font-medium pb-2 hidden sm:table-cell">Citas</th>
                <th className="text-right font-medium pb-2">Ocupación</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(s => {
                const occBarCls = s.occupancyPct >= 85
                  ? 'bg-brand-500'
                  : s.occupancyPct >= 70
                  ? 'bg-gold-400'
                  : 'bg-terra-400'
                return (
                  <tr key={s.stylistId} className="border-b border-warm-100 last:border-0">
                    <td className="py-2.5">
                      <div className="flex items-center gap-2.5 min-w-0">
                        <span
                          className="w-7 h-7 rounded-full flex items-center justify-center text-[10.5px] font-medium font-serif flex-shrink-0"
                          style={{
                            backgroundColor: s.stylistColor ? `${s.stylistColor}33` : '#ece7df',
                            color: s.stylistColor ?? '#7c6d54',
                          }}
                        >
                          {initialsOf(s.stylistName)}
                        </span>
                        <span className="text-warm-800 truncate">{s.stylistName}</span>
                      </div>
                    </td>
                    <td className="py-2.5 text-right tabular-nums font-medium text-warm-800">
                      {fmtK(s.revenue)}
                    </td>
                    <td className="py-2.5 text-right tabular-nums text-warm-600 hidden sm:table-cell">
                      {s.appointmentsCount}
                    </td>
                    <td className="py-2.5 text-right">
                      <div className="flex items-center gap-2 justify-end">
                        <div className="w-14 h-1.5 rounded-full bg-warm-100 overflow-hidden">
                          <div
                            className={cls('h-full rounded-full', occBarCls)}
                            style={{ width: Math.min(100, s.occupancyPct) + '%' }}
                          />
                        </div>
                        <span className="text-[12px] tabular-nums text-warm-600 w-8">
                          {Math.round(s.occupancyPct)}%
                        </span>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function initialsOf(name: string): string {
  return name.split(' ').slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('')
}

// ───────────────────────────────────────────────────────────────────────
// Embudo de citas
// ───────────────────────────────────────────────────────────────────────

function FunnelCard({ funnel }: { funnel: FunnelStats }) {
  const max = Math.max(1, funnel.requested)
  const steps = [
    { key: 'Solicitadas', value: funnel.requested, color: 'bg-warm-300' },
    { key: 'Confirmadas', value: funnel.confirmed, color: 'bg-brand-300' },
    { key: 'Atendidas',   value: funnel.attended,  color: 'bg-brand-500' },
    { key: 'No-show',     value: funnel.noShow,    color: 'bg-terra-400' },
  ]

  // Insight de cierre: % de solicitadas que se atendieron.
  const attendedRate = funnel.requested > 0
    ? Math.round((funnel.attended / funnel.requested) * 100)
    : 0

  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-softer">
      <h3 className="font-serif text-[20px] text-warm-800 leading-tight">Embudo de citas</h3>
      <div className="text-[11.5px] text-warm-500 mt-0.5 mb-4">
        De solicitud a atención
      </div>
      {funnel.requested === 0 ? (
        <div className="py-8 text-center text-[13px] text-warm-500">
          Sin citas creadas en el período.
        </div>
      ) : (
        <>
          <div className="space-y-2.5">
            {steps.map((f, i) => {
              const pct = (f.value / max) * 100
              // Conversion vs paso anterior.
              const prevValue = i > 0 ? steps[i - 1].value : f.value
              const convPct = prevValue > 0 ? Math.round((f.value / prevValue) * 100) : 100
              const noShowVsAttended = funnel.attended > 0
                ? Math.round((funnel.noShow / funnel.attended) * 100)
                : 0
              return (
                <div key={f.key}>
                  <div className="flex items-center justify-between mb-1 text-[12.5px]">
                    <span className="text-warm-700">{f.key}</span>
                    <span className="flex items-center gap-2">
                      <span className="font-medium text-warm-800 tabular-nums">{f.value}</span>
                      {i > 0 && (
                        <span className={cls(
                          'text-[10.5px] tabular-nums',
                          f.key === 'No-show' ? 'text-terra-500' : 'text-warm-400',
                        )}>
                          {f.key === 'No-show'
                            ? `${noShowVsAttended}% de atendidas`
                            : `${convPct}%`}
                        </span>
                      )}
                    </span>
                  </div>
                  <div className="h-7 rounded-lg bg-warm-50 overflow-hidden">
                    <div
                      className={cls('h-full rounded-lg transition-all', f.color)}
                      style={{ width: pct + '%' }}
                    />
                  </div>
                </div>
              )
            })}
          </div>
          <div className="mt-4 pt-3 border-t border-warm-150 text-[11.5px] text-warm-500 flex items-start gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-brand-500 mt-1.5 flex-shrink-0" />
            <span>
              {attendedRate}% de las citas solicitadas se atienden.
            </span>
          </div>
        </>
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Card oscura "Lectura del período"
// ───────────────────────────────────────────────────────────────────────

function InsightCard({ eyebrow, text }: { eyebrow: string; text: string | null }) {
  // Si no hay texto generado (por poca actividad), mostramos un mensaje
  // suave en vez de esconder la card.
  const fallback = 'Cuando tengas más actividad, acá te resumimos lo más relevante del período.'
  return (
    <div className="bg-warm-800 text-white rounded-2xl p-6 flex flex-col justify-center relative overflow-hidden min-h-[180px]">
      <div className="absolute -right-12 -top-12 w-40 h-40 rounded-full bg-brand-700/30 blur-2xl" />
      <div className="relative">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium">
          {eyebrow}
        </div>
        <div className="font-serif text-[24px] sm:text-[26px] leading-tight mt-2">
          {text ? 'Tu salón en una mirada 💛' : 'Todavía es pronto para sacar lecturas'}
        </div>
        <p className="text-[13px] text-warm-300 leading-relaxed mt-3">
          {text ?? fallback}
        </p>
      </div>
    </div>
  )
}
