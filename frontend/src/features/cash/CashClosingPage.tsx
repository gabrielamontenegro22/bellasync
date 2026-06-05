import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowDownRight, ArrowUpRight,
  Banknote, Clock, CreditCard,
  Download, Lock, Plus, Smartphone, CheckCircle2, X,
} from 'lucide-react'
import { PROVIDER_COLORS, PROVIDER_FALLBACK_COLOR } from '@/features/payments/paymentCatalog'
import { cls } from '@/lib/cls'
import { useAuth, useIsAdmin } from '@/features/auth/useAuth'
import { getReceptionPermissions } from '@/api/admin'
import { fmtCop } from '@/features/customers/lib/customerLook'
import { extractApiError } from '@/lib/extractApiError'
import {
  getDailyCashSummary,
  getCashClosingForDate,
  createCashClosing,
  listCashClosings,
  type DailyCashSummary,
  type CashClosing,
} from '@/api/cash'
import type { ExpenseResponse } from '@/api/expenses'
import type { PaymentResponse } from '@/api/payments'
import { getPaymentBadge } from '@/features/payments/paymentBadge'
import { RegisterExpenseModal } from './components/RegisterExpenseModal'

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
  const isAdmin = useIsAdmin()
  const qc = useQueryClient()

  // Permisos del salón. Solo nos importa para recepción — admin no se
  // restringe nunca. staleTime alto: la admin cambia esto raramente.
  const permsQ = useQuery({
    queryKey: ['receptionPermissions'],
    queryFn: getReceptionPermissions,
    enabled: !isAdmin,
    staleTime: 5 * 60_000,
  })
  // Cierre permitido si sos admin O si la admin habilitó el toggle.
  // Mientras carga (recepción + primer fetch), asumimos NO para evitar
  // flicker del botón.
  const canCloseCash = isAdmin || (permsQ.data?.canCloseCash ?? false)
  const [tab, setTab] = useState<'hoy' | 'historial'>('hoy')
  const [filterMethod, setFilterMethod] = useState<string>('all')
  const [closeOpen, setCloseOpen] = useState(false)
  const [expenseOpen, setExpenseOpen] = useState(false)

  // Por ahora fijamos hoy. Cuando el endpoint acepte navegación,
  // exponemos un date picker (ver TODO en /caja date nav).
  const today = formatLocalDate(new Date())

  const { data, isLoading, error } = useQuery({
    queryKey: ['cash', today],
    queryFn: () => getDailyCashSummary(today),
  })

  // ¿Ya se cerró hoy? Lo persistido manda — si refrescás la página, el
  // pill "Caja cerrada" sigue visible y el botón de cerrar deshabilitado.
  const { data: existingClosing } = useQuery({
    queryKey: ['cashClosing', today],
    queryFn: () => getCashClosingForDate(today),
  })
  const isClosedToday = !!existingClosing

  // Mutación de cierre. Al éxito invalida ambas queries (estado actual
  // y lista del historial) para que el pill y la tabla se actualicen.
  const createMutation = useMutation({
    mutationFn: createCashClosing,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cashClosing'] })
      qc.invalidateQueries({ queryKey: ['cashClosings'] })
      setCloseOpen(false)
    },
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
          {!isClosedToday && canCloseCash && (
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
          {!isClosedToday && !canCloseCash && (
            <div
              title="La administradora del salón es quien firma el cierre de caja"
              className="px-3.5 py-2 rounded-lg border border-warm-200 text-warm-500 text-[12px] italic flex items-center gap-1.5"
            >
              <Lock size={13} /> Cierre solo admin
            </div>
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
            <h1 className="font-serif text-[30px] sm:text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
              Caja de hoy
            </h1>
            <p className="text-[13.5px] text-warm-500 mt-1.5 flex items-center gap-2">
              <Clock size={13} strokeWidth={1.8} />
              {formatHumanDate(new Date())} · Abierta desde las 8:00 am
            </p>
          </div>
          {existingClosing ? (
            <div
              className="rounded-xl bg-brand-50 ring-1 ring-brand-200 px-4 py-2.5 flex items-center gap-2.5 max-w-[420px]"
              title={existingClosing.diffNote ? `Nota: ${existingClosing.diffNote}` : undefined}
            >
              <span className="w-7 h-7 rounded-full bg-brand-700 text-white flex items-center justify-center flex-shrink-0">
                <CheckCircle2 size={15} strokeWidth={2.4} />
              </span>
              <div className="min-w-0">
                <div className="text-[12.5px] font-medium text-brand-800">Caja cerrada</div>
                <div className="text-[11px] text-warm-600">
                  Diferencia:{' '}
                  {existingClosing.diff === 0
                    ? 'cuadró perfecto'
                    : (existingClosing.diff > 0 ? '+' : '') + fmtCop(existingClosing.diff)}
                </div>
                {existingClosing.diffNote && (
                  <div className="text-[11px] text-warm-500 italic truncate mt-0.5">
                    “{existingClosing.diffNote}”
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
            value={fmtCop(data?.totalExpenses ?? 0)}
            sub={`${data?.expenses.length ?? 0} salidas`}
            accent="terra"
            icon={<ArrowDownRight size={14} strokeWidth={1.8} />}
          />
          <Kpi
            label="Neto en caja"
            value={fmtCop((data?.totalAmount ?? 0) - (data?.totalExpenses ?? 0))}
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
          onOpenExpense={() => setExpenseOpen(true)}
          closed={isClosedToday}
        />
      ) : (
        <TabHistorial />
      )}

      <CloseModal
        open={closeOpen}
        onClose={() => setCloseOpen(false)}
        expected={expectedCashFor(data)}
        cashSales={cashSalesFor(data)}
        cashExpenses={cashExpensesFor(data)}
        submitting={createMutation.isPending}
        submitError={createMutation.error ? extractApiError(createMutation.error, 'No se pudo cerrar la caja.') : null}
        onConfirm={(counted, _diff, note) => {
          createMutation.mutate({
            closedDate: today,
            baseAmount: BASE_INICIAL,
            countedCash: counted,
            diffNote: note || null,
          })
        }}
      />

      <RegisterExpenseModal
        open={expenseOpen}
        onClose={() => setExpenseOpen(false)}
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
  onOpenExpense,
  closed,
}: {
  data: DailyCashSummary | undefined
  isLoading: boolean
  error: Error | null
  filterMethod: string
  onFilterChange: (m: string) => void
  onOpenClose: () => void
  onOpenExpense: () => void
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
              onClick={onOpenExpense}
              className="text-[12px] text-brand-700 font-medium flex items-center gap-1 hover:underline"
            >
              <Plus size={14} strokeWidth={2.2} /> Registrar egreso
            </button>
          </div>
          {(data?.expenses.length ?? 0) === 0 ? (
            <div className="px-5 py-8 text-center text-[12.5px] text-warm-500">
              Aún no hay egresos registrados hoy.
            </div>
          ) : (
            <div className="divide-y divide-warm-100">
              {data!.expenses.map((e) => (
                <ExpenseRow key={e.id} expense={e} />
              ))}
            </div>
          )}
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
            <div className="space-y-4">
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

                    {/* Sub-líneas por proveedor (banco/marca). Solo si
                        hay más de uno — si todo el Transfer del día fue
                        Bancolombia, no aporta repetir la cifra. */}
                    {b.byProvider.length > 1 && (
                      <div className="mt-2 ml-8 space-y-1">
                        {b.byProvider.map((p) => {
                          const color = p.provider
                            ? (PROVIDER_COLORS[p.provider] ?? PROVIDER_FALLBACK_COLOR)
                            : PROVIDER_FALLBACK_COLOR
                          return (
                            <div
                              key={p.provider ?? '__none__'}
                              className="flex items-center justify-between text-[11.5px] text-warm-600"
                            >
                              <div className="flex items-center gap-1.5">
                                <span className={cls('w-1.5 h-1.5 rounded-full', color.dot)} />
                                <span>{p.provider ?? 'Sin especificar'}</span>
                              </div>
                              <span className="tabular-nums">{fmtCop(p.total)}</span>
                            </div>
                          )
                        })}
                      </div>
                    )}
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
              <span className="tabular-nums">-{fmtCop(cashExpensesFor(data))}</span>
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

function ExpenseRow({ expense }: { expense: ExpenseResponse }) {
  return (
    <div className="px-5 py-3 flex items-center gap-3">
      <div className="text-[11.5px] tabular-nums text-warm-400 w-10">
        {formatHHmm(expense.registeredAt)}
      </div>
      <div className="w-7 h-7 rounded-lg bg-terra-100/60 text-terra-500 flex items-center justify-center flex-shrink-0">
        <ArrowDownRight size={14} strokeWidth={1.8} />
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[13px] text-warm-800 truncate">{expense.concept}</div>
        <div className="text-[11px] text-warm-500 truncate">
          {expense.method !== 'Cash' && <>Pagado con {expense.method} · </>}
          {expense.registeredByUserName
            ? <>Por {expense.registeredByUserName}</>
            : <>Sin firma</>}
        </div>
      </div>
      <div className="text-[13.5px] font-medium text-terra-500 tabular-nums">
        -{fmtCop(expense.amount)}
      </div>
    </div>
  )
}

function TxnRow({ txn }: { txn: PaymentResponse }) {
  // Badge muestra el provider si lo hay (Bancolombia con colores propios),
  // o el genérico de la categoría si no.
  const badge = getPaymentBadge(txn.method, txn.provider)
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
          {txn.registeredByUserName && (
            <> · Cobró <span className="text-warm-700">{txn.registeredByUserName}</span></>
          )}
        </div>
      </div>
      <div
        className={cls(
          'text-[10.5px] font-medium px-2 py-0.5 rounded-md flex items-center gap-1',
          badge.className,
        )}
      >
        {badge.label}
      </div>
      <div className="text-[13.5px] font-medium text-warm-800 tabular-nums w-24 text-right">
        {fmtCop(txn.total)}
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Tab Historial — datos reales del backend (últimos 30 días).
// ───────────────────────────────────────────────────────────────────────

function TabHistorial() {
  const { data: closings, isLoading, error } = useQuery({
    queryKey: ['cashClosings'],
    queryFn: () => listCashClosings(),
  })

  return (
    <div className="px-6 lg:px-8 py-6">
      <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden">
        <div className="overflow-x-auto">
        <table className="w-full text-[13px] min-w-[480px]">
          <thead>
            <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
              <th className="text-left font-medium px-5 py-3">Fecha</th>
              <th className="text-right font-medium px-5 py-3">Total recaudado</th>
              <th className="text-right font-medium px-5 py-3 hidden sm:table-cell">
                Efectivo contado
              </th>
              <th className="text-right font-medium px-5 py-3">Diferencia</th>
              <th className="text-left font-medium px-5 py-3 hidden md:table-cell">Nota</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-5 py-10 text-center text-[13px] text-warm-400">
                  Cargando historial…
                </td>
              </tr>
            )}
            {error && (
              <tr>
                <td colSpan={5} className="px-5 py-10 text-center text-[13px] text-terra-500">
                  No se pudo cargar el historial.
                </td>
              </tr>
            )}
            {!isLoading && !error && (closings?.length ?? 0) === 0 && (
              <tr>
                <td colSpan={5} className="px-5 py-16 text-center text-[13px] text-warm-500">
                  Cuando empieces a cerrar la caja cada noche, el historial aparecerá acá.
                </td>
              </tr>
            )}
            {closings?.map((cc: CashClosing) => {
              const diffColor =
                cc.diff === 0 ? 'text-brand-700' : cc.diff > 0 ? 'text-gold-600' : 'text-terra-500'
              const diffLabel =
                cc.diff === 0 ? 'Cuadró' : (cc.diff > 0 ? '+' : '') + fmtCop(cc.diff)
              return (
                <tr key={cc.id} className="border-b border-warm-100 last:border-0 hover:bg-warm-50/40">
                  <td className="px-5 py-3.5 font-medium text-warm-800 tabular-nums">
                    {formatHumanDateShort(cc.closedDate)}
                    {cc.closedByUserName && (
                      <div className="text-[10.5px] font-normal text-warm-500 mt-0.5">
                        Por {cc.closedByUserName}
                      </div>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-right tabular-nums text-warm-800">
                    {fmtCop(cc.totalAmount)}
                  </td>
                  <td className="px-5 py-3.5 text-right tabular-nums text-warm-700 hidden sm:table-cell">
                    {fmtCop(cc.countedCash)}
                    <span className="text-warm-400 text-[11px] ml-1">
                      / {fmtCop(cc.expectedCash)}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-right tabular-nums">
                    <span className={cls('font-medium', diffColor)}>{diffLabel}</span>
                  </td>
                  <td className="px-5 py-3.5 text-warm-500 hidden md:table-cell italic truncate max-w-[260px]">
                    {cc.diffNote ?? '—'}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        </div>
      </div>
    </div>
  )
}

/** "Mié 3 jun" — compacto para tabla. */
function formatHumanDateShort(yyyyMmDd: string): string {
  const [y, m, d] = yyyyMmDd.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('es-CO', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
  }).format(date)
}

// ───────────────────────────────────────────────────────────────────────
// Modal de cierre / arqueo
// ───────────────────────────────────────────────────────────────────────

function CloseModal({
  open,
  onClose,
  expected,
  cashSales,
  cashExpenses,
  submitting = false,
  submitError = null,
  onConfirm,
}: {
  open: boolean
  onClose: () => void
  expected: number
  cashSales: number
  cashExpenses: number
  submitting?: boolean
  submitError?: string | null
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
    // Sheet en mobile, centrado en desktop. max-h + scroll interno para
    // que la sección "Diferencia" no se corte en pantallas chicas.
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm anim-fade" />
      <div
        className="relative w-full sm:max-w-md max-h-[92vh] sm:max-h-[88vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-fade flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between flex-shrink-0">
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

        <div className="px-6 py-5 space-y-4 overflow-y-auto flex-1">
          <div className="rounded-xl bg-warm-50 border border-warm-150 p-4 space-y-2">
            <Row k="Base inicial" v={fmtCop(BASE_INICIAL)} />
            <Row k="+ Ventas en efectivo" v={fmtCop(cashSales)} />
            <Row k="− Egresos en efectivo" v={'-' + fmtCop(cashExpenses)} />
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

        {submitError && (
          <div className="px-6 pb-3">
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {submitError}
            </div>
          </div>
        )}

        <div className="px-6 py-4 bg-warm-50 border-t border-warm-150 flex items-center justify-end gap-2 flex-shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="px-4 py-2.5 rounded-lg text-[13px] text-warm-700 hover:bg-warm-150 disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => onConfirm(countedNum, diff, note.trim())}
            disabled={!hasCount || (hasDiff && !note.trim()) || submitting}
            title={
              hasDiff && !note.trim()
                ? 'Explicá brevemente la diferencia antes de cerrar'
                : undefined
            }
            className={cls(
              'px-5 py-2.5 rounded-lg text-[13px] font-medium flex items-center gap-2 transition',
              hasCount && (!hasDiff || note.trim()) && !submitting
                ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft'
                : 'bg-warm-200 text-warm-400 cursor-not-allowed',
            )}
          >
            <Lock size={15} strokeWidth={1.8} />
            {submitting ? 'Guardando…' : 'Confirmar cierre'}
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
  Transfer: {
    label: 'Transferencia',
    icon: <Smartphone size={14} strokeWidth={1.7} />,
    tone: 'text-warm-700 bg-warm-100',
    dot: 'bg-warm-500',
  },
  Card: {
    label: 'Tarjeta',
    icon: <CreditCard size={14} strokeWidth={1.7} />,
    tone: 'text-warm-700 bg-warm-100',
    dot: 'bg-warm-500',
  },
  Other: {
    label: 'Otro',
    icon: <span className="text-[10px] leading-none">•••</span>,
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

function cashExpensesFor(data?: DailyCashSummary): number {
  return data?.cashExpenses ?? 0
}

function expectedCashFor(data?: DailyCashSummary): number {
  // expected = base + ventas efectivo − egresos efectivo
  return BASE_INICIAL + cashSalesFor(data) - cashExpensesFor(data)
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
