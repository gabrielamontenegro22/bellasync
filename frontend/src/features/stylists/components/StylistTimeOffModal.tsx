import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Palmtree, Trash2, Plus, AlertTriangle } from 'lucide-react'
import { Modal, ModalFooter } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { extractApiError } from '@/lib/extractApiError'
import { cls } from '@/lib/cls'
import {
  addStylistTimeOff,
  getAffectedAppointments,
  listStylistTimeOffs,
  removeStylistTimeOff,
  type StylistResponse,
  type StylistTimeOff,
} from '@/api/stylists'

/**
 * Modal de gestión de vacaciones / días libres de UN estilista. Muestra
 * los períodos existentes (con badge "pasado" en gris para los ya
 * cumplidos) y permite agregar uno nuevo con preview de citas afectadas.
 *
 * Cuando se agrega un período que cae sobre citas existentes, mostramos
 * la lista al admin para que las reagende manualmente (cada fila linkea
 * al detalle de la cita en /agenda). No reagendamos en bulk porque
 * cada cita necesita una decisión humana (otro estilista? mover día?).
 */
export function StylistTimeOffModal({
  stylist,
  onClose,
}: {
  stylist: StylistResponse
  onClose: () => void
}) {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: ['stylistTimeOffs', stylist.id],
    queryFn: () => listStylistTimeOffs(stylist.id),
  })

  const [showAddForm, setShowAddForm] = useState(false)

  const refresh = () =>
    qc.invalidateQueries({ queryKey: ['stylistTimeOffs', stylist.id] })

  const upcoming = (data ?? []).filter((t) => !t.isPast)
  const past = (data ?? []).filter((t) => t.isPast)

  return (
    <Modal title={`Días libres de ${stylist.fullName}`} onClose={onClose} size="lg">
      <p className="text-[13px] text-warm-600 mb-4">
        Marcá los períodos en los que {stylist.fullName.split(' ')[0]} no estará
        disponible. El sistema bloqueará automáticamente nuevas citas con ella
        esos días.
      </p>

      {showAddForm ? (
        <AddTimeOffForm
          stylistId={stylist.id}
          onCancel={() => setShowAddForm(false)}
          onAdded={() => {
            setShowAddForm(false)
            refresh()
          }}
        />
      ) : (
        <Button
          size="sm"
          onClick={() => setShowAddForm(true)}
          leftIcon={<Plus size={14} />}
          className="mb-4"
        >
          Agregar días libres
        </Button>
      )}

      {isLoading && <div className="h-32 rounded-lg bg-warm-100 animate-pulse" />}

      {data && data.length === 0 && (
        <div className="rounded-xl border-2 border-dashed border-warm-200 px-4 py-10 text-center">
          <Palmtree size={28} className="mx-auto text-warm-400 mb-2" />
          <div className="text-[13px] text-warm-500">
            {stylist.fullName.split(' ')[0]} no tiene días libres marcados.
          </div>
        </div>
      )}

      {upcoming.length > 0 && (
        <div className="mt-2">
          <div className="text-[10.5px] tracking-[0.14em] uppercase text-warm-500 font-medium mb-2">
            Próximos
          </div>
          <div className="space-y-2">
            {upcoming.map((t) => (
              <TimeOffRow key={t.id} timeOff={t} onRemoved={refresh} />
            ))}
          </div>
        </div>
      )}

      {past.length > 0 && (
        <div className="mt-5">
          <div className="text-[10.5px] tracking-[0.14em] uppercase text-warm-500 font-medium mb-2">
            Pasados
          </div>
          <div className="space-y-2 opacity-60">
            {past.map((t) => (
              <TimeOffRow key={t.id} timeOff={t} onRemoved={refresh} />
            ))}
          </div>
        </div>
      )}

      <ModalFooter>
        <Button variant="secondary" onClick={onClose} fullWidth>
          Cerrar
        </Button>
      </ModalFooter>
    </Modal>
  )
}

// ───────────────────────────────────────────────────────────────────────

