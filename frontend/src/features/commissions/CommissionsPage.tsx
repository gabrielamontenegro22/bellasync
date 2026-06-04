import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Percent, CheckCircle2, Wallet, X } from 'lucide-react'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { fmtCop } from '@/features/customers/lib/customerLook'
import { extractApiError } from '@/lib/extractApiError'
import {
  getCommissionsSummary,
  createCommissionPayout,
  listCommissionPayouts,
  type StylistCommissionRow,
  type CommissionPayout,
} from '@/api/commissions'
import { useCommissionsSetting } from './useCommissionsSetting'

/**
 * `/comisiones` — la admin ve cuánto se le debe a cada estilista en
 * un período y puede marcar pagos.
 *
 * Si el módulo está OFF para el tenant, mostramos un empty state que
 * lleva a Configuración → Comisiones para activarlo. Así la URL no se
 * rompe pero la pantalla es honesta sobre por qué no hay nada.
 */
export function CommissionsPage() {
  const { user } = useAuth()
  const { data: setting, isLoading: settingLoading } = useCommissionsSetting()

  if (settingLoading) {
    return (
      <div className="flex-1 min-w-0 bg-cream py-16 text-center text-[13px] text-warm-500">
        Cargando…
      </div>
    )
  }

  if (!setting?.enabled) {
    return (
      <div className="flex-1 min-w-0 bg-cream flex items-center justify-center px-6 py-16">
        <div className="max-w-md text-center">
          <div className="w-14 h-14 mx-auto rounded-2xl bg-warm-100 text-warm-500 flex items-center justify-center mb-4">
            <Percent size={26} strokeWidth={1.5} />
          </div>
          <h2 className="font-serif text-[26px] text-warm-800">Comisiones desactivadas</h2>
          <p className="text-[13.5px] text-warm-500 mt-2 leading-relaxed">
            Este módulo está apagado para tu salón. Activalo en
            Configuración si pagás a tus estilistas por % de servicio.
          </p>
          <Link
            to="/configuracion/comisiones"
            className="inline-flex items-center gap-2 mt-5 px-4 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13px] font-medium"
          >
            Ir a Configuración → Comisiones
          </Link>
        </div>
      </div>
    )
  }

  return <CommissionsBody tenantName={user?.tenantName ?? 'Salón'} />
}

// ───────────────────────────────────────────────────────────────────────

