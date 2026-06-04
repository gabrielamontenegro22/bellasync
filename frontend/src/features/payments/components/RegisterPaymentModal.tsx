import { useRef, useState } from 'react'
import { CheckCircle } from 'lucide-react'
import { Button, Card, Input } from '@/components/ui'
import type { AppointmentResponse } from '@/api/appointments'
import type { PaymentMethod } from '@/api/payments'
import { extractApiError } from '@/lib/extractApiError'
import { useRegisterPayment } from '../hooks'
import { PaymentMethodPicker } from './PaymentMethodPicker'

interface RegisterPaymentModalProps {
  appointment: AppointmentResponse
  /**
   * Pagos YA registrados para esta cita (suma de Payment.total).
   * El parent (típicamente DetailPanel del agenda) lo calcula desde
   * `useCustomerPayments` y lo pasa para que el modal compute el saldo
   * correctamente: saldo = precio − anticipo validado − ya pagado.
   * Si no se pasa, asume 0 (modo legacy / compat).
   */
  alreadyPaid?: number
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
export function RegisterPaymentModal({
  appointment, alreadyPaid = 0, onClose,
}: RegisterPaymentModalProps) {
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [provider, setProvider] = useState<string | null>(null)
  // Saldo restante = total servicio − anticipo validado − pagos ya
  // registrados. Si la cliente sobre-pagó (raro), queda negativo y el
  // form muestra 0.
  const remaining = Math.max(
    0,
    appointment.priceSnapshot - appointment.validatedDepositAmount - alreadyPaid,
  )
  const hasDeposit = appointment.validatedDepositAmount > 0
  const hasPriorPayments = alreadyPaid > 0
  const [amount, setAmount] = useState(remaining)
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
          provider,
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

  // Si es Transfer, no podemos enviar sin banco — el backend devolvería 400.
  const providerRequired = method === 'Transfer'
  // Cap por overpay: el backend ahora rechaza amount > remaining con
  // un error explícito (defensa en profundidad). Acá lo prevenimos en
  // el UI mostrando el error antes de hacer la request.
  const exceedsRemaining = amount > remaining
  const canSubmit =
    amount > 0 &&
    !exceedsRemaining &&
    !submittingRef.current &&
    (!providerRequired || !!provider)

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
      <Card className="w-full max-w-lg space-y-4 p-5" onClick={e => e.stopPropagation()}>
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

        {/* Resumen de la cita — confirma identidad de la cita +
            breakdown del saldo cuando hay anticipo previo. */}
        <div className="rounded-lg bg-warm-50 border border-warm-150 p-3 text-sm">
          <div className="font-medium text-warm-800">{appointment.customerName}</div>
          <div className="text-warm-600 mt-0.5">
            {appointment.serviceName} · {appointment.stylistName}
          </div>
          {hasDeposit || hasPriorPayments ? (
            // Mostramos el breakdown cuando hay anticipo y/o pagos
            // parciales previos: la recepcionista entiende de dónde sale
            // el "falta cobrar" pre-rellenado.
            <div className="mt-2 pt-2 border-t border-warm-200 space-y-0.5 text-xs tabular-nums">
              <div className="flex justify-between text-warm-600">
                <span>Total servicio</span>
                <span>${appointment.priceSnapshot.toLocaleString('es-CO')}</span>
              </div>
              {hasDeposit && (
                <div className="flex justify-between text-brand-700">
                  <span>− Anticipo validado</span>
                  <span>${appointment.validatedDepositAmount.toLocaleString('es-CO')}</span>
                </div>
              )}
              {hasPriorPayments && (
                <div className="flex justify-between text-brand-700">
                  <span>− Pagos previos</span>
                  <span>${alreadyPaid.toLocaleString('es-CO')}</span>
                </div>
              )}
              <div className="flex justify-between font-semibold text-warm-800 pt-0.5 border-t border-warm-200/60">
                <span>Falta cobrar</span>
                <span>${remaining.toLocaleString('es-CO')}</span>
              </div>
            </div>
          ) : (
            <div className="text-warm-500 mt-0.5 text-xs tabular-nums">
              Precio del servicio: ${appointment.priceSnapshot.toLocaleString('es-CO')}
            </div>
          )}
        </div>

        {/* Método de pago — 3 chips + picker dinámico de banco/marca */}
        <div>
          <label className="mb-2 block text-xs uppercase tracking-wide text-warm-500">
            Método de pago
          </label>
          <PaymentMethodPicker
            method={method}
            provider={provider}
            onChange={(m, p) => {
              setMethod(m)
              setProvider(p)
            }}
          />
        </div>

        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
              Monto
              <span className="ml-1 normal-case text-warm-400 tracking-normal">
                (máx ${remaining.toLocaleString('es-CO')})
              </span>
            </label>
            <Input
              type="number"
              min={0}
              max={remaining}
              step={1000}
              value={amount}
              onChange={e => {
                // Cap visual: si tipea más del saldo, lo recortamos. El
                // backend igual valida; esto evita que vean "Total
                // $250.000" cuando solo deben $50.000.
                const n = Number(e.target.value)
                setAmount(Number.isFinite(n) ? Math.min(n, remaining) : 0)
              }}
            />
            {exceedsRemaining && (
              <p className="mt-1 text-[11.5px] text-terra-500">
                No puede ser mayor a ${remaining.toLocaleString('es-CO')}.
              </p>
            )}
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
              method === 'Card' ? 'Número de voucher del datáfono' :
              method === 'Transfer' ? 'Número de aprobación de la transferencia' :
              'Descripción (cheque, divisa, etc.)'
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

// Look del badge en tablas/cards: getPaymentBadge(method, provider).
// Re-exportado para compatibilidad con código existente.
export { getPaymentBadge } from '../paymentBadge'
