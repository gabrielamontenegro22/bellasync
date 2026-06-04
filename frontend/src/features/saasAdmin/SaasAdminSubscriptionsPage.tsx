import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Loader2, X, XCircle } from 'lucide-react'
import {
  listPendingValidations,
  rejectSubscriptionPayment,
  validateSubscriptionPayment,
  type PendingValidationRow,
} from '@/api/saasAdmin'
import { Modal, ModalFooter } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { extractApiError } from '@/lib/extractApiError'
import { cls } from '@/lib/cls'

/**
 * Panel del SuperAdmin: cola de pagos reportados por los salones,
 * esperando validación contra el extracto bancario.
 *
 * Replica el patrón de ValidationQueuePage (vouchers de clientes
 * dentro de un salón), pero a nivel SaaS — pagos que los salones
 * hacen a BellaSync.
 *
 * Flujo: SuperAdmin abre Bancolombia, busca el monto + referencia,
 * y según si encuentra o no la transferencia, clickea Validar
 * o Rechazar (con motivo).
 */

const fmt = (n: number) => '$' + Math.round(n).toLocaleString('es-CO')
const fmtDateTime = (iso: string) => {
  const d = new Date(iso)
  return d.toLocaleString('es-CO', {
    day: 'numeric', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export function SaasAdminSubscriptionsPage() {
  const qc = useQueryClient()
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['saasAdmin', 'pendingValidations'],
    queryFn: listPendingValidations,
  })

  const [rejectingId, setRejectingId] = useState<string | null>(null)

  const refresh = () => qc.invalidateQueries({ queryKey: ['saasAdmin', 'pendingValidations'] })

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 max-w-5xl">
        <PageHeader />
        <div className="rounded-xl bg-warm-100 h-40 animate-pulse" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="px-6 lg:px-10 py-8 max-w-5xl">
        <PageHeader />
        <div className="rounded-xl bg-terra-100 border border-terra-200 p-4 text-[13px] text-terra-700">
          {error ? extractApiError(error) : 'No se pudo cargar la cola.'}
        </div>
      </div>
    )
  }

  return (
    <div className="px-6 lg:px-10 py-8 max-w-5xl">
      <PageHeader count={data.length} />

      {data.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="space-y-3">
          {data.map((row) => (
            <ValidationCard
              key={row.invoiceId}
              row={row}
              onValidated={refresh}
              onRequestReject={() => setRejectingId(row.invoiceId)}
            />
          ))}
        </div>
      )}

      {rejectingId && (
        <RejectModal
          invoiceId={rejectingId}
          onClose={() => setRejectingId(null)}
          onRejected={() => {
            setRejectingId(null)
            refresh()
          }}
        />
      )}
    </div>
  )
}

function PageHeader({ count }: { count?: number }) {
  return (
    <div className="mb-7">
      <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
        SaaS Admin · BellaSync
      </div>
      <h1 className="font-serif text-[40px] lg:text-[46px] leading-[1.02] tracking-tight text-warm-800 mt-1">
        Validación de pagos
        {count !== undefined && count > 0 && (
          <span className="text-[20px] text-warm-500 ml-3 align-middle">({count})</span>
        )}
      </h1>
      <p className="text-[14px] text-warm-600 leading-relaxed mt-2.5 max-w-2xl">
        Salones que reportaron transferencias. Cruza contra tu extracto bancario
        y aprueba o rechaza.
      </p>
    </div>
  )
}

function EmptyState() {
  return (
    <div className="rounded-2xl border-2 border-dashed border-warm-200 px-6 py-16 text-center">
      <CheckCircle size={32} className="mx-auto text-brand-600 mb-3" />
      <div className="text-[14px] font-medium text-warm-800">Cola vacía</div>
      <div className="text-[12.5px] text-warm-500 mt-1">
        Cuando un salón reporte una transferencia, va a aparecer acá.
      </div>
    </div>
  )
}

