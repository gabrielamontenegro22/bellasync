import { useState } from 'react'
import { type VoucherResponse, type VoucherUrgency } from '@/api/vouchers'
import { Badge, Button, Card } from '@/components/ui'
import { usePendingVouchers, useValidateVoucher } from './hooks'

/** Cola de validación de pagos. Split view: lista izq + detalle der. */
export function ValidationQueuePage() {
  const { data: vouchers, isLoading, error } = usePendingVouchers()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  // Auto-seleccionar el primero al cargar
  if (vouchers && vouchers.length > 0 && !selectedId) {
    setSelectedId(vouchers[0].id)
  }

  const selected = vouchers?.find(v => v.id === selectedId) ?? null

  return (
    <div className="flex h-full gap-4 p-4">
      <section className="w-96 flex-shrink-0 space-y-2 overflow-auto">
        <Card className="p-3">
          <h1 className="font-serif text-xl text-brand-700">Cola de validación</h1>
          <p className="text-xs text-warm-500">{vouchers?.length ?? 0} comprobantes pendientes</p>
        </Card>

        {isLoading && <p className="px-3 text-sm text-warm-500">Cargando…</p>}
        {error && <p className="px-3 text-sm text-terra-700">No se pudo cargar la cola.</p>}

        {vouchers?.length === 0 && (
          <Card className="p-6 text-center">
            <p className="text-warm-500">No hay comprobantes pendientes 🎉</p>
          </Card>
        )}

        {vouchers?.map(v => (
          <VoucherListItem
            key={v.id}
            voucher={v}
            selected={selectedId === v.id}
            onClick={() => setSelectedId(v.id)}
          />
        ))}
      </section>

      <main className="flex-1 overflow-auto">
        {selected ? <VoucherDetail voucher={selected} /> : <EmptyDetail />}
      </main>
    </div>
  )
}

function VoucherListItem({
  voucher, selected, onClick,
}: { voucher: VoucherResponse; selected: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-start gap-3 rounded-lg border p-3 text-left transition ${
        selected ? 'border-brand-500 bg-brand-50' : 'border-warm-200 bg-white hover:border-brand-300'
      }`}
    >
      <UrgencyDot urgency={voucher.urgency} />
      <div className="flex-1">
        <p className="font-medium text-warm-900">{voucher.customerName}</p>
        <p className="text-xs text-warm-500">{voucher.serviceName} · {voucher.stylistName}</p>
        <p className="mt-1 text-xs text-warm-500">{formatMoney(voucher.reportedAmount)} · {formatRelative(voucher.receivedAt)}</p>
      </div>
    </button>
  )
}

function UrgencyDot({ urgency }: { urgency: VoucherUrgency }) {
  const color =
    urgency === 'urgent' ? 'bg-terra-400' :
    urgency === 'tomorrow' ? 'bg-gold-400' :
    'bg-warm-300'
  return <span className={`mt-1 inline-block h-2.5 w-2.5 flex-shrink-0 rounded-full ${color}`} />
}

function VoucherDetail({ voucher }: { voucher: VoucherResponse }) {
  const validate = useValidateVoucher()
  const [notes, setNotes] = useState('')

  const decide = (decision: 'Confirm' | 'Reject' | 'RequestClarification') => {
    validate.mutate({ id: voucher.id, decision, notes: notes || undefined }, {
      onSuccess: () => setNotes(''),
    })
  }

  return (
    <Card className="p-4">
      <div className="flex items-start justify-between">
        <div>
          <p className="font-serif text-2xl text-brand-700">{voucher.customerName}</p>
          <p className="text-sm text-warm-500">{voucher.customerPhone}</p>
        </div>
        <Badge tone={voucher.urgency === 'urgent' ? 'terra' : voucher.urgency === 'tomorrow' ? 'gold' : 'neutral'}>
          {voucher.urgency === 'urgent' ? 'Urgente' : voucher.urgency === 'tomorrow' ? 'Mañana' : 'Esta semana'}
        </Badge>
      </div>

      <hr className="my-4 border-warm-200" />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <InfoCard title="Cita">
          <Row label="Servicio">{voucher.serviceName}</Row>
          <Row label="Estilista">{voucher.stylistName}</Row>
          <Row label="Hora">{formatDateTime(voucher.appointmentStartAt)}</Row>
        </InfoCard>

        <InfoCard title="Pago esperado">
          <Row label="Monto">{formatMoney(voucher.appointmentDepositAmount)}</Row>
        </InfoCard>

        <InfoCard title="Comprobante recibido">
          <Row label="Monto">{formatMoney(voucher.reportedAmount)}</Row>
          {voucher.bank && <Row label="Banco">{voucher.bank}</Row>}
          {voucher.referenceNumber && <Row label="Ref">{voucher.referenceNumber}</Row>}
          {voucher.senderName && <Row label="De">{voucher.senderName}</Row>}
          <Row label="Recibido">{formatRelative(voucher.receivedAt)}</Row>
        </InfoCard>
      </div>

      {voucher.imageUrl && (
        <div className="mt-4">
          <p className="mb-2 text-xs uppercase tracking-wide text-warm-500">Imagen del comprobante</p>
          <a href={voucher.imageUrl} target="_blank" rel="noopener">
            <img src={voucher.imageUrl} alt="Comprobante" className="max-h-96 rounded-lg border border-warm-200" />
          </a>
        </div>
      )}

      <div className="mt-4">
        <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">Notas internas</label>
        <textarea
          value={notes}
          onChange={e => setNotes(e.target.value)}
          rows={2}
          className="w-full rounded-md border border-warm-200 p-2 text-sm"
          placeholder="Opcional: agregá un comentario para el equipo o el cliente"
        />
      </div>

      <div className="mt-4 flex gap-2">
        <Button onClick={() => decide('Confirm')} disabled={validate.isPending}>✓ Confirmar pago</Button>
        <Button variant="secondary" onClick={() => decide('RequestClarification')} disabled={validate.isPending}>
          ? Pedir aclaración
        </Button>
        <Button variant="danger" onClick={() => decide('Reject')} disabled={validate.isPending}>
          ✗ Rechazar
        </Button>
      </div>
    </Card>
  )
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-warm-200 p-3">
      <p className="mb-2 text-xs uppercase tracking-wide text-warm-500">{title}</p>
      <dl className="space-y-1 text-sm">{children}</dl>
    </div>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-2">
      <dt className="text-warm-500">{label}</dt>
      <dd className="text-right text-warm-900">{children}</dd>
    </div>
  )
}

function EmptyDetail() {
  return (
    <Card className="p-8 text-center">
      <p className="text-warm-500">Seleccioná un comprobante de la lista.</p>
    </Card>
  )
}

function formatMoney(amount: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount)
}
function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
}
function formatRelative(iso: string): string {
  const diffMin = Math.round((Date.now() - new Date(iso).getTime()) / 60000)
  if (diffMin < 1) return 'recién'
  if (diffMin < 60) return `hace ${diffMin} min`
  const diffH = Math.round(diffMin / 60)
  if (diffH < 24) return `hace ${diffH} h`
  return new Date(iso).toLocaleDateString('es-CO')
}