function CommissionsBody({ tenantName }: { tenantName: string }) {
  const [period, setPeriod] = useState<Period>(() => currentMonth())
  const [payoutFor, setPayoutFor] = useState<StylistCommissionRow | null>(null)

  const summaryQ = useQuery({
    queryKey: ['commissions', 'summary', period.from, period.to],
    queryFn: () => getCommissionsSummary(period.from, period.to),
  })

  const payoutsQ = useQuery({
    queryKey: ['commissions', 'payouts', period.from, period.to],
    queryFn: () => listCommissionPayouts(period.from, period.to),
  })

  return (
    <div className="flex-1 min-w-0 bg-cream">
      {/* Topbar */}
      <header className="h-16 border-b border-warm-150 bg-cream/80 backdrop-blur sticky top-0 z-30 flex items-center px-6 lg:px-8">
        <div className="text-[12.5px] text-warm-500">
          <span className="text-warm-700">{tenantName}</span> · Comisiones
        </div>
      </header>

      {/* Header */}
      <div className="px-6 lg:px-8 pt-8 pb-5">
        <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Liquidación de equipo
        </div>
        <h1 className="font-serif text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
          Comisiones
        </h1>
        <p className="text-[13.5px] text-warm-500 mt-1.5">
          Cuánto le toca a cada estilista del período según el % de cada servicio.
        </p>

        {/* Period selector */}
        <div className="mt-6 flex items-center gap-2 flex-wrap">
          {PRESETS.map(p => {
            const active = p.from === period.from && p.to === period.to
            return (
              <button
                key={p.label}
                type="button"
                onClick={() => setPeriod({ from: p.from, to: p.to })}
                className={cls(
                  'px-3 py-1.5 rounded-lg text-[12.5px] font-medium border transition',
                  active
                    ? 'bg-warm-800 text-white border-warm-800'
                    : 'bg-white text-warm-600 border-warm-200 hover:border-warm-300',
                )}
              >
                {p.label}
              </button>
            )
          })}
          <div className="flex items-center gap-1.5 ml-2">
            <input
              type="date"
              value={period.from}
              max={period.to}
              onChange={(e) => setPeriod({ ...period, from: e.target.value })}
              className="px-2.5 py-1.5 rounded-lg border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums"
            />
            <span className="text-warm-400 text-[12px]">a</span>
            <input
              type="date"
              value={period.to}
              min={period.from}
              onChange={(e) => setPeriod({ ...period, to: e.target.value })}
              className="px-2.5 py-1.5 rounded-lg border border-warm-200 bg-white text-[12.5px] text-warm-800 tabular-nums"
            />
          </div>
        </div>

        {/* Totales del período */}
        <div className="mt-6 grid sm:grid-cols-3 gap-3">
          <Kpi label="Comisión generada"  value={fmtCop(summaryQ.data?.totalEarned ?? 0)} accent="warm" />
          <Kpi label="Ya liquidado"       value={fmtCop(summaryQ.data?.totalPaid ?? 0)}   accent="brand" />
          <Kpi label="Pendiente de pagar" value={fmtCop(summaryQ.data?.totalPending ?? 0)} accent="gold" />
        </div>
      </div>

      {/* Tabla por estilista */}
      <div className="px-6 lg:px-8 pb-6">
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden">
          <div className="px-5 py-3.5 border-b border-warm-150 flex items-center justify-between">
            <h3 className="font-serif text-[18px] text-warm-800">Por estilista</h3>
            <span className="text-[11.5px] text-warm-500">
              {summaryQ.data?.stylists.length ?? 0} estilistas
            </span>
          </div>

          {summaryQ.isLoading ? (
            <div className="px-5 py-10 text-center text-[13px] text-warm-500">Cargando…</div>
          ) : summaryQ.error ? (
            <div className="px-5 py-10 text-center text-[13px] text-terra-500">
              No se pudo cargar el resumen.
            </div>
          ) : (summaryQ.data?.stylists.length ?? 0) === 0 ? (
            <div className="px-5 py-10 text-center text-[13px] text-warm-500">
              Ningún estilista tiene movimientos en este período.
            </div>
          ) : (
            <table className="w-full text-[13px]">
              <thead>
                <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                  <th className="text-left font-medium px-5 py-3">Estilista</th>
                  <th className="text-right font-medium px-5 py-3 hidden sm:table-cell">Pagos</th>
                  <th className="text-right font-medium px-5 py-3">Cobrado</th>
                  <th className="text-right font-medium px-5 py-3">Comisión</th>
                  <th className="text-right font-medium px-5 py-3 hidden md:table-cell">Ya pagado</th>
                  <th className="text-right font-medium px-5 py-3">Pendiente</th>
                  <th className="px-5 py-3 w-32" />
                </tr>
              </thead>
              <tbody>
                {summaryQ.data!.stylists.map(row => (
                  <tr key={row.stylistId} className="border-b border-warm-100 last:border-0 hover:bg-warm-50/40">
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2.5">
                        <span
                          className="w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-semibold flex-shrink-0"
                          style={{
                            backgroundColor: row.stylistColor ? `${row.stylistColor}33` : '#ece7df',
                            color: row.stylistColor ?? '#46423a',
                          }}
                        >
                          {row.stylistName.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase()}
                        </span>
                        <span className="font-medium text-warm-800 truncate">{row.stylistName}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-right text-warm-500 tabular-nums hidden sm:table-cell">
                      {row.paymentsCount}
                    </td>
                    <td className="px-5 py-3.5 text-right text-warm-700 tabular-nums">
                      {fmtCop(row.cobradoTotal)}
                    </td>
                    <td className="px-5 py-3.5 text-right text-warm-800 tabular-nums font-medium">
                      {fmtCop(row.commissionEarned)}
                    </td>
                    <td className="px-5 py-3.5 text-right text-brand-700 tabular-nums hidden md:table-cell">
                      {row.alreadyPaidInRange > 0 ? fmtCop(row.alreadyPaidInRange) : '—'}
                    </td>
                    <td className="px-5 py-3.5 text-right tabular-nums">
                      {row.pending > 0 ? (
                        <span className="text-gold-600 font-semibold">{fmtCop(row.pending)}</span>
                      ) : (
                        <span className="text-brand-700 inline-flex items-center gap-1 text-[12px]">
                          <CheckCircle2 size={12} /> al día
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-3.5 text-right">
                      {row.pending > 0 && (
                        <button
                          type="button"
                          onClick={() => setPayoutFor(row)}
                          className="px-3 py-1.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12px] font-medium inline-flex items-center gap-1.5"
                        >
                          <Wallet size={12} /> Liquidar
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Historial */}
        <div className="bg-white rounded-2xl border border-warm-150 shadow-soft overflow-hidden mt-5">
          <div className="px-5 py-3.5 border-b border-warm-150 flex items-center justify-between">
            <h3 className="font-serif text-[18px] text-warm-800">Historial de liquidaciones</h3>
            <span className="text-[11.5px] text-warm-500">
              {payoutsQ.data?.length ?? 0} pagos
            </span>
          </div>
          {(payoutsQ.data?.length ?? 0) === 0 ? (
            <div className="px-5 py-8 text-center text-[12.5px] text-warm-500">
              Aún no liquidaste comisiones en este período.
            </div>
          ) : (
            <table className="w-full text-[13px]">
              <thead>
                <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                  <th className="text-left font-medium px-5 py-3">Fecha</th>
                  <th className="text-left font-medium px-5 py-3">Estilista</th>
                  <th className="text-left font-medium px-5 py-3 hidden md:table-cell">Período</th>
                  <th className="text-right font-medium px-5 py-3">Monto</th>
                  <th className="text-left font-medium px-5 py-3 hidden lg:table-cell">Nota</th>
                </tr>
              </thead>
              <tbody>
                {payoutsQ.data!.map((p: CommissionPayout) => (
                  <tr key={p.id} className="border-b border-warm-100 last:border-0">
                    <td className="px-5 py-3 text-warm-600 tabular-nums">{formatDate(p.paidAt)}</td>
                    <td className="px-5 py-3 text-warm-800">{p.stylistName}</td>
                    <td className="px-5 py-3 text-warm-500 hidden md:table-cell tabular-nums">
                      {p.periodFrom} → {p.periodTo}
                    </td>
                    <td className="px-5 py-3 text-right font-medium text-warm-800 tabular-nums">
                      {fmtCop(p.amount)}
                    </td>
                    <td className="px-5 py-3 text-warm-500 italic hidden lg:table-cell truncate max-w-[300px]">
                      {p.notes ?? '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {payoutFor && (
        <PayoutModal
          stylist={payoutFor}
          period={period}
          onClose={() => setPayoutFor(null)}
        />
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal "Liquidar"
// ───────────────────────────────────────────────────────────────────────

function PayoutModal({
  stylist, period, onClose,
}: {
  stylist: StylistCommissionRow
  period: Period
  onClose: () => void
}) {
  const qc = useQueryClient()
  const [amount, setAmount] = useState(String(stylist.pending))
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: createCommissionPayout,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['commissions'] })
      onClose()
    },
    onError: (e) => setError(extractApiError(e, 'No se pudo registrar.')),
  })

  const amountNum = parseInt(amount.replace(/[^0-9]/g, '')) || 0
  const exceeds = amountNum > stylist.pending
  const canSubmit = amountNum > 0 && !exceeds && !mut.isPending

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-8" onClick={onClose}>
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm anim-fade" />
      <div className="relative w-full max-w-md bg-white rounded-2xl shadow-pop overflow-hidden anim-fade" onClick={e => e.stopPropagation()}>
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium">
              Liquidar comisión
            </div>
            <h3 className="font-serif text-[24px] text-warm-800 mt-1">{stylist.stylistName}</h3>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center" aria-label="Cerrar">
            <X size={18} />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4">
          <div className="rounded-xl bg-warm-50 border border-warm-150 p-4 text-[12.5px] space-y-1.5">
            <div className="flex justify-between text-warm-600">
              <span>Comisión del período</span>
              <span className="tabular-nums">{fmtCop(stylist.commissionEarned)}</span>
            </div>
            <div className="flex justify-between text-brand-700">
              <span>− Ya pagado</span>
              <span className="tabular-nums">{fmtCop(stylist.alreadyPaidInRange)}</span>
            </div>
            <div className="flex justify-between font-semibold text-warm-800 pt-1.5 border-t border-warm-200/60">
              <span>Pendiente</span>
              <span className="tabular-nums">{fmtCop(stylist.pending)}</span>
            </div>
          </div>

          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">
              Monto a liquidar
              <span className="ml-1 normal-case text-warm-400">(máx {fmtCop(stylist.pending)})</span>
            </label>
            <div className="relative">
              <span className="absolute left-3.5 top-1/2 -translate-y-1/2 text-warm-400 text-[15px]">$</span>
              <input
                value={amount}
                onChange={e => setAmount(e.target.value)}
                inputMode="numeric"
                autoFocus
                className="w-full pl-7 pr-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[15px] text-warm-800 tabular-nums focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none"
              />
            </div>
            {exceeds && (
              <p className="mt-1 text-[11.5px] text-terra-500">No puede ser mayor a {fmtCop(stylist.pending)}.</p>
            )}
          </div>

          <div>
            <label className="text-[12.5px] font-medium text-warm-700 block mb-1.5">Nota (opcional)</label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              placeholder="Ej: Pagado en efectivo. Ref TRF-238."
              className="w-full px-3 py-2 rounded-lg bg-white border border-warm-200 text-[13px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none resize-none"
            />
          </div>

          <p className="text-[11.5px] text-warm-500">
            Se registra una liquidación cubriendo el período <strong>{period.from}</strong> a <strong>{period.to}</strong>.
          </p>

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
        </div>

        <div className="px-6 py-4 bg-warm-50 border-t border-warm-150 flex items-center justify-end gap-2">
          <button onClick={onClose} className="px-4 py-2.5 rounded-lg text-[13px] text-warm-700 hover:bg-warm-150">Cancelar</button>
          <button
            type="button"
            onClick={() => mut.mutate({
              stylistId: stylist.stylistId,
              amount: amountNum,
              periodFrom: period.from,
              periodTo: period.to,
              notes: notes.trim() || null,
            })}
            disabled={!canSubmit}
            className={cls(
              'px-5 py-2.5 rounded-lg text-[13px] font-medium flex items-center gap-2 transition',
              canSubmit ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft' : 'bg-warm-200 text-warm-400 cursor-not-allowed',
            )}
          >
            <Wallet size={14} />
            {mut.isPending ? 'Registrando…' : 'Marcar pagada'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Helpers
// ───────────────────────────────────────────────────────────────────────

function Kpi({ label, value, accent }: { label: string; value: string; accent: 'warm' | 'brand' | 'gold' }) {
  const accents = {
    warm:  'text-warm-800',
    brand: 'text-brand-700',
    gold:  'text-gold-600',
  } as const
  return (
    <div className="bg-white border border-warm-150 rounded-2xl p-4 shadow-softer">
      <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">{label}</div>
      <div className={cls('font-serif text-[28px] leading-none tabular-nums mt-2', accents[accent])}>{value}</div>
    </div>
  )
}

interface Period { from: string; to: string }

function currentMonth(): Period {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth(), 1)
  return { from: ymd(first), to: ymd(now) }
}

function fortnightCurrent(): Period {
  const now = new Date()
  const day = now.getDate()
  const start = day <= 15
    ? new Date(now.getFullYear(), now.getMonth(), 1)
    : new Date(now.getFullYear(), now.getMonth(), 16)
  return { from: ymd(start), to: ymd(now) }
}

function previousMonth(): Period {
  const now = new Date()
  const first = new Date(now.getFullYear(), now.getMonth() - 1, 1)
  const last = new Date(now.getFullYear(), now.getMonth(), 0)
  return { from: ymd(first), to: ymd(last) }
}

const PRESETS: Array<{ label: string; from: string; to: string }> = (() => {
  const m = currentMonth()
  const f = fortnightCurrent()
  const p = previousMonth()
  return [
    { label: 'Quincena actual', from: f.from, to: f.to },
    { label: 'Este mes',        from: m.from, to: m.to },
    { label: 'Mes pasado',      from: p.from, to: p.to },
  ]
})()

function ymd(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`
}
