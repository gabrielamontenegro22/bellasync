import { useMemo, useState } from 'react'
import { Sparkles } from 'lucide-react'
import { Button, Modal, ModalFooter } from '@/components/ui'
import type {
  AppointmentResponse,
  DepositRefundDecision,
} from '@/api/appointments'
import { extractApiError } from '@/lib/extractApiError'
import { usePermissions } from '@/features/auth/useAuth'
import { useQuery } from '@tanstack/react-query'
import { getPaymentPolicy } from '@/api/admin'
import { useCancelAppointment } from '../hooks'
import { cls } from '@/lib/cls'

interface CancelAppointmentModalProps {
  appointment: AppointmentResponse
  onClose: () => void
  /**
   * Callback opcional disparado cuando la cancelación generó un crédito
   * (decisión CreditPending) y la admin decide reagendar inmediatamente.
   * Recibe el customerId para que el padre abra el modal "Nueva cita"
   * con la cliente pre-seleccionada (el card de crédito disponible
   * aparece solo al cargar los créditos del cliente).
   *
   * Si no se pasa, el modal solo se cierra al elegir "Después".
   */
  onRescheduleAfterCredit?: (customerId: string) => void
}

/**
 * Modal "Cancelar cita" — reemplaza al window.prompt() viejo. Tiene
 * dos modos según si hay anticipo Validado:
 *
 *  - Sin anticipo: pide solo el motivo (opcional). Confirma y listo.
 *
 *  - Con anticipo Validado: además del motivo OBLIGATORIO, muestra
 *    la "sugerencia automática" según la ventana del salón y permite
 *    elegir manualmente entre los 3 estados — pero el dropdown solo
 *    está habilitado para admin o para recepción con CanRefundDeposit.
 *    Si recepción no tiene el permiso, se muestra solo la decisión
 *    automática (informativa) y el handler la respetará.
 *
 * Decisiones de UI:
 *  - El motivo es obligatorio cuando hay plata cobrada porque el
 *    backend lo rechaza si está vacío en ese caso. Hacerlo obligatorio
 *    en el form evita un round-trip de error.
 *  - El "auto" se muestra como hint arriba del dropdown para que
 *    quien no tiene permiso entienda qué va a pasar.
 */
