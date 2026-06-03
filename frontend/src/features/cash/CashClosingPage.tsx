import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowDownRight, ArrowUpRight,
  Banknote, ChevronRight, Clock, CreditCard,
  Download, Lock, Plus, Smartphone, CheckCircle2, X,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { fmtCop } from '@/features/customers/lib/customerLook'
import { getDailyCashSummary, type DailyCashSummary } from '@/api/cash'
import type { PaymentResponse } from '@/api/payments'

/**
 * `/caja` — réplica fiel del mockup Caja.html/caja.jsx.
 *
 * Lo que sí está conectado al backend hoy (vía /api/Cash/daily-summary):
 *   - Total recaudado, # transacciones, propinas.
 *   - Breakdown por método de pago.
 *   - Lista de transacciones del día (cliente, servicio, estilista, hora).
 *   - Ventas por estilista (agrupando los pagos del día en frontend).
 *   - Cálculo de efectivo esperado en caja para el arqueo.
 *
 * Lo que aún está como visual / próximamente:
 *   - Egresos del día: no hay entidad Expense aún. Mostramos vacío con
 *     CTA "Registrar egreso" deshabilitado.
 *   - Historial de cierres: no hay entidad CashClosing aún.
 *   - Caja abierta/cerrada como estado persistido. El cierre solo calcula
 *     y muestra la diferencia, no se guarda.
 *   - Base inicial: constante por ahora.
 */