function TimeOffRow({
  timeOff, onRemoved,
}: {
  timeOff: StylistTimeOff
  onRemoved: () => void
}) {
  const mut = useMutation({
    mutationFn: () => removeStylistTimeOff(timeOff.id),
    onSuccess: onRemoved,
    onError: (e) => window.alert(extractApiError(e, 'No se pudo borrar.')),
  })

  const isSingleDay = timeOff.fromDate === timeOff.toDate
  const label = isSingleDay
    ? fmt(timeOff.fromDate)
    : `${fmt(timeOff.fromDate)} – ${fmt(timeOff.toDate)}`

  return (
    <div className="flex items-center justify-between gap-3 px-3 py-2.5 rounded-lg border border-warm-150 bg-white">
      <div className="flex items-center gap-3 min-w-0 flex-1">
        <Palmtree size={14} className="text-warm-400 flex-shrink-0" />
        <div className="min-w-0">
          <div className="text-[13px] font-medium text-warm-800">{label}</div>
          {timeOff.reason && (
            <div className="text-[11.5px] text-warm-500 truncate">{timeOff.reason}</div>
          )}
        </div>
      </div>
      <button
        type="button"
        onClick={() => {
          if (window.confirm('¿Borrar este período?')) mut.mutate()
        }}
        disabled={mut.isPending}
        className="p-1.5 text-warm-400 hover:text-terra-600 hover:bg-terra-100/40 rounded-md transition disabled:opacity-40"
        title="Borrar"
      >
        <Trash2 size={13} />
      </button>
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────

function AddTimeOffForm({
  stylistId, onCancel, onAdded,
}: {
  stylistId: string
  onCancel: () => void
  onAdded: () => void
}) {
  const todayStr = new Date().toISOString().slice(0, 10)
  const [fromDate, setFromDate] = useState(todayStr)
  const [toDate, setToDate] = useState(todayStr)
  const [reason, setReason] = useState('')
  const [err, setErr] = useState<string | null>(null)

  // Preview de citas afectadas — se dispara cada vez que cambian las fechas.
  const affectedQ = useQuery({
    queryKey: ['affectedAppts', stylistId, fromDate, toDate],
    queryFn: () => getAffectedAppointments(stylistId, fromDate, toDate),
    enabled: !!fromDate && !!toDate && fromDate <= toDate,
  })

  const mut = useMutation({
    mutationFn: () =>
      addStylistTimeOff(stylistId, {
        fromDate,
        toDate,
        reason: reason.trim() || null,
      }),
    onSuccess: onAdded,
    onError: (e) => setErr(extractApiError(e)),
  })

  const affectedCount = affectedQ.data?.length ?? 0

  return (
    <div className="mb-5 rounded-xl border border-warm-200 bg-warm-50/40 p-4">
      <div className="grid grid-cols-2 gap-3 mb-3">
        <div>
          <label className="text-[11.5px] font-medium text-warm-700 mb-1 block">
            Desde
          </label>
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            min={todayStr}
            className="w-full px-3 py-2 rounded-lg border border-warm-200 bg-white text-[13px] text-warm-800"
          />
        </div>
        <div>
          <label className="text-[11.5px] font-medium text-warm-700 mb-1 block">
            Hasta
          </label>
          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
            min={fromDate || todayStr}
            className="w-full px-3 py-2 rounded-lg border border-warm-200 bg-white text-[13px] text-warm-800"
          />
        </div>
      </div>

      <div className="mb-3">
        <label className="text-[11.5px] font-medium text-warm-700 mb-1 block">
          Motivo <span className="text-warm-400">(opcional)</span>
        </label>
        <input
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Vacaciones, capacitación, cita médica…"
          className="w-full px-3 py-2 rounded-lg border border-warm-200 bg-white text-[13px] text-warm-800"
          maxLength={200}
        />
      </div>

      {/* Preview de citas afectadas */}
      {affectedCount > 0 && (
        <div className="mb-3 rounded-lg bg-gold-50 border border-gold-200 p-3">
          <div className="flex items-center gap-2 text-[12.5px] font-semibold text-gold-700 mb-2">
            <AlertTriangle size={14} />
            {affectedCount} cita{affectedCount === 1 ? '' : 's'} en ese período
          </div>
          <ul className="space-y-1 text-[11.5px] text-warm-700 max-h-32 overflow-y-auto">
            {affectedQ.data!.slice(0, 8).map((a) => {
              const d = new Date(a.startAt)
              return (
                <li key={a.appointmentId} className="flex gap-2">
                  <span className="text-warm-500 tabular-nums">
                    {d.toLocaleDateString('es-CO', { day: '2-digit', month: 'short' })}{' '}
                    {d.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' })}
                  </span>
                  <span className="truncate">{a.customerName} — {a.serviceName}</span>
                </li>
              )
            })}
            {affectedQ.data!.length > 8 && (
              <li className="text-warm-500 italic">
                +{affectedQ.data!.length - 8} más…
              </li>
            )}
          </ul>
          <div className="text-[11px] text-warm-600 mt-2 leading-relaxed">
            Estas citas <strong>no se cancelan automáticamente</strong>. Después de
            guardar, andá a la agenda y reagendalas con otro estilista o pasalas
            a otro día.
          </div>
        </div>
      )}

      {err && (
        <div className="mb-3 rounded-md bg-terra-100 px-3 py-2 text-[12px] text-terra-700">
          {err}
        </div>
      )}

      <div className={cls('flex gap-2', affectedCount > 0 && 'flex-col-reverse sm:flex-row')}>
        <Button variant="secondary" onClick={onCancel} fullWidth size="sm" disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={() => { setErr(null); mut.mutate() }}
          fullWidth
          size="sm"
          loading={mut.isPending}
        >
          {affectedCount > 0 ? 'Guardar igual' : 'Guardar'}
        </Button>
      </div>
    </div>
  )
}

function fmt(dateStr: string): string {
  // dateStr viene como "YYYY-MM-DD" — armamos en local sin desplazamiento.
  const [y, m, d] = dateStr.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  return date.toLocaleDateString('es-CO', {
    day: 'numeric', month: 'short', year: 'numeric',
  })
}
