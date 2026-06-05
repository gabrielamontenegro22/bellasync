import { useMemo, useState } from 'react'
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
export function CancelAppointmentModal({ appointment, onClose }: CancelAppointmentModalProps) {
  const perms = usePermissions()
  const cancel = useCancelAppointment()

  // Carga la política del salón solo si hay anticipo Validated — si no
  // hay plata, no necesitamos ventana para mostrar nada.
  const hasValidatedDeposit = appointment.validatedDepositAmount > 0
  const policyQ = useQuery({
    queryKey: ['paymentPolicy'],
    queryFn: getPaymentPolicy,
    enabled: hasValidatedDeposit,
    staleTime: 5 * 60_000,
  })

  // Computa la sugerencia automática localmente (espejo de la lógica del
  // backend) para mostrarla como hint. El backend recomputa al guardar,
  // así que este valor es solo display.
  const autoDecision: DepositRefundDecision | null = useMemo(() => {
    if (!hasValidatedDeposit || !policyQ.data) return null
    const hoursUntil = (new Date(appointment.startAt).getTime() - Date.now()) / 3_600_000
    return hoursUntil >= policyQ.data.cancellationWindowHours
      ? 'Refunded'
      : 'Forfeited'
  }, [hasValidatedDeposit, policyQ.data, appointment.startAt])

  const [reason, setReason] = useState('')
  // Override seleccionado por el usuario. null = "usar la sugerencia auto".
  // Solo se manda al backend si difiere del auto (para que la auditoría
  // sea más clara).
  const [override, setOverride] = useState<DepositRefundDecision | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const canOverride = perms.isAdmin || perms.canRefundDeposit
  const reasonRequired = hasValidatedDeposit
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
      onClose()
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo cancelar la cita.'))
    }
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
                <span className="text-warm-500">Según la política del salón </span>
                ({policyQ.data?.cancellationWindowHours}h de ventana):{' '}
                <strong className="text-warm-800">
                  {autoDecision === 'Refunded'
                    ? 'devolver el anticipo'
                    : 'el anticipo queda perdido'}
                </strong>
                .
              </div>
            )}

            <div>
              <label className="mb-1.5 block text-[11px] uppercase tracking-wide text-warm-500">
                ¿Qué hacer con el anticipo?
              </label>
              <div className="grid grid-cols-1 gap-1.5">
                <DecisionChip
                  label="Usar la sugerencia automática"
                  desc={autoDecision === 'Refunded'
                    ? 'Marcar como pendiente de devolución.'
                    : autoDecision === 'Forfeited'
                      ? 'El salón se queda con el anticipo.'
                      : 'Decisión por defecto del backend.'}
                  selected={override === null}
                  onClick={() => setOverride(null)}
                  disabled={false}
                />
                <DecisionChip
                  label="Devolver el anticipo"
                  desc="Queda pendiente hasta que marques la transferencia en Caja."
                  selected={override === 'Refunded'}
                  onClick={() => setOverride('Refunded')}
                  disabled={!canOverride}
                />
                <DecisionChip
                  label="Crédito para la próxima cita"
                  desc="La cliente reagenda y el anticipo se aplica al nuevo turno."
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