export function CashClosingPage() {
  const { user } = useAuth()
  const [tab, setTab] = useState<'hoy' | 'historial'>('hoy')
  const [filterMethod, setFilterMethod] = useState<string>('all')
  const [closeOpen, setCloseOpen] = useState(false)
  const [closed, setClosed] = useState<{
    counted: number
    diff: number
    note: string
  } | null>(null)

  // Por ahora fijamos hoy. Cuando el endpoint acepte navegación,
  // exponemos un date picker (ver TODO en /caja date nav).
  const today = formatLocalDate(new Date())

  const { data, isLoading, error } = useQuery({
    queryKey: ['cash', today],
    queryFn: () => getDailyCashSummary(today),
  })

  return (
    <div className="flex-1 min-w-0 bg-cream">
      {/* Topbar */}
      <header
        className={cls(
          'h-16 border-b border-warm-150 bg-cream/80 backdrop-blur',
          'sticky top-0 z-30 flex items-center px-6 lg:px-8 gap-3',
        )}
      >
        <div className="text-[12.5px] text-warm-500">
          <span className="text-warm-700">{user?.tenantName ?? 'Salón'}</span> · Caja
        </div>
        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            disabled
            title="Próximamente"
            className={cls(
              'px-3 py-2 rounded-lg border border-warm-200 text-warm-400',
              'text-[12.5px] font-medium flex items-center gap-1.5 cursor-not-allowed',
            )}
          >
            <Download size={14} strokeWidth={1.8} /> Exportar
          </button>
          {!closed && (
            <button
              type="button"
              onClick={() => setCloseOpen(true)}
              disabled={isLoading || !data}
              className={cls(
                'px-3.5 py-2 rounded-lg bg-brand-700 hover:bg-brand-800',
                'text-white text-[12.5px] font-medium flex items-center gap-1.5 shadow-soft',
                'disabled:opacity-50 disabled:cursor-not-allowed',
              )}
            >
              <Lock size={15} strokeWidth={1.8} /> Cerrar caja
            </button>
          )}
        </div>
      </header>

      {/* Page header */}
      <div className="px-6 lg:px-8 pt-8 pb-5">
        <div className="flex items-end justify-between flex-wrap gap-4">
          <div>
            <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
              Movimiento del día
            </div>
            <h1 className="font-serif text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
              Caja de hoy
            </h1>
            <p className="text-[13.5px] text-warm-500 mt-1.5 flex items-center gap-2">
              <Clock size={13} strokeWidth={1.8} />
              {formatHumanDate(new Date())} · Abierta desde las 8:00 am
            </p>
          </div>
          {closed ? (
            <div
              className="rounded-xl bg-brand-50 ring-1 ring-brand-200 px-4 py-2.5 flex items-center gap-2.5 max-w-[420px]"
              title={closed.note ? `Nota: ${closed.note}` : undefined}
            >
              <span className="w-7 h-7 rounded-full bg-brand-700 text-white flex items-center justify-center flex-shrink-0">
                <CheckCircle2 size={15} strokeWidth={2.4} />
              </span>
              <div className="min-w-0">
                <div className="text-[12.5px] font-medium text-brand-800">Caja cerrada</div>
                <div className="text-[11px] text-warm-600">
                  Diferencia:{' '}
                  {closed.diff === 0
                    ? 'cuadró perfecto'
                    : (closed.diff > 0 ? '+' : '') + fmtCop(closed.diff)}
                </div>
                {closed.note && (
                  <div className="text-[11px] text-warm-500 italic truncate mt-0.5">
                    “{closed.note}”
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="rounded-xl bg-gold-50 ring-1 ring-gold-200 px-4 py-2.5 flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-gold-400 animate-pulse" />
              <span className="text-[12.5px] text-gold-600 font-medium">Caja abierta</span>
            </div>
          )}
        </div>

        {/* KPIs */}
        <div className="mt-7 grid sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <div className="bg-brand-700 text-white rounded-2xl p-5 shadow-soft">
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-brand-200 font-medium">
              Total recaudado
            </div>
            <div className="font-serif text-[38px] leading-none tabular-nums mt-2">
              {fmtCop(data?.totalAmount ?? 0)}
            </div>
            <div className="text-[11.5px] text-brand-100 mt-1.5">
              {data?.paymentCount ?? 0} transacciones
            </div>
          </div>
          <Kpi
            label="Egresos del día"
            value={fmtCop(0)}
            sub="próximamente"
            accent="terra"
            icon={<ArrowDownRight size={14} strokeWidth={1.8} />}
          />
          <Kpi
            label="Neto en caja"
            value={fmtCop(data?.totalAmount ?? 0)}
            sub="ventas − egresos"
            accent="brand"
            icon={<ArrowUpRight size={14} strokeWidth={1.8} />}
          />
          <Kpi
            label="Efectivo esperado"
            value={fmtCop(expectedCashFor(data))}
            sub="incluye base inicial"
            accent="gold"
            icon={<Banknote size={14} strokeWidth={1.8} />}
          />
        </div>
      </div>

      {/* Tabs */}
      <div className="px-6 lg:px-8 border-b border-warm-150">
        <div className="flex items-center gap-1">
          {(
            [
              ['hoy', 'Resumen de hoy'],
              ['historial', 'Historial de cierres'],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              onClick={() => setTab(id)}
              className={cls(
                'px-4 py-2.5 text-[13px] font-medium border-b-2 -mb-px transition',
                tab === id
                  ? 'border-brand-700 text-brand-800'
                  : 'border-transparent text-warm-500 hover:text-warm-700',
              )}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Estado: error / loading se muestran dentro de los paneles para
          que el header siga visible. */}

      {tab === 'hoy' ? (
        <TabHoy
          data={data}
          isLoading={isLoading}
          error={error as Error | null}
          filterMethod={filterMethod}
          onFilterChange={setFilterMethod}
          onOpenClose={() => setCloseOpen(true)}
          closed={!!closed}
        />
      ) : (
        <TabHistorial />
      )}

      <CloseModal
        open={closeOpen}
        onClose={() => setCloseOpen(false)}
        expected={expectedCashFor(data)}
        cashSales={cashSalesFor(data)}
        onConfirm={(counted, diff, note) => {
          setClosed({ counted, diff, note })
          setCloseOpen(false)
        }}
      />
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Tab Hoy: filtros + transacciones + egresos | breakdown + ranking + arqueo
// ───────────────────────────────────────────────────────────────────────

function TabHoy({
  data,
  isLoading,
  error,
  filterMethod,
  onFilterChange,
  onOpenClose,
  closed,
}: {
  data: DailyCashSummary | undefined
  isLoading: boolean
  error: Error | null
  filterMethod: string
  onFilterChange: (m: string) => void
  onOpenClose: () => void
  closed: boolean
}) {
  const txns = data?.payments ?? []
  const totalAmount = data?.totalAmount ?? 0

  // Conteo por método para los pills.
  const countByMethod = useMemo(() => {
    const m: Record<string, number> = {}
    txns.forEach((t) => {
      m[t.method] = (m[t.method] ?? 0) + 1
    })
    return m
  }, [txns])

  const filteredTxns =
    filterMethod === 'all' ? txns : txns.filter((t) => t.method === filterMethod)

  // Ventas por estilista (ordenado descendente).
  const byStylist = useMemo(() => {
    const m: Record<string, number> = {}
    txns.forEach((t) => {
      m[t.stylistName] = (m[t.stylistName] ?? 0) + t.total
    })
    return Object.entries(m).sort((a, b) => b[1] - a[1])
  }, [txns])

  // byMethod del backend ya viene ordenado descendente.
  const breakdown = data?.byMethod ?? []

  return (
    <div className="px-6 lg:px-8 py-6 grid lg:grid-cols-3 gap-5">
      {/* IZQ */}
      <div className="lg:col-span-2 space-y-5">
        {/* Filtro por método */}
        <div className="flex items-center gap-1.5 flex-wrap">
          <button
            type="button"
            onClick={() => onFilterChange('all')}
            className={cls(
              'px-3 py-1.5 rounded-lg text-[12px] font-medium transition',
              filterMethod === 'all'
                ? 'bg-warm-800 text-white'
                : 'bg-white border border-warm-200 text-warm-600 hover:border-warm-300',
            )}
          >
            Todas <span className="tabular-nums opacity-70">{txns.length}</span>
          </button>
          {Object.entries(countByMethod).map(([method, count]) => {
            const m = METHOD_LOOK[method] ?? METHOD_LOOK.Other
            return (
              <button
                key={method}
                type="button"
                onClick={() => onFilterChange(method)}
                className={cls(
                  'px-3 py-1.5 rounded-lg text-[12px] font-medium flex items-center gap-1.5 transition',
                  filterMethod === method
                    ? 'bg-warm-800 text-white'
                    : 'bg-white border border-warm-200 text-warm-600 hover:border-warm-300',
                )}
              >
                <span className={cls('w-1.5 h-1.5 rounded-full', m.dot)} />
                {m.label} <span className="tabular-nums opacity-70">{count}</span>
              </button>
            )
          })}
        </div>

        {/* Lista de transacciones */}
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden">
          <div className="px-5 py-3.5 border-b border-warm-150 flex items-center justify-between">
            <h3 className="font-serif text-[18px] text-warm-800">Transacciones</h3>
            <span className="text-[11.5px] text-warm-500">
              {filteredTxns.length} cobros
            </span>
          </div>
          <div className="divide-y divide-warm-100 max-h-[420px] overflow-y-auto">
            {error && (
              <div className="px-5 py-8 text-center text-[13px] text-terra-500">
                No se pudo cargar la caja.
              </div>
            )}
            {isLoading && (
              <div className="px-5 py-8 text-center text-[13px] text-warm-400">
                Cargando…
              </div>
            )}
            {!isLoading && !error && filteredTxns.length === 0 && (
              <div className="px-5 py-10 text-center text-[13px] text-warm-500">
                Aún no hay pagos registrados {filterMethod === 'all' ? 'hoy' : 'de ese método'}.
              </div>
            )}
            {filteredTxns.map((t) => (
              <TxnRow key={t.id} txn={t} />
            ))}
          </div>
        </div>

        {/* Egresos */}
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden">
          <div className="px-5 py-3.5 border-b border-warm-150 flex items-center justify-between">
            <h3 className="font-serif text-[18px] text-warm-800">Egresos del día</h3>
            <button
              type="button"
              disabled
              title="Próximamente"
              className="text-[12px] text-warm-400 font-medium flex items-center gap-1 cursor-not-allowed"
            >
              <Plus size={14} strokeWidth={2.2} /> Registrar egreso
            </button>
          </div>
          <div className="px-5 py-8 text-center text-[12.5px] text-warm-500">
            Los egresos en efectivo del día —compras a proveedores, propinas,
            domicilios— vivirán acá pronto.
          </div>
        </div>
      </div>

      {/* DER */}
      <div className="space-y-5">
        {/* Por método */}
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft p-5">
          <h3 className="font-serif text-[18px] text-warm-800 mb-4">Por método de pago</h3>
          {breakdown.length === 0 ? (
            <div className="text-[12.5px] text-warm-500 py-3 text-center">
              Sin movimientos aún.
            </div>
          ) : (
            <div className="space-y-3.5">
              {breakdown.map((b) => {
                const m = METHOD_LOOK[b.method] ?? METHOD_LOOK.Other
                const pct = totalAmount > 0 ? (b.total / totalAmount) * 100 : 0
                return (
                  <div key={b.method}>
                    <div className="flex items-center justify-between mb-1.5">
                      <div className="flex items-center gap-2 text-[12.5px] text-warm-700">
                        <span
                          className={cls(
                            'w-6 h-6 rounded-md flex items-center justify-center',
                            m.tone,
                          )}
                        >
                          {m.icon}
                        </span>
                        {m.label}
                      </div>
                      <span className="text-[13px] font-medium text-warm-800 tabular-nums">
                        {fmtCop(b.total)}
                      </span>
                    </div>
                    <div className="h-1.5 rounded-full bg-warm-100 overflow-hidden">
                      <div
                        className={cls('h-full rounded-full', m.dot)}
                        style={{ width: pct + '%' }}
                      />
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Por estilista */}
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft p-5">
          <h3 className="font-serif text-[18px] text-warm-800 mb-4">Ventas por estilista</h3>
          {byStylist.length === 0 ? (
            <div className="text-[12.5px] text-warm-500 py-3 text-center">
              Aún sin ventas.
            </div>
          ) : (
            <div className="space-y-2.5">
              {byStylist.map(([name, val], i) => (
                <div key={name} className="flex items-center gap-3">
                  <span className="font-serif text-[14px] text-warm-400 w-5 tabular-nums">
                    {i + 1}
                  </span>
                  <span className="flex-1 text-[13px] text-warm-700">{name}</span>
                  <span className="text-[13px] font-medium text-warm-800 tabular-nums">
                    {fmtCop(val)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Arqueo */}
        <div className="bg-warm-800 text-white rounded-2xl p-5">
          <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium">
            Arqueo de efectivo
          </div>
          <div className="mt-3 space-y-1.5 text-[12.5px]">
            <div className="flex justify-between">
              <span className="text-warm-300">Base inicial</span>
              <span className="tabular-nums">{fmtCop(BASE_INICIAL)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-warm-300">+ Ventas efectivo</span>
              <span className="tabular-nums">{fmtCop(cashSalesFor(data))}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-warm-300">− Egresos efectivo</span>
              <span className="tabular-nums">-{fmtCop(0)}</span>
            </div>
            <div className="pt-2 mt-1 border-t border-white/15 flex justify-between items-center">
              <span className="font-medium">Esperado en caja</span>
              <span className="font-serif text-[20px] tabular-nums">
                {fmtCop(expectedCashFor(data))}
              </span>
            </div>
          </div>
          {!closed && (
            <button
              type="button"
              onClick={onOpenClose}
              disabled={isLoading || !data}
              className={cls(
                'mt-4 w-full px-4 py-2.5 rounded-lg bg-gold-300 hover:bg-gold-200',
                'text-warm-800 text-[13px] font-medium flex items-center justify-center gap-2',
                'disabled:opacity-50 disabled:cursor-not-allowed',
              )}
            >
              <Lock size={15} strokeWidth={1.8} /> Hacer arqueo y cerrar
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function TxnRow({ txn }: { txn: PaymentResponse }) {
  const m = METHOD_LOOK[txn.method] ?? METHOD_LOOK.Other
  return (
    <div className="px-5 py-3 flex items-center gap-3 hover:bg-warm-50/50 transition">
      <div className="text-[11.5px] tabular-nums text-warm-400 w-10">
        {formatHHmm(txn.registeredAt)}
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[13px] font-medium text-warm-800 truncate">
          {txn.customerName}
        </div>
        <div className="text-[11.5px] text-warm-500 truncate">
          {txn.serviceName} · {txn.stylistName}
        </div>
      </div>
      <div
        className={cls(
          'text-[10.5px] font-medium px-2 py-0.5 rounded-md flex items-center gap-1',
          m.tone,
        )}
      >
        {m.label}
      </div>
      <div className="text-[13.5px] font-medium text-warm-800 tabular-nums w-24 text-right">
        {fmtCop(txn.total)}
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Tab Historial — placeholder hasta que exista entidad CashClosing.
// ───────────────────────────────────────────────────────────────────────

function TabHistorial() {
  return (
    <div className="px-6 lg:px-8 py-6">
      <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
              <th className="text-left font-medium px-5 py-3">Fecha</th>
              <th className="text-right font-medium px-5 py-3">Total</th>
              <th className="text-right font-medium px-5 py-3 hidden sm:table-cell">
                Transacciones
              </th>
              <th className="text-right font-medium px-5 py-3">Diferencia</th>
              <th className="text-left font-medium px-5 py-3 hidden md:table-cell">
                Cerrada por
              </th>
              <th className="px-5 py-3 w-10" />
            </tr>
          </thead>
          <tbody>
            <tr>
              <td colSpan={6} className="px-5 py-16 text-center text-[13px] text-warm-500">
                Cuando empieces a cerrar la caja cada noche, el historial
                aparecerá acá. (próximamente)
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal de cierre / arqueo
// ───────────────────────────────────────────────────────────────────────

function CloseModal({
  open,
  onClose,
  expected,
  cashSales,
  onConfirm,
}: {
  open: boolean
  onClose: () => void
  expected: number
  cashSales: number
  onConfirm: (counted: number, diff: number, note: string) => void
}) {
  const [counted, setCounted] = useState('')
  const [note, setNote] = useState('')
  useEffect(() => {
    if (open) {
      setCounted('')
      setNote('')
    }
  }, [open])

  if (!open) return null
  const countedNum = parseInt(counted.replace(/[^0-9]/g, '')) || 0
  const diff = countedNum - expected
  const hasCount = counted !== ''
  const hasDiff = hasCount && diff !== 0

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center px-4 py-8"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm anim-fade" />
      <div
        className="relative w-full max-w-md bg-white rounded-2xl shadow-pop overflow-hidden anim-fade"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium">
              Arqueo de efectivo
            </div>
            <h3 className="font-serif text-[26px] text-warm-800 mt-1 leading-tight">
              Cerrar caja del día
            </h3>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center"
            aria-label="Cerrar"
          >
            <X size={18} strokeWidth={2} />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4">
          <div className="rounded-xl bg-warm-50 border border-warm-150 p-4 space-y-2">
            <Row k="Base inicial" v={fmtCop(BASE_INICIAL)} />
            <Row k="+ Ventas en efectivo" v={fmtCop(cashSales)} />
            <Row k="− Egresos en efectivo" v={'-' + fmtCop(0)} />
            <div className="pt-2 border-t border-warm-200 flex items-center justify-between">
              <span className="text-[13px] font-medium text-warm-800">
                Efectivo esperado en caja
              </span>
              <span className="font-serif text-[20px] tabular-nums text-warm-800">
                {fmtCop(expected)}
              </span>
            </div>
          </div>

          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
              ¿Cuánto efectivo contaste?
            </label>
            <div className="relative">
              <span className="absolute left-3.5 top-1/2 -translate-y-1/2 text-warm-400 text-[15px]">
                $
              </span>
              <input
                value={counted}
                onChange={(e) => setCounted(e.target.value)}
                inputMode="numeric"
                placeholder="0"
                autoFocus
                className={cls(
                  'w-full pl-7 pr-3.5 py-2.5 rounded-lg bg-white border border-warm-200',
                  'text-[15px] text-warm-800 tabular-nums focus:border-brand-500',
                  'focus:ring-2 focus:ring-brand-100 outline-none',
                )}
              />
            </div>
          </div>

          {hasCount && (
            <div
              className={cls(
                'rounded-xl p-4 flex items-center justify-between anim-fade',
                diff === 0
                  ? 'bg-brand-50 ring-1 ring-brand-200'
                  : Math.abs(diff) < 10000
                  ? 'bg-gold-50 ring-1 ring-gold-200'
                  : 'bg-terra-100/60 ring-1 ring-terra-300',
              )}
            >
              <div>
                <div className="text-[11px] tracking-[0.14em] uppercase text-warm-500 font-medium">
                  Diferencia
                </div>
                <div
                  className={cls(
                    'font-serif text-[24px] tabular-nums mt-0.5',
                    diff === 0
                      ? 'text-brand-700'
                      : diff > 0
                      ? 'text-gold-600'
                      : 'text-terra-500',
                  )}
                >
                  {diff > 0 ? '+' : ''}
                  {fmtCop(diff)}
                </div>
              </div>
              <div className="text-[12px] text-warm-600 text-right max-w-[140px]">
                {diff === 0
                  ? '¡Cuadra perfecto!'
                  : diff > 0
                  ? 'Sobra efectivo en caja'
                  : 'Falta efectivo en caja'}
              </div>
            </div>
          )}

          {hasDiff && (
            <div className="anim-fade">
              <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
                ¿A qué se debe la diferencia?
                <span className="text-terra-500 ml-1">*</span>
              </label>
              <textarea
                value={note}
                onChange={(e) => setNote(e.target.value)}
                rows={3}
                placeholder={
                  diff > 0
                    ? 'Ej: una clienta dejó propina en efectivo y no se registró…'
                    : 'Ej: se prestó plata de caja para un domicilio y no se anotó como egreso…'
                }
                className={cls(
                  'w-full px-3 py-2 rounded-lg bg-white border border-warm-200',
                  'text-[13px] text-warm-800 placeholder:text-warm-400',
                  'focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none resize-none',
                )}
              />
              <p className="text-[11px] text-warm-500 mt-1.5">
                Esta nota queda en el registro del cierre. Útil cuando
                revises el historial después.
              </p>
            </div>
          )}
        </div>

        <div className="px-6 py-4 bg-warm-50 border-t border-warm-150 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2.5 rounded-lg text-[13px] text-warm-700 hover:bg-warm-150"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => onConfirm(countedNum, diff, note.trim())}
            disabled={!hasCount || (hasDiff && !note.trim())}
            title={
              hasDiff && !note.trim()
                ? 'Explicá brevemente la diferencia antes de cerrar'
                : undefined
            }
            className={cls(
              'px-5 py-2.5 rounded-lg text-[13px] font-medium flex items-center gap-2 transition',
              hasCount && (!hasDiff || note.trim())
                ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft'
                : 'bg-warm-200 text-warm-400 cursor-not-allowed',
            )}
          >
            <Lock size={15} strokeWidth={1.8} /> Confirmar cierre
          </button>
        </div>
      </div>
    </div>
  )
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <div className="flex items-center justify-between text-[12.5px]">
      <span className="text-warm-500">{k}</span>
      <span className="text-warm-700 tabular-nums">{v}</span>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// KPI card
// ───────────────────────────────────────────────────────────────────────

function Kpi({
  label,
  value,
  sub,
  accent,
  icon,
}: {
  label: string
  value: string
  sub: string
  accent: 'brand' | 'gold' | 'terra'
  icon: React.ReactNode
}) {
  const accents = {
    brand: 'text-brand-700',
    gold: 'text-gold-600',
    terra: 'text-terra-500',
  } as const
  const bgs = {
    brand: 'bg-brand-50',
    gold: 'bg-gold-50',
    terra: 'bg-terra-100/60',
  } as const

  return (
    <div className="bg-white rounded-2xl border border-warm-150 p-5 shadow-soft">
      <div className="flex items-center justify-between">
        <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">
          {label}
        </div>
        <span
          className={cls(
            'w-7 h-7 rounded-lg flex items-center justify-center',
            bgs[accent],
            accents[accent],
          )}
        >
          {icon}
        </span>
      </div>
      <div
        className={cls(
          'font-serif text-[34px] leading-none tabular-nums mt-2.5',
          accents[accent],
        )}
      >
        {value}
      </div>
      <div className="text-[11px] text-warm-500 mt-1.5">{sub}</div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Look-and-feel por método de pago.
// Las claves son los strings que devuelve el backend (PaymentMethod.ToString())
// más alias por compatibilidad. Si el backend agrega un método nuevo cae
// en METHOD_LOOK.Other.
// ───────────────────────────────────────────────────────────────────────

interface MethodLook {
  label: string
  icon: React.ReactNode
  /** clases para el chip / icono cuadrado */
  tone: string
  /** clase de bg del dot (también se usa como color de la barra) */
  dot: string
}

const METHOD_LOOK: Record<string, MethodLook> = {
  Cash: {
    label: 'Efectivo',
    icon: <Banknote size={14} strokeWidth={1.7} />,
    tone: 'text-brand-700 bg-brand-50',
    dot: 'bg-brand-500',
  },
  Bancolombia: {
    label: 'Bancolombia',
    icon: <CreditCard size={14} strokeWidth={1.7} />,
    tone: 'text-[#7a5e2d] bg-[#f6ecd0]',
    dot: 'bg-[#d4a72b]',
  },
  Nequi: {
    label: 'Nequi',
    icon: <Smartphone size={14} strokeWidth={1.7} />,
    tone: 'text-[#7a2d6b] bg-[#f3dcee]',
    dot: 'bg-[#c026a8]',
  },
  Daviplata: {
    label: 'Daviplata',
    icon: <Smartphone size={14} strokeWidth={1.7} />,
    tone: 'text-[#9a2828] bg-[#f6dede]',
    dot: 'bg-[#d33333]',
  },
  CreditCard: {
    label: 'Datáfono',
    icon: <CreditCard size={14} strokeWidth={1.7} />,
    tone: 'text-[#3d5664] bg-[#dde7ec]',
    dot: 'bg-[#5d7a8a]',
  },
  DebitCard: {
    label: 'Datáfono',
    icon: <CreditCard size={14} strokeWidth={1.7} />,
    tone: 'text-[#3d5664] bg-[#dde7ec]',
    dot: 'bg-[#5d7a8a]',
  },
  Other: {
    label: 'Otro',
    icon: <ChevronRight size={14} strokeWidth={1.7} />,
    tone: 'text-warm-600 bg-warm-100',
    dot: 'bg-warm-400',
  },
}

// ───────────────────────────────────────────────────────────────────────
// Helpers
// ───────────────────────────────────────────────────────────────────────

/** Base inicial: hoy es constante. Cuando exista config por salón, leer de ahí. */
const BASE_INICIAL = 100000

function cashSalesFor(data?: DailyCashSummary): number {
  if (!data) return 0
  return data.byMethod.find((b) => b.method === 'Cash')?.total ?? 0
}

function expectedCashFor(data?: DailyCashSummary): number {
  // expected = base + ventas efectivo − egresos efectivo (hoy 0)
  return BASE_INICIAL + cashSalesFor(data)
}

/** "2026-06-03" en zona local — coincide con lo que el backend espera. */
function formatLocalDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

/** "Martes 3 de junio, 2026" — para el subtítulo del header. */
function formatHumanDate(d: Date): string {
  return new Intl.DateTimeFormat('es-CO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(d)
}

/** "09:15" desde un ISO. */
function formatHHmm(iso: string): string {
  const d = new Date(iso)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}