function ValidationCard({
  row,
  onValidated,
  onRequestReject,
}: {
  row: PendingValidationRow
  onValidated: () => void
  onRequestReject: () => void
}) {
  const [err, setErr] = useState<string | null>(null)

  const validateMut = useMutation({
    mutationFn: () => validateSubscriptionPayment(row.invoiceId),
    onSuccess: () => onValidated(),
    onError: (e) => setErr(extractApiError(e)),
  })

  return (
    <div className="rounded-xl border border-warm-200 bg-white p-4 sm:p-5 hover:shadow-soft transition">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-serif text-[20px] text-warm-800 truncate">
              {row.tenantName}
            </span>
            <span className="text-[10.5px] tracking-[0.1em] uppercase font-semibold text-warm-600 bg-warm-100 px-1.5 py-0.5 rounded">
              {row.planName}
            </span>
          </div>
          <div className="text-[12px] text-warm-500 mt-0.5">/{row.tenantSlug}</div>
        </div>
        <div className="text-right flex-shrink-0">
          <div className="font-serif text-[24px] tabular-nums text-warm-800">
            {fmt(row.amount)}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1.5 mt-4 text-[12.5px]">
        <RowField label="Método" value={row.reportedMethod} />
        <RowField label="Referencia" value={row.reportedReference ?? '(no envió)'} />
        <RowField label="Reportado" value={fmtDateTime(row.reportedAt)} />
        <RowField label="Vence" value={fmtDateTime(row.dueDate)} />
      </div>

      {err && (
        <div className="mt-3 rounded-md bg-terra-100 px-3 py-2 text-[12px] text-terra-700">
          {err}
        </div>
      )}

      <div className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-warm-100">
        <Button
          onClick={() => { setErr(null); validateMut.mutate() }}
          loading={validateMut.isPending}
          leftIcon={<CheckCircle size={14} />}
          size="sm"
        >
          Validar
        </Button>
        <Button
          variant="secondary"
          onClick={onRequestReject}
          leftIcon={<X size={14} />}
          size="sm"
          disabled={validateMut.isPending}
        >
          Rechazar
        </Button>
      </div>
    </div>
  )
}

function RowField({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span className="text-warm-500">{label}</span>
      <span className={cls('text-warm-800 font-medium truncate')}>{value}</span>
    </div>
  )
}

const PRESET_REASONS = [
  'No encuentro la transferencia en el extracto',
  'El monto no coincide',
  'La referencia no se puede ubicar',
  'La cuenta de origen no parece pertenecer al salón',
]

function RejectModal({
  invoiceId,
  onClose,
  onRejected,
}: {
  invoiceId: string
  onClose: () => void
  onRejected: () => void
}) {
  const [reason, setReason] = useState('')
  const [err, setErr] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: () => rejectSubscriptionPayment(invoiceId, reason.trim()),
    onSuccess: () => onRejected(),
    onError: (e) => setErr(extractApiError(e)),
  })

  return (
    <Modal title="Rechazar reporte" onClose={onClose} size="md">
      <p className="text-[13px] text-warm-600 mb-4">
        La factura volverá a estado Pendiente y el salón verá el motivo
        para corregir y reportar de nuevo.
      </p>

      <div className="space-y-3">
        <div>
          <label className="text-[12.5px] font-medium text-warm-700 mb-1.5 block">
            Motivo
          </label>
          <div className="flex flex-wrap gap-1.5 mb-2">
            {PRESET_REASONS.map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setReason(r)}
                className={cls(
                  'text-[11.5px] px-2.5 py-1 rounded-full border transition',
                  reason === r
                    ? 'border-brand-700 bg-brand-50 text-brand-800'
                    : 'border-warm-200 text-warm-600 hover:border-warm-300',
                )}
              >
                {r}
              </button>
            ))}
          </div>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            placeholder="Ej: el monto fue $80.000 en vez de los $90.000 reportados."
            className="w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition resize-none"
          />
        </div>
      </div>

      <ModalFooter error={err}>
        <Button variant="secondary" onClick={onClose} fullWidth disabled={mut.isPending}>
          Cancelar
        </Button>
        <Button
          variant="danger"
          onClick={() => { setErr(null); mut.mutate() }}
          fullWidth
          loading={mut.isPending}
          disabled={!reason.trim()}
          leftIcon={<XCircle size={14} />}
        >
          Rechazar
        </Button>
      </ModalFooter>
    </Modal>
  )
}

// (Loader2 importado para tipos, lo dejamos para evitar warning de import vacío
// si en el futuro lo usamos para algún spinner inline.)
void Loader2
