import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle,
  Clock,
  Sparkles,
  X,
  XCircle,
} from 'lucide-react'
import { Modal, ModalFooter } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { extractApiError } from '@/lib/extractApiError'
import {
  changePlan,
  getSubscription,
  reportPayment,
  type InvoiceRow,
  type PlanOption,
  type Subscription,
} from '@/api/subscription'
import { cls } from '@/lib/cls'
import { SettingsHeader } from './_primitives'

/**
 * `/configuracion/suscripcion` — gestiona la suscripción del salón al
 * SaaS BellaSync. Una sola call (GET /api/Subscription) trae todo lo
 * que la pantalla necesita: plan vigente, fechas, próxima factura
 * pendiente, historial e catálogo para el modal "Cambiar plan".
 *
 * Layout (espejo del mockup):
 *   1. Banner trial-ending-soon o past-due cuando aplica.
 *   2. Card dark con plan actual + precio + próximo cobro + botones.
 *   3. Pill de estado.
 *   4. Tabla de historial de pagos.
 *
 * Anti-pasarela: el botón "Pagar ahora" abre un modal donde la admin
 * confirma que hizo la transferencia y deja la referencia. El
 * SaaSAdmin de BellaSync valida después; este flujo confía y aplica.
 */

const fmt = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')
const fmtMonth = (iso: string) => {
  const d = new Date(iso)
  return d.toLocaleDateString('es-CO', { day: 'numeric', month: 'short', year: 'numeric' })
}

