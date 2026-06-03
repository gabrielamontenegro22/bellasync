import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Banknote, ChevronLeft, ChevronRight, CheckCircle, AlertCircle,
  CreditCard, Smartphone, Wallet,
} from 'lucide-react'
import { Card, Input } from '@/components/ui'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import {
  getDailyCashSummary,
  type DailyCashSummary,
  type MethodBreakdownItem,
} from '@/api/cash'
import { METHOD_BADGE } from '@/features/payments/components/RegisterPaymentModal'
import {
  fmtCop, initialsOf, toneOf,
} from '@/features/customers/lib/customerLook'
import type { PaymentMethod } from '@/api/payments'

/**
 * `/caja` — pantalla nocturna de cierre. Lo que la admin abre al
 * final del día para conciliar lo facturado contra los movimientos
 * del banco.
 *
 * Estructura (alineada con el bullet del contexto que dio Gabriela:
 * "suma todo lo facturado, separa lo cobrado en efectivo de lo cobrado
 * por transferencia, y le pide que cruce contra el banco. Doble
 * validación humana antes de cerrar."):
 *
 *  1. Date nav (Ayer / Hoy / Mañana) + date picker.
 *  2. Stats cards: Total facturado · # pagos · Propinas.
 *  3. Breakdown por método con barras de proporción.
 *  4. Input "Saldo del banco" con cruce automático: ✓ cuadra o
 *     ✗ diferencia $X (en rojo si negativa, verde si positiva).
 *  5. Tabla de pagos del día para drill-down.
 */
export function CashClosingPage() {
  const { user } = useAuth()
  const [date, setDate] = useState(() => formatLocalDate(new Date()))
  // Cruce con banco — solo client-side, no se persiste hoy.
  const [bankBalance, setBankBalance] = useState<string>('')

  const { data, isLoading, error } = useQuery({
    queryKey: ['cash', date],
    queryFn: () => getDailyCashSummary(date),
  })

  const today = formatLocalDate(new Date())
  const isToday = date === today
  const yesterday = formatLocalDate(addDays(parseLocalDate(date), -1))
  const tomorrow = formatLocalDate(addDays(parseLocalDate(date), 1))

  // Cruce contra banco — diferencia entre lo declarado (saldo) y lo
  // que tenemos registrado en payments digitales (excluyendo efectivo).
  const cruce = useMemo(() => {
    if (!data || !bankBalance.trim()) return null
    const declared = Number(bankBalance)
    if (!Number.isFinite(declared)) return null
    const expected = data.byMethod
      .filter(b => b.method !== 'Cash')  // efectivo NO entra al banco
      .reduce((acc, b) => acc + b.total, 0)
    const diff = declared - expected
    return { expected, declared, diff, matches: Math.abs(diff) < 0.01 }
  }, [data, bankBalance])

  return (
    <div className="flex h-full min-h-0 bg-warm-50 overflow-hidden">
      <div className="flex-1 min-w-0 overflow-y-auto">
        {/* Header */}
        <header className="px-5 lg:px-8 pt-5 lg:pt-7 pb-4 bg-white border-b border-warm-150">
          <div className="text-[12px] uppercase tracking-[0.12em] text-warm-500 font-medium">
            <span className="text-brand-700">{user?.tenantName ?? 'Salón'}</span>
            <span className="text-warm-300 mx-2">•</span>
            <span>Cierre de caja</span>
          </div>
          <div className="mt-1 flex items-baseline gap-3 flex-wrap">
            <h1 className="font-serif text-[32px] lg:text-[40px] leading-[1.05] text-warm-800 tracking-tight">
              {fmtDateLong(parseLocalDate(date))}
            </h1>
            {isToday && (
              <span className="text-[11.5px] font-medium text-brand-700 bg-brand-50 px-2 py-0.5 rounded-full uppercase tracking-wider">
                Hoy
              </span>
            )}
          </div>

          {/* Day nav */}
          <div className="mt-5 flex items-center gap-1.5 text-[13.5px] flex-wrap">
            <button
              type="button"
              onClick={() => setDate(yesterday)}
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-warm-600 hover:text-warm-800 hover:bg-warm-100 rounded-md"
            >
              <ChevronLeft size={16} /> Ayer
              <span className="text-warm-400 text-[12px] ml-1 tabular-nums">
                {fmtDateShort(parseLocalDate(yesterday))}
              </span>
            </button>
            <button
              type="button"
              onClick={() => setDate(today)}
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
              onClick={() => setDate(tomorrow)}
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-warm-600 hover:text-warm-800 hover:bg-warm-100 rounded-md"
            >
              <span className="text-warm-400 text-[12px] mr-1 tabular-nums">
                {fmtDateShort(parseLocalDate(tomorrow))}
              </span>
              Mañana <ChevronRight size={16} />
            </button>
            <input
              type="date"
              value={date}
              onChange={e => setDate(e.target.value)}
              className="ml-2 rounded-md border border-warm-200 bg-white px-2 py-1.5 text-[12.5px] text-warm-700"
            />
          </div>
        </header>

        <div className="px-5 lg:px-8 py-5 space-y-5">
          {isLoading && <p className="text-[13px] text-warm-500">Cargando caja…</p>}
          {error && <p className="text-[13px] text-terra-500">No se pudo cargar el resumen.</p>}

          {data && <StatsRow data={data} />}
          {data && data.byMethod.length > 0 && <MethodBreakdown data={data} />}
          {data && <BankReconcile
            bankBalance={bankBalance}
            onBankBalanceChange={setBankBalance}
            cruce={cruce}
          />}
          {data && <PaymentsTable data={data} />}
        </div>
      </div>
    </div>
  )
}

