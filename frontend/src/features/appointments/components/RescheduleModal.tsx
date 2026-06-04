import { useState } from 'react'
import { Button, DateTimePicker, Modal, ModalFooter } from '@/components/ui'
import type { AppointmentResponse } from '@/api/appointments'
import { extractApiError } from '@/lib/extractApiError'
import { useAuth } from '@/features/auth/useAuth'
import { useRescheduleAppointment } from '../hooks'

interface RescheduleModalProps {
  appointment: AppointmentResponse
  onClose: () => void
}

/**
 * Modal "Reagendar cita" — abierto desde el panel detalle del agenda.
 * Solo cambia el slot (fecha + hora); cliente/estilista/servicio se
 * mantienen igual. Si quieres cambiar otra cosa, hay que cancelar y
 * crear una nueva.
 *
 * Pre-rellena los pickers con la fecha+hora actual de la cita para
 * que mover "30 min" sea barato. Si el SalonAdmin marca el checkbox
 * de walk-in puede pasar por encima de la regla de 30 min de
 * anticipación.
 */
export function RescheduleModal({ appointment, onClose }: RescheduleModalProps) {
  const { user } = useAuth()
  const isAdmin = user?.role === 'SalonAdmin'

  // Pre-fill con la fecha actual de la cita en hora local.
  // datetime-local input necesita "YYYY-MM-DDTHH:mm" sin TZ.
  const [newStartLocal, setNewStartLocal] = useState(() => {
    const d = new Date(appointment.startAt)
    const yyyy = d.getFullYear()
    const mm = String(d.getMonth() + 1).padStart(2, '0')
    const dd = String(d.getDate()).padStart(2, '0')
    const hh = String(d.getHours()).padStart(2, '0')
    const mn = String(d.getMinutes()).padStart(2, '0')
    return `${yyyy}-${mm}-${dd}T${hh}:${mn}`
  })
  const [bypassAdvance, setBypassAdvance] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const reschedule = useRescheduleAppointment()

  async function submit() {
    setSubmitError(null)
    try {
      await reschedule.mutateAsync({
        id: appointment.id,
        req: {
          newStartAtUtc: new Date(newStartLocal).toISOString(),
          bypassAdvanceWindow: isAdmin && bypassAdvance,
        },
      })
      onClose()
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo reagendar la cita.'))
    }
  }

  // No tiene sentido reagendar a la misma hora.
  const originalLocal = new Date(appointment.startAt).toISOString().slice(0, 16)
  const newIsoLocal = new Date(newStartLocal).toISOString().slice(0, 16)
  const noChange = newIsoLocal === originalLocal

  return (
    <Modal title="Reagendar cita" onClose={onClose} size="sm">
      <div className="space-y-4">
        {/* Resumen no editable — para que la recepcionista confirme que está
            moviendo la cita correcta. */}
        <div className="rounded-lg bg-warm-50 border border-warm-150 p-3 text-sm">
          <div className="font-medium text-warm-800">{appointment.customerName}</div>
          <div className="text-warm-600 mt-0.5">
            {appointment.serviceName} · con {appointment.stylistName}
          </div>
          <div className="text-warm-500 mt-0.5 text-xs">
            Duración: {appointment.durationMinutes} min
          </div>
        </div>

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
            Nueva fecha y hora
          </label>
          <DateTimePicker
            value={newStartLocal}
            onChange={setNewStartLocal}
            minHour={6}
            maxHour={22}
            fullWidth
          />
        </div>

        {isAdmin && (
          <label className="flex items-start gap-2 text-xs text-warm-600 cursor-pointer">
            <input
              type="checkbox"
              checked={bypassAdvance}
              onChange={e => setBypassAdvance(e.target.checked)}
              className="mt-0.5"
            />
            <span>
              Saltar la regla de 30 min de anticipación
              <br />
              <span className="text-warm-400">
                (solo para imprevistos / walk-ins que llegan justo ahora)
              </span>
            </span>
          </label>
        )}

        <ModalFooter error={submitError}>
          <Button variant="secondary" onClick={onClose} fullWidth>
            Cancelar
          </Button>
          <Button
            fullWidth
            onClick={submit}
            loading={reschedule.isPending}
            disabled={noChange || reschedule.isPending}
          >
            {noChange ? 'Sin cambios' : 'Reagendar'}
          </Button>
        </ModalFooter>
      </div>
    </Modal>
  )
}