export function SuscripcionPage() {
  const qc = useQueryClient()
  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['subscription'],
    queryFn: getSubscription,
    retry: 1,
    // M8 del audit: si hay pago en validación, polleamos cada 30s para
    // que María vea ni bien BellaSync valida (sin tener que refrescar
    // manualmente). Si no, no polleamos — datos casi-estáticos.
    refetchInterval: (q) =>
      q.state.data?.pendingValidationInvoice ? 30_000 : false,
  })

  const [planModalOpen, setPlanModalOpen] = useState(false)
  const [payModalOpen, setPayModalOpen] = useState(false)

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Suscripción y facturación"
          desc="Tu plan actual de BellaSync, el próximo cobro y el historial de pagos."
        />
        <div className="rounded-2xl bg-warm-100 h-44 animate-pulse" />
        <div className="mt-7 rounded-xl bg-warm-100 h-64 animate-pulse" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Suscripción y facturación"
          desc="Tu plan actual de BellaSync, el próximo cobro y el historial de pagos."
        />
        <div className="rounded-xl bg-terra-100 border border-terra-200 px-4 py-4 flex items-start gap-3">
          <AlertTriangle size={18} className="text-terra-700 mt-0.5 flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <div className="text-[13px] text-terra-700 font-medium">
              No pudimos cargar tu suscripción
            </div>
            <div className="text-[12px] text-terra-700/80 mt-1">
              {error ? extractApiError(error) : 'Respuesta vacía del servidor.'}
            </div>
            <Button
              variant="secondary"
              size="sm"
              className="mt-3"
              onClick={() => refetch()}
              loading={isRefetching}
            >
              Reintentar
            </Button>
          </div>
        </div>
      </div>
    )
  }

  const onSubscriptionChange = (next: Subscription) => {
    qc.setQueryData(['subscription'], next)
  }

  return (
    <div className="px-6 lg:px-10 py-8 max-w-3xl">
      <SettingsHeader
        eyebrow="Ajustes del salón"
        title="Suscripción y facturación"
        desc="Tu plan actual de BellaSync, el próximo cobro y el historial de pagos de tu salón."
      />

      <StatusBanner sub={data} />

      <PlanCard
        sub={data}
        onChangePlanClick={() => setPlanModalOpen(true)}
        onPayClick={() => setPayModalOpen(true)}
      />

      <StatusPill sub={data} />

      <PaymentHistory invoices={data.invoices} />

      {planModalOpen && (
        <ChangePlanModal
          sub={data}
          onClose={() => setPlanModalOpen(false)}
          onChanged={(next) => {
            onSubscriptionChange(next)
            setPlanModalOpen(false)
          }}
        />
      )}

      {payModalOpen && (
        <ReportPaymentModal
          sub={data}
          onClose={() => setPayModalOpen(false)}
          onReported={(next) => {
            onSubscriptionChange(next)
            setPayModalOpen(false)
          }}
        />
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Banner de alerta (trial-ending-soon / past-due)
// ───────────────────────────────────────────────────────────────────────

function StatusBanner({ sub }: { sub: Subscription }) {
  // Prioridad 1: pago en validación (lo más informativo y reciente).
  if (sub.pendingValidationInvoice) {
    const inv = sub.pendingValidationInvoice
    return (
      <div className="mb-5 rounded-xl bg-gold-50 border border-gold-200 px-4 py-3 flex items-start gap-2.5">
        <Clock size={16} className="text-gold-600 mt-0.5 flex-shrink-0" />
        <div className="text-[12.5px] text-gold-700 leading-relaxed">
          <strong>Tu pago está en validación.</strong> Reportaste {fmt(inv.amount)} vía{' '}
          {inv.reportedMethod}{inv.reportedReference ? ` (ref ${inv.reportedReference})` : ''}.
          BellaSync lo verifica en el banco — suele tardar 1-2 días hábiles.
        </div>
      </div>
    )
  }
  // Prioridad 2: último rechazo (para que sepa que su reporte fue inválido).
  if (sub.lastRejectionReason) {
    return (
      <div className="mb-5 rounded-xl bg-terra-100 border border-terra-200 px-4 py-3 flex items-start gap-2.5">
        <XCircle size={16} className="text-terra-700 mt-0.5 flex-shrink-0" />
        <div className="text-[12.5px] text-terra-700 leading-relaxed">
          <strong>Tu reporte de pago fue rechazado.</strong> Motivo: {sub.lastRejectionReason}.
          Revisa los datos y vuelve a reportar.
        </div>
      </div>
    )
  }
  // Prioridad 3: past-due.
  if (sub.status === 'PastDue') {
    return (
      <div className="mb-5 rounded-xl bg-terra-100 border border-terra-200 px-4 py-3 flex items-start gap-2.5">
        <AlertTriangle size={16} className="text-terra-700 mt-0.5 flex-shrink-0" />
        <div className="text-[12.5px] text-terra-700 leading-relaxed">
          <strong>Tu pago está pendiente.</strong> Tu suscripción venció el{' '}
          {fmtMonth(sub.currentPeriodEnd)}. Regulariza para no perder acceso a las
          features avanzadas.
        </div>
      </div>
    )
  }
  if (sub.trialEndingSoon) {
    const days = Math.max(0, sub.daysUntilNextCharge)
    return (
      <div className="mb-5 rounded-xl bg-gold-50 border border-gold-200 px-4 py-3 flex items-start gap-2.5">
        <Sparkles size={16} className="text-gold-600 mt-0.5 flex-shrink-0" />
        <div className="text-[12.5px] text-gold-700 leading-relaxed">
          <strong>Tu período de prueba termina en {days} día{days === 1 ? '' : 's'}.</strong>{' '}
          Reporta tu transferencia para no perder acceso.
        </div>
      </div>
    )
  }
  return null
}

// ───────────────────────────────────────────────────────────────────────
// Card oscura con plan actual + acciones
// ───────────────────────────────────────────────────────────────────────

function PlanCard({
  sub,
  onChangePlanClick,
  onPayClick,
}: {
  sub: Subscription
  onChangePlanClick: () => void
  onPayClick: () => void
}) {
  // Label inteligente del botón principal según el estado:
  //   Pago en validación → "Esperando validación" (disabled)
  //   Trial              → "Reportar pago"
  //   PastDue            → "Reportar pago"
  //   Active + factura   → "Reportar pago"
  //   Active sin factura → "Renovar suscripción"
  //   Cancelled          → null
  const isPendingValidation = !!sub.pendingValidationInvoice
  const payAction = (() => {
    if (sub.status === 'Cancelled') return null
    if (isPendingValidation) return 'Esperando validación'
    if (sub.status === 'Active' && !sub.nextDueInvoice) return 'Renovar suscripción'
    return 'Reportar pago'
  })()

  return (
    <div className="rounded-2xl bg-warm-800 text-white p-6 relative overflow-hidden">
      <div className="absolute -right-16 -top-16 w-48 h-48 rounded-full bg-brand-700/30 blur-2xl" />
      <div className="relative flex items-start justify-between flex-wrap gap-4">
        <div>
          <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-300 font-medium">
            Plan actual
          </div>
          <div className="font-serif text-[32px] leading-tight mt-1">{sub.planName}</div>
          {sub.planTagline && (
            <div className="text-[12.5px] text-warm-300 mt-1">{sub.planTagline}</div>
          )}
        </div>
        <div className="text-right">
          <div className="font-serif text-[34px] tabular-nums leading-none">
            {fmt(sub.monthlyPrice)}
          </div>
          <div className="text-[11.5px] text-warm-300 mt-1">/ mes</div>
        </div>
      </div>
      <div className="relative mt-5 pt-4 border-t border-white/15 flex items-center justify-between flex-wrap gap-3">
        <div className="text-[12.5px] text-warm-200 flex items-center gap-2">
          <CalendarClock size={14} />
          {sub.status === 'Trial' ? 'Trial termina' : 'Próximo cobro'}:{' '}
          <span className="text-white font-medium">{fmtMonth(sub.currentPeriodEnd)}</span>
          {sub.daysUntilNextCharge >= 0 && (
            <span className="text-warm-300">
              ({sub.daysUntilNextCharge} día{sub.daysUntilNextCharge === 1 ? '' : 's'})
            </span>
          )}
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onChangePlanClick}
            disabled={sub.status === 'Cancelled' || isPendingValidation}
            title={isPendingValidation
              ? 'Espera la validación de tu pago anterior antes de cambiar de plan'
              : undefined}
            className="px-3.5 py-2 rounded-lg bg-white/10 hover:bg-white/15 text-white text-[12.5px] font-medium disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Cambiar plan
          </button>
          {payAction && (
            <button
              type="button"
              onClick={onPayClick}
              disabled={isPendingValidation}
              className="px-3.5 py-2 rounded-lg bg-gold-300 hover:bg-gold-200 text-warm-800 text-[12.5px] font-medium disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {payAction}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Pill de estado: distinto color según Status
// ───────────────────────────────────────────────────────────────────────

function StatusPill({ sub }: { sub: Subscription }) {
  if (sub.status === 'Active') {
    return (
      <div className="mt-5 rounded-xl bg-brand-50 border border-brand-200 px-4 py-3 flex items-center gap-2.5">
        <CheckCircle size={16} className="text-brand-700" />
        <span className="text-[12.5px] text-brand-800">
          Tu cuenta está al día. Gracias por confiar en BellaSync 💛
        </span>
      </div>
    )
  }
  if (sub.status === 'Trial') {
    return (
      <div className="mt-5 rounded-xl bg-warm-50 border border-warm-150 px-4 py-3 flex items-center gap-2.5">
        <Sparkles size={16} className="text-gold-600" />
        <span className="text-[12.5px] text-warm-700">
          Estás en período de prueba — acceso completo, sin cargo.
        </span>
      </div>
    )
  }
  if (sub.status === 'PastDue') {
    return null  // ya tiene su banner arriba
  }
  // Cancelled
  return (
    <div className="mt-5 rounded-xl bg-warm-100 border border-warm-200 px-4 py-3 flex items-center gap-2.5">
      <X size={16} className="text-warm-500" />
      <span className="text-[12.5px] text-warm-600">
        Tu suscripción fue cancelada{sub.cancelledAt ? ` el ${fmtMonth(sub.cancelledAt)}` : ''}.
      </span>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Tabla de historial
// ───────────────────────────────────────────────────────────────────────

function PaymentHistory({ invoices }: { invoices: InvoiceRow[] }) {
  return (
    <div className="mt-7">
      <div className="text-[12.5px] font-semibold text-warm-800 mb-3">
        Historial de pagos
      </div>
      <div className="rounded-xl border border-warm-150 bg-white overflow-hidden">
        {invoices.length === 0 ? (
          <div className="px-4 py-10 text-center text-[12.5px] text-warm-500">
            Todavía no hay facturas emitidas.
          </div>
        ) : (
          <table className="w-full text-[13px]">
            <thead>
              <tr className="bg-warm-50 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                <th className="text-left font-medium px-4 py-2.5">Fecha</th>
                <th className="text-left font-medium px-4 py-2.5 hidden sm:table-cell">Plan</th>
                <th className="text-left font-medium px-4 py-2.5 hidden sm:table-cell">Método</th>
                <th className="text-right font-medium px-4 py-2.5">Monto</th>
                <th className="text-right font-medium px-4 py-2.5">Estado</th>
              </tr>
            </thead>
            <tbody>
              {invoices.map((inv) => (
                <tr key={inv.id} className="border-b border-warm-100 last:border-0">
                  <td className="px-4 py-3 text-warm-800">
                    {fmtMonth(inv.paidAt ?? inv.issuedAt)}
                  </td>
                  <td className="px-4 py-3 text-warm-600 hidden sm:table-cell">{inv.planName}</td>
                  <td className="px-4 py-3 text-warm-600 hidden sm:table-cell">
                    {inv.paymentMethod ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-warm-800 font-medium">
                    {fmt(inv.amount)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <StatusBadge status={inv.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; cls: string }> = {
    Paid:     { label: 'Pagado',     cls: 'text-brand-700 bg-brand-50' },
    Pending:  { label: 'Pendiente',  cls: 'text-gold-700 bg-gold-50' },
    Reported: { label: 'En validación', cls: 'text-gold-700 bg-gold-100' },
    Failed:   { label: 'Fallido',    cls: 'text-terra-700 bg-terra-100' },
    Waived:   { label: 'Cortesía',   cls: 'text-warm-600 bg-warm-100' },
  }
  const v = map[status] ?? { label: status, cls: 'text-warm-600 bg-warm-100' }
  return (
    <span className={cls(
      'text-[10.5px] tracking-[0.1em] uppercase font-semibold px-2 py-0.5 rounded-md',
      v.cls,
    )}>
      {v.label}
    </span>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal: Cambiar plan
// ───────────────────────────────────────────────────────────────────────

function ChangePlanModal({
  sub,
  onClose,
  onChanged,
}: {
  sub: Subscription
  onClose: () => void
  onChanged: (next: Subscription) => void
}) {
  const [selected, setSelected] = useState<string>(sub.planCode)
  const [err, setErr] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: (code: string) => changePlan(code),
    onSuccess: (next) => onChanged(next),
    onError: (e) => setErr(extractApiError(e)),
  })

  const save = () => {
    setErr(null)
    if (selected === sub.planCode) {
      onClose()
      return
    }
    mut.mutate(selected)
  }

  return (
    <Modal title="Cambiar plan" onClose={onClose} size="lg">
      <p className="text-[13px] text-warm-600 mb-4">
        Elige un plan. El cambio aplica al próximo cobro: las facturas ya
        emitidas conservan su monto histórico.
      </p>
      <div className="grid gap-3 sm:grid-cols-1">
        {sub.availablePlans.map((p) => (
          <PlanRow
            key={p.code}
            plan={p}
            selected={selected === p.code}
            onSelect={() => setSelected(p.code)}
          />
        ))}
      </div>
      <ModalFooter error={err}>
        <Button variant="secondary" onClick={onClose} fullWidth disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button onClick={save} fullWidth loading={mut.isPending}>
          {selected === sub.planCode ? 'Sin cambios' : 'Confirmar cambio'}
        </Button>
      </ModalFooter>
    </Modal>
  )
}

function PlanRow({
  plan,
  selected,
  onSelect,
}: {
  plan: PlanOption
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cls(
        'text-left rounded-xl p-4 transition border-2',
        selected
          ? 'border-brand-700 bg-brand-50/40'
          : 'border-warm-200 bg-white hover:border-warm-300',
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <div className="font-serif text-[18px] text-warm-800">{plan.name}</div>
            {plan.isCurrent && (
              <span className="text-[9.5px] tracking-[0.1em] uppercase font-semibold text-brand-700 bg-brand-100 px-1.5 py-0.5 rounded">
                Actual
              </span>
            )}
            {plan.isHighlighted && (
              <span className="text-[9.5px] tracking-[0.1em] uppercase font-semibold text-gold-700 bg-gold-100 px-1.5 py-0.5 rounded">
                Más popular
              </span>
            )}
          </div>
          {plan.tagline && (
            <div className="text-[12px] text-warm-500 mt-0.5">{plan.tagline}</div>
          )}
        </div>
        <div className="text-right flex-shrink-0">
          <div className="font-serif text-[20px] tabular-nums text-warm-800 leading-none">
            {fmt(plan.monthlyPrice)}
          </div>
          <div className="text-[11px] text-warm-500 mt-1">/ mes</div>
        </div>
      </div>
      <ul className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-x-3 gap-y-1.5">
        {plan.features.map((f) => (
          <li key={f} className="flex items-start gap-1.5 text-[12px] text-warm-700">
            <CheckCircle size={12} className="text-brand-600 mt-0.5 flex-shrink-0" />
            <span>{f}</span>
          </li>
        ))}
      </ul>
    </button>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Modal: Pagar factura pendiente
// ───────────────────────────────────────────────────────────────────────

const PAY_METHODS = ['Bancolombia', 'Nequi', 'Daviplata', 'Davivienda', 'BBVA', 'Otro']

function ReportPaymentModal({
  sub,
  onClose,
  onReported,
}: {
  sub: Subscription
  onClose: () => void
  onReported: (next: Subscription) => void
}) {
  const [method, setMethod] = useState<string>('Bancolombia')
  const [reference, setReference] = useState('')
  const [err, setErr] = useState<string | null>(null)

  // Si hay factura pendiente, usamos su monto/plan. Si no, el monto será
  // el del plan vigente (cuando el backend la emita en el momento).
  const invoice = sub.nextDueInvoice
  const planName = invoice?.planName ?? sub.planName
  const amount = invoice?.amount ?? sub.monthlyPrice
  const periodLabel = invoice
    ? `${fmtMonth(invoice.periodStart)} – ${fmtMonth(invoice.periodEnd)}`
    : 'Próximo mes (se emite al confirmar)'

  const mut = useMutation({
    mutationFn: () =>
      reportPayment({
        paymentMethod: method,
        reference: reference.trim() || null,
      }),
    onSuccess: (next) => onReported(next),
    onError: (e) => setErr(extractApiError(e)),
  })

  return (
    <Modal title="Reportar transferencia" onClose={onClose} size="md">
      <p className="text-[13px] text-warm-600 mb-4">
        Reporta acá los datos de tu transferencia. BellaSync verificará
        contra el banco y activará tu plan apenas confirme. Suele tardar
        1-2 días hábiles.
      </p>
      <div className="rounded-xl bg-warm-50 border border-warm-150 p-4 mb-4">
        <div className="flex items-center justify-between text-[12.5px]">
          <span className="text-warm-500">Plan</span>
          <span className="text-warm-800 font-medium">{planName}</span>
        </div>
        <div className="flex items-center justify-between text-[12.5px] mt-1.5">
          <span className="text-warm-500">Período</span>
          <span className="text-warm-700">{periodLabel}</span>
        </div>
        <div className="flex items-center justify-between mt-3 pt-3 border-t border-warm-150">
          <span className="text-[13px] text-warm-800 font-medium">Monto a pagar</span>
          <span className="font-serif text-[22px] tabular-nums text-warm-800">
            {fmt(amount)}
          </span>
        </div>
      </div>

      <div className="space-y-3">
        <div>
          <label className="text-[12.5px] font-medium text-warm-700 mb-1.5 block">
            Método de pago
          </label>
          <div className="flex flex-wrap gap-2">
            {PAY_METHODS.map((m) => (
              <button
                key={m}
                type="button"
                onClick={() => setMethod(m)}
                className={cls(
                  'px-3 py-1.5 rounded-lg text-[12.5px] transition border',
                  method === m
                    ? 'bg-brand-700 text-white border-brand-700'
                    : 'bg-white text-warm-700 border-warm-200 hover:border-warm-300',
                )}
              >
                {m}
              </button>
            ))}
          </div>
        </div>
        <div>
          <label className="text-[12.5px] font-medium text-warm-700 mb-1.5 block">
            Referencia <span className="text-warm-400">(opcional)</span>
          </label>
          <input
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            placeholder="Ej: BCO-209384 — comprobante del banco"
            className="w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition"
          />
        </div>
      </div>

      <ModalFooter error={err}>
        <Button variant="secondary" onClick={onClose} fullWidth disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={() => { setErr(null); mut.mutate() }}
          fullWidth
          loading={mut.isPending}
          leftIcon={<CheckCircle size={14} />}
        >
          Reportar transferencia
        </Button>
      </ModalFooter>
    </Modal>
  )
}