// ============================================================
// Stats arriba: Total facturado · # pagos · Propinas
// ============================================================
function StatsRow({ data }: { data: DailyCashSummary }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
      <StatCard
        icon={Wallet}
        accent="bg-brand-50 text-brand-700 border-brand-100"
        label="Total facturado"
        value={fmtCop(data.totalAmount)}
        hint={`${data.paymentCount} pago${data.paymentCount === 1 ? '' : 's'} registrado${data.paymentCount === 1 ? '' : 's'}`}
      />
      <StatCard
        icon={Banknote}
        accent="bg-warm-100 text-warm-700 border-warm-200"
        label="Pagos del día"
        value={data.paymentCount.toString()}
        hint="incluye efectivo + digitales"
      />
      <StatCard
        icon={CheckCircle}
        accent="bg-gold-50 text-gold-600 border-gold-200"
        label="Propinas"
        value={fmtCop(data.totalTips)}
        hint="ya incluidas en el total"
      />
    </div>
  )
}

function StatCard({
  icon: I, accent, label, value, hint,
}: {
  icon: React.ComponentType<{ size?: number }>
  accent: string
  label: string
  value: string
  hint: string
}) {
  return (
    <div className="bg-white border border-warm-150 rounded-xl p-4 shadow-softer">
      <div className={cls('w-10 h-10 rounded-lg border flex items-center justify-center', accent)}>
        <I size={18} />
      </div>
      <div className="mt-3 text-[11px] tracking-[0.12em] uppercase text-warm-500 font-medium">
        {label}
      </div>
      <div className="font-serif text-[28px] text-warm-800 leading-none mt-1.5 tabular-nums">
        {value}
      </div>
      <div className="text-[12px] text-warm-500 mt-1.5">{hint}</div>
    </div>
  )
}

// ============================================================
// Breakdown por método con barras de proporción
// ============================================================
function MethodBreakdown({ data }: { data: DailyCashSummary }) {
  return (
    <Card className="p-5">
      <div className="text-[11px] tracking-[0.14em] uppercase text-warm-500 font-medium mb-4">
        Desglose por método
      </div>
      <div className="space-y-3">
        {data.byMethod.map(item => (
          <MethodRow key={item.method} item={item} total={data.totalAmount} />
        ))}
      </div>
    </Card>
  )
}

