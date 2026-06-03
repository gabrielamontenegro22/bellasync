import { useRef, useState } from 'react'
import { Banknote, CheckCircle, CreditCard, Smartphone } from 'lucide-react'
import { Button, Card, Input } from '@/components/ui'
import type { AppointmentResponse } from '@/api/appointments'
import type { PaymentMethod } from '@/api/payments'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import { useRegisterPayment } from '../hooks'

interface RegisterPaymentModalProps {
  appointment: AppointmentResponse
  onClose: () => void
}

/**
 * Modal "Cobrar" — registra un pago recibido por una cita atendida.
 *
 * Flujo: recepcionista termina de atender a la cliente → click en Cobrar
 * en el panel detalle → elige método (Efectivo / Bancolombia / Nequi /
 * Daviplata / tarjeta) → monto pre-rellenado con el precio del servicio
 * → opcional propina + referencia → registra.
 *
 * NO procesa pagos reales. El dinero entra por fuera (efectivo, app
 * bancaria, datáfono); esto solo deja el registro contable.
 */
export function RegisterPaymentModal({ appointment, onClose }: RegisterPaymentModalProps) {
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  // Monto pre-rellenado con priceSnapshot — caso típico (cliente paga
  // exactamente el precio). Si hay anticipo previo o descuento, la
  // recepcionista lo edita manualmente.
  const [amount, setAmount] = useState(appointment.priceSnapshot)
  const [tip, setTip] = useState(0)
  const [reference, setReference] = useState('')
  const [submitError, setSubmitError] = useState<string | null>(null)
  // Pantalla de "éxito" que reemplaza al form cuando el pago se guardó.
  // Guarda el monto total para el mensaje "$X registrado".
  const [success, setSuccess] = useState<number | null>(null)

  // Ref-based lock: previene doble-disparo aunque el usuario haga
  // doble-click ultrarrápido (antes de que React re-renderice el
  // botón disabled).
  const submittingRef = useRef(false)

  const register = useRegisterPayment()

  // Para métodos digitales la referencia es fuertemente recomendada
  // (sin ella es imposible reconciliar con el extracto bancario).
  const needsReference = method !== 'Cash'

  async function submit() {
    // Hard-lock: si ya estamos disparando una mutation, segundos clicks
    // se ignoran. El disabled del botón también lo previene, pero esto
    // es una segunda red de seguridad por si React no alcanza a pintar.
    if (submittingRef.current) return
    submittingRef.current = true
    setSubmitError(null)
    try {
      const result = await register.mutateAsync({
        appointmentId: appointment.id,
        req: {
          method,
          amount: Number(amount),
          tip: Number(tip),
          reference: reference.trim() || null,
        },
      })
      // Mostrar pantalla de éxito en vez de cerrar de inmediato — el
      // usuario debe ver "✓ Pago registrado" antes de que desaparezca
      // la ventana, sino vuelve a apretar pensando que no se guardó.
      setSuccess(result.total)
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo registrar el pago.'))
      submittingRef.current = false  // permitir reintentar si falló
    }
  }

  const canSubmit = amount > 0 && !submittingRef.current

  // Pantalla de éxito tras un registro exitoso. El usuario ve "✓ Pago
  // registrado por $X" y cierra él manualmente (o automáticamente tras
  // unos segundos si queremos). Esto soluciona el problema de "no sé si
  // se guardó → vuelvo a apretar → pago doble".
  if (success !== null) {
    return (
      <div
        className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4"
        onClick={onClose}
      >
        <Card className="w-full max-w-md space-y-4 p-6 text-center" onClick={e => e.stopPropagation()}>
          <div className="w-14 h-14 mx-auto rounded-full bg-brand-50 border border-brand-100 flex items-center justify-center text-brand-700">
            <CheckCircle size={28} />
          </div>
          <div>
            <h2 className="font-serif text-2xl text-warm-800">Pago registrado</h2>
            <div className="text-[14px] text-warm-600 mt-1">
              Quedó anotado <strong className="text-warm-800 tabular-nums">
                ${success.toLocaleString('es-CO')}
              </strong> para <strong className="text-warm-800">{appointment.customerName}</strong>.
            </div>
          </div>
          <Button onClick={onClose} fullWidth>Listo</Button>
        </Card>
      </div>
    )
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4"
      onClick={onClose}
    >
      <Card className="w-full max-w-md space-y-4 p-5" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between">
          <h2 className="font-serif text-xl text-brand-700">Registrar pago</h2>
          <button
            type="button"
            onClick={onClose}
            className="text-warm-400 hover:text-warm-600"
            aria-label="Cerrar"
          >
            ✕
          </button>
        </div>

        {/* Resumen de la cita — para confirmar que registramos para la cita correcta */}
        <div className="rounded-lg bg-warm-50 border border-warm-150 p-3 text-sm">
          <div className="font-medium text-warm-800">{appointment.customerName}</div>
          <div className="text-warm-600 mt-0.5">
            {appointment.serviceName} · {appointment.stylistName}
          </div>
          <div className="text-warm-500 mt-0.5 text-xs tabular-nums">
            Precio del servicio: ${appointment.priceSnapshot.toLocaleString('es-CO')}
          </div>
        </div>

        {/* Método de pago — pills coloreadas */}
        <div>
          <label className="mb-2 block text-xs uppercase tracking-wide text-warm-500">
            Método de pago
          </label>
          <div className="grid grid-cols-3 gap-2">
            {METHOD_OPTIONS.map(opt => {
              const selected = method === opt.value
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setMethod(opt.value)}
                  className={cls(
                    'flex flex-col items-center gap-1 px-2 py-2.5 rounded-lg border text-[12px] font-medium transition',
                    selected
                      ? `${opt.activeBg} ${opt.activeFg} ${opt.activeBorder} ring-2 ring-offset-1 ${opt.activeRing}`
                      : 'bg-white border-warm-200 text-warm-600 hover:border-warm-300',
                  )}
                >
                  <opt.icon size={16} />
                  {opt.label}
                </button>
              )
            })}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
              Monto
            </label>
            <Input
              type="number"
              min={0}
              step={1000}
              value={amount}
              onChange={e => setAmount(Number(e.target.value))}
            />
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
              Propina (opcional)
            </label>
            <Input
              type="number"
              min={0}
              step={1000}
              value={tip}
              onChange={e => setTip(Number(e.target.value))}
            />
          </div>
        </div>

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
            Referencia {needsReference && <span className="text-terra-500 normal-case lowercase">(recomendado)</span>}
          </label>
          <Input
            value={reference}
            onChange={e => setReference(e.target.value)}
            placeholder={
              method === 'Cash' ? '—' :
              method === 'CreditCard' || method === 'DebitCard' ? 'Número de voucher del datáfono' :
              'Número de aprobación de la transferencia'
            }
            disabled={method === 'Cash'}
          />
        </div>

        {/* Total destacado */}
        <div className="flex items-center justify-between rounded-lg bg-brand-50 border border-brand-100 p-3">
          <span className="text-[12px] uppercase tracking-wide text-brand-700 font-medium">Total</span>
          <span className="font-serif text-[22px] text-brand-800 tabular-nums">
            ${(Number(amount) + Number(tip)).toLocaleString('es-CO')}
          </span>
        </div>

        {submitError && (
          <p className="rounded-md bg-terra-100 p-2 text-sm text-terra-500">{submitError}</p>
        )}

        <div className="flex gap-2 pt-1">
          <Button variant="secondary" onClick={onClose} fullWidth>
            Cancelar
          </Button>
          <Button
            fullWidth
            onClick={submit}
            loading={register.isPending}
            disabled={!canSubmit || register.isPending}
          >
            Registrar pago
          </Button>
        </div>
      </Card>
    </div>
  )
}