export function CancelAppointmentModal({
  appointment, onClose, onRescheduleAfterCredit,
}: CancelAppointmentModalProps) {
  const perms = usePermissions()
  const cancel = useCancelAppointment()

  // Step interno del modal:
  //   - 'form'             → el formulario de cancelación (default)
  //   - 'credit-followup'  → pregunta "¿reagendar ahora?" después de
  //                          cancelar exitosamente con decisión Credit
  const [step, setStep] = useState<'form' | 'credit-followup'>('form')

  // Carga la política del salón solo si hay anticipo Validated — si no
  // hay plata, no necesitamos ventana para mostrar nada.
  const hasValidatedDeposit = appointment.validatedDepositAmount > 0
  const hasDirectPayments = appointment.directPaymentsTotal > 0
  // hasMoney = "tiene dinero asociado" (cualquier tipo). Espejo del check
  // del backend — si es true, el motivo es obligatorio.
  const hasMoney = hasValidatedDeposit || hasDirectPayments

  // Si la cita usa un voucher de crédito interno (anticipo cubierto con
  // saldo viejo de cita cancelada), la decisión "Devolver" no es legal:
  // la plata no entró en esta cita, era saldo aplicado. Forzar Refunded
  // crearía un Expense fantasma que dejaría el Neto del día negativo.
  // Por eso ocultamos el chip y mostramos una nota explicativa.
  const isInternalCreditCita = appointment.hasInternalCreditVoucher

  const policyQ = useQuery({
    queryKey: ['paymentPolicy'],
    queryFn: getPaymentPolicy,
    enabled: hasValidatedDeposit,
    staleTime: 5 * 60_000,
  })

  // Computa la sugerencia automática localmente (espejo de la lógica del
  // backend) para mostrarla como hint. El backend recomputa al guardar,
  // así que este valor es solo display.
  //
  // Reglas (mismas del backend):
  //   - windowHours <= 0  → Forfeited siempre (política estricta).
  //   - cita ya pasó       → Forfeited.
  //   - dentro de ventana → Refunded.
  //   - fuera de ventana  → Forfeited.
  const autoDecision: DepositRefundDecision | null = useMemo(() => {
    if (!hasValidatedDeposit || !policyQ.data) return null
    // Si es crédito interno, la decisión auto NO puede ser Refunded.
    // En esos casos el "auto" representa "lo más equivalente": dentro de
    // ventana → CreditPending (el saldo vuelve a estar disponible),
    // fuera de ventana → Forfeited (el salón lo retiene).
    if (isInternalCreditCita) {
      const win = policyQ.data.cancellationWindowHours
      if (win <= 0) return 'Forfeited'
      const hoursUntil = (new Date(appointment.startAt).getTime() - Date.now()) / 3_600_000
      return hoursUntil >= win ? 'CreditPending' : 'Forfeited'
    }
    const win = policyQ.data.cancellationWindowHours
    if (win <= 0) return 'Forfeited'
    const hoursUntil = (new Date(appointment.startAt).getTime() - Date.now()) / 3_600_000
    return hoursUntil >= win ? 'Refunded' : 'Forfeited'
  }, [hasValidatedDeposit, policyQ.data, appointment.startAt, isInternalCreditCita])

  const [reason, setReason] = useState('')
  // Override seleccionado por el usuario. null = "usar la sugerencia auto".
  // Solo se manda al backend si difiere del auto (para que la auditoría
  // sea más clara).
  const [override, setOverride] = useState<DepositRefundDecision | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const canOverride = perms.isAdmin || perms.canRefundDeposit
  // Motivo obligatorio si hay cualquier tipo de dinero asociado, no solo
  // vouchers — antes solo chequeábamos vouchers y el backend rechazaba
  // cuando había Payment directo sin motivo.
  const reasonRequired = hasMoney
  const reasonOk = !reasonRequired || reason.trim().length > 0

  async function submit() {
    setSubmitError(null)
    try {
      await cancel.mutateAsync({
        id: appointment.id,
        req: {
          reason: reason.trim() || undefined,
          depositOverride: override ?? undefined,
        },
      })

      // Si la decisión fue "Crédito para próxima cita", quedamos en el
      // modal y mostramos el follow-up. La cliente avisa al toque "¿lo
      // reagendamos?" y la admin lo hace en 1 sola interacción.
      // El callback al padre solo se invoca si dice "Sí, reagendar".
      if (override === 'CreditPending' && onRescheduleAfterCredit) {
        setStep('credit-followup')
        return
      }

      onClose()
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo cancelar la cita.'))
    }
  }

  function handleRescheduleNow() {
    onRescheduleAfterCredit?.(appointment.customerId)
    onClose()
  }

  if (step === 'credit-followup') {
    return (
      <Modal title="Crédito guardado" onClose={onClose} size="sm">
        <div className="space-y-4 text-center">
          <div className="mx-auto w-12 h-12 rounded-full bg-brand-50 flex items-center justify-center">
            <Sparkles size={22} className="text-brand-700" />
          </div>

          <div>
            <div className="text-[15px] font-medium text-warm-800">
              Cita cancelada — crédito creado
            </div>
            <div className="text-[13px] text-warm-600 mt-1.5 leading-relaxed">
              <strong className="text-warm-800 tabular-nums">
                ${appointment.validatedDepositAmount.toLocaleString('es-CO')}
              </strong>{' '}
              quedan disponibles como crédito para{' '}
              <strong className="text-warm-800">{appointment.customerName}</strong>.
            </div>
            <div className="text-[12.5px] text-warm-500 mt-2">
              ¿Querés reagendarle ahora una cita nueva?
            </div>
          </div>

          <ModalFooter>
            <Button variant="secondary" onClick={onClose} fullWidth>
              Después
            </Button>
            <Button fullWidth onClick={handleRescheduleNow}>
              Sí, reagendar ahora
            </Button>
          </ModalFooter>
        </div>
      </Modal>
    )
  }

  return (
    <Modal title="Cancelar cita" onClose={onClose} size="sm">
      <div className="space-y-4">
        <div className="rounded-lg bg-warm-50 border border-warm-150 p-3 text-sm">
          <div className="font-medium text-warm-800">{appointment.customerName}</div>
          <div className="text-warm-600 mt-0.5">
            {appointment.serviceName} · con {appointment.stylistName}
          </div>
          <div className="text-warm-500 mt-0.5 text-xs">
            {new Date(appointment.startAt).toLocaleString('es-CO', {
              weekday: 'short', day: 'numeric', month: 'short',
              hour: '2-digit', minute: '2-digit',
            })}
          </div>
        </div>

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
            Motivo {reasonRequired && <span className="text-red-600">*</span>}
          </label>
          <textarea
            value={reason}
            onChange={e => setReason(e.target.value)}
            rows={2}
            placeholder={reasonRequired
              ? 'La cliente avisó / no llegó / cambió de fecha…'
              : 'Opcional'}
            className="w-full rounded-lg border border-warm-200 px-3 py-2 text-sm text-warm-800 focus:outline-none focus:border-warm-400 resize-none"
          />
          {reasonRequired && !reasonOk && (
            <div className="text-xs text-warm-500 mt-1">
              Esta cita ya tiene anticipo cobrado — escribí por qué la cancelás
              para que quede en la auditoría.
            </div>
          )}
        </div>

        {hasValidatedDeposit && (
          <div className="rounded-lg border border-warm-150 bg-warm-50/60 p-3 space-y-3">
            <div>
              <div className="text-[11px] uppercase tracking-wide text-warm-500 mb-1">
                Anticipo cobrado
              </div>
              <div className="text-sm text-warm-800 tabular-nums">
                ${appointment.validatedDepositAmount.toLocaleString('es-CO')}
              </div>
            </div>

            {autoDecision && (
              <div className="text-xs text-warm-600 leading-relaxed">
                {(policyQ.data?.cancellationWindowHours ?? 0) <= 0 ? (
                  <>
                    <span className="text-warm-500">Política del salón: </span>
                    <strong className="text-warm-800">
                      el anticipo nunca se devuelve automáticamente
                    </strong>
                    .
                  </>
                ) : (
                  <>
                    <span className="text-warm-500">Según la política del salón </span>
                    ({policyQ.data?.cancellationWindowHours}h de ventana):{' '}
                    <strong className="text-warm-800">
                      {autoDecision === 'Refunded'
                        ? 'devolver el anticipo'
                        : 'el anticipo queda perdido'}
                    </strong>
                    .
                  </>
                )}
              </div>
            )}

            <div>
              <label className="mb-1.5 block text-[11px] uppercase tracking-wide text-warm-500">
                ¿Qué hacer con el anticipo?
              </label>

              {isInternalCreditCita && (
                <div className="text-[11.5px] text-amber-800 bg-amber-50 border border-amber-200 rounded-md px-3 py-2 mb-2 leading-snug">
                  <strong>Cita pagada con crédito interno.</strong> No se puede
                  devolver porque esa plata no entró hoy a la caja — era saldo
                  viejo de una cita anterior aplicado a ésta. Las opciones son
                  mantener el crédito disponible o retenerlo definitivamente.
                  Si necesitás transferir plata real a la cliente, registrá un
                  egreso manual desde <em>Caja → Registrar egreso</em>.
                </div>
              )}

              <div className="grid grid-cols-1 gap-1.5">
                <DecisionChip
                  label="Usar la sugerencia automática"
                  desc={isInternalCreditCita
                    ? (autoDecision === 'Forfeited'
                        ? 'Retener el saldo (cancelación tardía).'
                        : 'Devolver el saldo a crédito disponible.')
                    : (autoDecision === 'Refunded'
                        ? 'Marcar como pendiente de devolución.'
                        : autoDecision === 'Forfeited'
                          ? 'El salón se queda con el anticipo.'
                          : 'Decisión por defecto del backend.')}
                  selected={override === null}
                  onClick={() => setOverride(null)}
                  disabled={false}
                />
                {/* "Devolver el anticipo" SOLO visible cuando NO es crédito interno.
                    En internos, el dominio rechaza Refunded — mostrarlo confunde. */}
                {!isInternalCreditCita && (
                  <DecisionChip
                    label="Devolver el anticipo"
                    desc="Queda pendiente hasta que marques la transferencia en Caja."
                    selected={override === 'Refunded'}
                    onClick={() => setOverride('Refunded')}
                    disabled={!canOverride}
                  />
                )}
                <DecisionChip
                  label={isInternalCreditCita
                    ? 'Mantener crédito disponible'
                    : 'Crédito para la próxima cita'}
                  desc={isInternalCreditCita
                    ? 'El saldo vuelve a quedar disponible para que la cliente lo use en una próxima cita.'
                    : 'La cliente reagenda y el anticipo se aplica al nuevo turno.'}
                  selected={override === 'CreditPending'}
                  onClick={() => setOverride('CreditPending')}
                  disabled={!canOverride}
                />
                <DecisionChip
                  label="No devolver (cancelación tardía)"
                  desc="El salón se queda con el anticipo. Sin acciones posteriores."
                  selected={override === 'Forfeited'}
                  onClick={() => setOverride('Forfeited')}
                  disabled={!canOverride}
                />
              </div>
              {!canOverride && (
                <div className="text-[11px] text-warm-500 mt-2 leading-relaxed">
                  Solo la administradora puede elegir manualmente. La cancelación
                  va a aplicar la sugerencia automática de arriba.
                </div>
              )}
            </div>
          </div>
        )}

        <ModalFooter error={submitError}>
          <Button variant="secondary" onClick={onClose} fullWidth>
            Volver
          </Button>
          <Button
            fullWidth
            onClick={submit}
            loading={cancel.isPending}
            disabled={!reasonOk || cancel.isPending}
          >
            Cancelar cita
          </Button>
        </ModalFooter>
      </div>
    </Modal>
  )
}

function DecisionChip({
  label, desc, selected, onClick, disabled,
}: {
  label: string
  desc: string
  selected: boolean
  onClick: () => void
  disabled: boolean
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cls(
        'text-left rounded-lg border px-3 py-2 transition',
        selected
          ? 'border-brand-600 bg-brand-50/60 ring-1 ring-brand-200'
          : 'border-warm-200 bg-white hover:border-warm-300',
        disabled && 'opacity-50 cursor-not-allowed hover:border-warm-200',
      )}
    >
      <div className="text-[12.5px] font-medium text-warm-800">{label}</div>
      <div className="text-[11px] text-warm-500 mt-0.5 leading-snug">{desc}</div>
    </button>
  )
}