function MethodRow({ item, total }: { item: MethodBreakdownItem; total: number }) {
  const badge = METHOD_BADGE[item.method as PaymentMethod]
  const pct = total > 0 ? (item.total / total) * 100 : 0
  return (
    <div>
      <div className="flex items-baseline justify-between mb-1.5">
        <div className="flex items-center gap-2.5">
          <MethodIcon method={item.method as PaymentMethod} />
          <span className={cls('text-[11.5px] px-2 py-0.5 rounded-md font-medium', badge.bg, badge.fg)}>
            {badge.label}
          </span>
          <span className="text-[12px] text-warm-500 tabular-nums">
            {item.count} {item.count === 1 ? 'pago' : 'pagos'}
          </span>
        </div>
        <div className="flex items-baseline gap-2">
          <span className="font-serif text-[18px] text-warm-800 tabular-nums">
            {fmtCop(item.total)}
          </span>
          <span className="text-[11.5px] text-warm-400 tabular-nums">
            {pct.toFixed(0)}%
          </span>
        </div>
      </div>
      <div className="h-1.5 rounded-full bg-warm-100 overflow-hidden">
        <div
          className={cls('h-full rounded-full', getBarColor(item.method as PaymentMethod))}
          style={{ width: pct + '%' }}
        />
      </div>
    </div>
  )
}

function MethodIcon({ method }: { method: PaymentMethod }) {
  const I =
    method === 'Cash' ? Banknote
    : method === 'CreditCard' || method === 'DebitCard' ? CreditCard
    : Smartphone
  return <I size={14} className="text-warm-500" />
}

function getBarColor(method: PaymentMethod): string {
  switch (method) {
    case 'Cash': return 'bg-brand-500'
    case 'Bancolombia': return 'bg-[#fdda24]'
    case 'Nequi': return 'bg-[#da1e8e]'
    case 'Daviplata': return 'bg-[#e2231a]'
    case 'CreditCard':
    case 'DebitCard': return 'bg-warm-400'
    default: return 'bg-warm-300'
  }
}