// ===== Catálogo visual de métodos =====
// Mantener acá (no en customerLook) porque son específicos del módulo Pagos.
// Cada método tiene su par bg/fg para que la pill seleccionada sea distintiva.

interface MethodOption {
  value: PaymentMethod
  label: string
  icon: React.ComponentType<{ size?: number; className?: string }>
  activeBg: string
  activeFg: string
  activeBorder: string
  activeRing: string
}

const METHOD_OPTIONS: MethodOption[] = [
  { value: 'Cash',        label: 'Efectivo',    icon: Banknote,   activeBg: 'bg-brand-50',  activeFg: 'text-brand-800', activeBorder: 'border-brand-200',  activeRing: 'ring-brand-200' },
  { value: 'Bancolombia', label: 'Bancolombia', icon: Smartphone, activeBg: 'bg-[#fef3c4]', activeFg: 'text-[#7d5b14]', activeBorder: 'border-[#e6d5a3]', activeRing: 'ring-[#e6d5a3]' },
  { value: 'Nequi',       label: 'Nequi',       icon: Smartphone, activeBg: 'bg-[#fce4f1]', activeFg: 'text-[#a02670]', activeBorder: 'border-[#f4cce0]', activeRing: 'ring-[#f4cce0]' },
  { value: 'Daviplata',   label: 'Daviplata',   icon: Smartphone, activeBg: 'bg-[#fee2e2]', activeFg: 'text-[#9a2828]', activeBorder: 'border-[#fbb6b6]', activeRing: 'ring-[#fbb6b6]' },
  { value: 'CreditCard',  label: 'T. crédito',  icon: CreditCard, activeBg: 'bg-warm-100',  activeFg: 'text-warm-800',  activeBorder: 'border-warm-300',  activeRing: 'ring-warm-250' },
  { value: 'DebitCard',   label: 'T. débito',   icon: CreditCard, activeBg: 'bg-warm-100',  activeFg: 'text-warm-800',  activeBorder: 'border-warm-300',  activeRing: 'ring-warm-250' },
]

/** Mapeo público para que otros componentes (tabla de pagos del CRM) pinten consistente */
export const METHOD_BADGE: Record<PaymentMethod, { label: string; bg: string; fg: string }> = {
  Cash:        { label: 'Efectivo',     bg: 'bg-brand-50',   fg: 'text-brand-800' },
  Bancolombia: { label: 'Bancolombia',  bg: 'bg-[#fef3c4]',  fg: 'text-[#7d5b14]' },
  Nequi:       { label: 'Nequi',        bg: 'bg-[#fce4f1]',  fg: 'text-[#a02670]' },
  Daviplata:   { label: 'Daviplata',    bg: 'bg-[#fee2e2]',  fg: 'text-[#9a2828]' },
  CreditCard:  { label: 'T. crédito',   bg: 'bg-warm-100',   fg: 'text-warm-700'  },
  DebitCard:   { label: 'T. débito',    bg: 'bg-warm-100',   fg: 'text-warm-700'  },
  Other:       { label: 'Otro',         bg: 'bg-warm-100',   fg: 'text-warm-600'  },
}