// ============================================================
// Cruce con saldo del banco
// ============================================================
function BankReconcile({
  bankBalance, onBankBalanceChange, cruce,
}: {
  bankBalance: string
  onBankBalanceChange: (v: string) => void
  cruce: { expected: number; declared: number; diff: number; matches: boolean } | null
}) {
  return (
    <Card className="p-5">
      <div className="flex items-start gap-3">
        <div className="w-10 h-10 rounded-lg bg-warm-100 border border-warm-200 text-warm-700 flex items-center justify-center flex-shrink-0">
          <CheckCircle size={18} />
        </div>
        <div className="flex-1 min-w-0">
          <div className="text-[14px] font-medium text-warm-800">Cruce con el banco</div>
          <p className="text-[12.5px] text-warm-500 mt-1">
            Mirá el saldo total que entró a la cuenta del salón hoy (suma de las
            transferencias en la app del banco, sin contar lo de efectivo).
            BellaSync compara contra los pagos digitales registrados acá.
          </p>
          <div className="mt-3 flex items-center gap-2">
            <span className="text-[13px] text-warm-600 shrink-0">Saldo del banco:</span>
            <Input
              type="number"
              min={0}
              step={1000}
              value={bankBalance}
              onChange={e => onBankBalanceChange(e.target.value)}
              placeholder="0"
              className="w-40"
            />
          </div>
          {cruce && (
            <div className={cls(
              'mt-3 rounded-lg p-3 text-[13px] flex items-start gap-2',
              cruce.matches
                ? 'bg-brand-50 border border-brand-100 text-brand-800'
                : 'bg-terra-100 border border-terra-300 text-terra-500',
            )}>
              {cruce.matches
                ? <CheckCircle size={16} className="mt-0.5 flex-shrink-0" />
                : <AlertCircle size={16} className="mt-0.5 flex-shrink-0" />}
              <div className="flex-1 tabular-nums">
                {cruce.matches ? (
                  <span className="font-semibold">✓ Cuadra. Saldo del banco coincide con lo registrado.</span>
                ) : (
                  <>
                    <div className="font-semibold">
                      {cruce.diff > 0 ? 'Sobra' : 'Falta'} {fmtCop(Math.abs(cruce.diff))} en el banco.
                    </div>
                    <div className="text-[12px] mt-0.5 opacity-80">
                      Registrado en BellaSync: {fmtCop(cruce.expected)} ·
                      Declarado del banco: {fmtCop(cruce.declared)}.
                      {cruce.diff > 0 && ' Quizá hubo una transferencia que no se registró.'}
                      {cruce.diff < 0 && ' Quizá se registró un pago que no llegó al banco.'}
                    </div>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </Card>
  )
}

// ============================================================
// Tabla de pagos del día
// ============================================================
function PaymentsTable({ data }: { data: DailyCashSummary }) {
  if (data.payments.length === 0) {
    return (
      <Card className="p-10 text-center">
        <Banknote size={28} className="mx-auto text-warm-400" />
        <div className="font-serif text-[20px] text-warm-700 mt-3">Sin pagos este día</div>
        <div className="text-[13px] text-warm-500 mt-1 max-w-md mx-auto">
          Cuando registres cobros en la agenda, aparecerán acá para el cierre del día.
        </div>
      </Card>
    )
  }

  return (
    <Card className="overflow-hidden p-0">
      <div className="px-5 py-3 border-b border-warm-150">
        <div className="text-[11px] tracking-[0.14em] uppercase text-warm-500 font-medium">
          Pagos del día ({data.payments.length})
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead className="bg-warm-50/60 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
            <tr>
              <th className="text-left font-medium pl-5 pr-3 py-3">Hora</th>
              <th className="text-left font-medium px-3 py-3">Cliente</th>
              <th className="text-left font-medium px-3 py-3">Servicio</th>
              <th className="text-left font-medium px-3 py-3">Método</th>
              <th className="text-left font-medium px-3 py-3">Referencia</th>
              <th className="text-right font-medium pr-5 pl-3 py-3">Monto</th>
            </tr>
          </thead>
          <tbody>
            {data.payments.map(p => {
              const badge = METHOD_BADGE[p.method]
              const tone = toneOf(p.appointmentId)
              return (
                <tr key={p.id} className="border-b border-warm-100 last:border-0 hover:bg-warm-50/40">
                  <td className="py-3 pl-5 pr-3 text-warm-700 tabular-nums">
                    {formatTime(p.registeredAt)}
                  </td>
                  <td className="py-3 px-3">
                    <div className="flex items-center gap-2">
                      <div className={cls(
                        'w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-semibold flex-shrink-0',
                        tone.bg, tone.fg,
                      )}>
                        {initialsOf(p.stylistName ?? '—')}
                      </div>
                      <span className="text-warm-700">{p.stylistName}</span>
                    </div>
                  </td>
                  <td className="py-3 px-3 text-warm-800">{p.serviceName}</td>
                  <td className="py-3 px-3">
                    <span className={cls('text-[11.5px] px-2 py-0.5 rounded-md', badge.bg, badge.fg)}>
                      {badge.label}
                    </span>
                  </td>
                  <td className="py-3 px-3 font-mono text-[11.5px] text-warm-500">
                    {p.reference ?? '—'}
                  </td>
                  <td className="py-3 pr-5 pl-3 text-right tabular-nums font-medium text-warm-800">
                    {fmtCop(p.total)}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

// ============================================================
// Helpers fecha
// ============================================================
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

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-CO', {
    hour: '2-digit', minute: '2-digit', hour12: false,
  })
}
