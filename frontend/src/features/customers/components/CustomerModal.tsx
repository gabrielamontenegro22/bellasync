import { useState, useEffect } from 'react'
import { Button, Card, Input } from '@/components/ui'
import type { CustomerResponse } from '@/api/customers'
import { extractApiError } from '@/lib/extractApiError'
import { useCreateCustomer, useUpdateCustomer } from '../hooks'

interface CustomerModalProps {
  /** Si null, modo CREAR. Si pasado, modo EDITAR ese customer. */
  customer: CustomerResponse | null
  onClose: () => void
}

/**
 * Modal para crear o editar un cliente con todos los campos del backend.
 * Reusa los mismos hooks de useCreateCustomer/useUpdateCustomer que invalidan
 * la lista al persistir.
 */
export function CustomerModal({ customer, onClose }: CustomerModalProps) {
  const isEdit = customer !== null
  const create = useCreateCustomer()
  const update = useUpdateCustomer()

  const [form, setForm] = useState({
    fullName: '',
    phone: '',
    email: '',
    birthday: '',
    documentNumber: '',
    address: '',
    notes: '',
    acceptsMarketing: false,
    isActive: true,
  })
  const [error, setError] = useState<string | null>(null)

  // Hidratar form en modo edit
  useEffect(() => {
    if (customer) {
      setForm({
        fullName: customer.fullName,
        phone: customer.phone,
        email: customer.email ?? '',
        birthday: customer.birthday ?? '',
        documentNumber: customer.documentNumber ?? '',
        address: customer.address ?? '',
        notes: customer.notes ?? '',
        acceptsMarketing: customer.acceptsMarketing,
        isActive: customer.isActive,
      })
    }
  }, [customer])

  function update_<K extends keyof typeof form>(key: K, value: typeof form[K]) {
    setForm(prev => ({ ...prev, [key]: value }))
  }

  async function submit() {
    setError(null)
    try {
      const payload = {
        fullName: form.fullName.trim(),
        phone: form.phone.trim(),
        email: form.email.trim() || undefined,
        birthday: form.birthday || undefined,
        documentNumber: form.documentNumber.trim() || undefined,
        address: form.address.trim() || undefined,
        notes: form.notes.trim() || undefined,
        acceptsMarketing: form.acceptsMarketing,
      }

      if (isEdit && customer) {
        await update.mutateAsync({
          id: customer.id,
          req: { ...payload, isActive: form.isActive },
        })
      } else {
        await create.mutateAsync(payload)
      }
      onClose()
    } catch (e) {
      setError(extractApiError(e, 'No se pudo guardar el cliente.'))
    }
  }

  const submitting = create.isPending || update.isPending
  const canSubmit = form.fullName.trim().length >= 3 && form.phone.trim().length > 0

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4" onClick={onClose}>
      <Card className="w-full max-w-lg max-h-[90vh] overflow-auto space-y-3 p-5" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between">
          <h2 className="font-serif text-xl text-brand-700">
            {isEdit ? 'Editar cliente' : 'Nuevo cliente'}
          </h2>
          <button onClick={onClose} className="text-warm-400 hover:text-warm-600" aria-label="Cerrar">✕</button>
        </div>

        <Input label="Nombre completo *" value={form.fullName} onChange={e => update_('fullName', e.target.value)} />
        <Input label="Teléfono *" value={form.phone} onChange={e => update_('phone', e.target.value)} />
        <Input label="Email" type="email" value={form.email} onChange={e => update_('email', e.target.value)} />

        <div className="grid grid-cols-2 gap-2">
          <Input label="Cumpleaños" type="date" value={form.birthday} onChange={e => update_('birthday', e.target.value)} />
          <Input label="Cédula" value={form.documentNumber} onChange={e => update_('documentNumber', e.target.value)} />
        </div>

        <Input label="Dirección" value={form.address} onChange={e => update_('address', e.target.value)} />

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
            Notas internas
          </label>
          <textarea
            value={form.notes}
            onChange={e => update_('notes', e.target.value)}
            rows={3}
            placeholder="Alergias, preferencias, observaciones del estilista…"
            className="w-full rounded-md border border-warm-200 p-2 text-sm"
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.acceptsMarketing}
            onChange={e => update_('acceptsMarketing', e.target.checked)}
          />
          <span>Acepta recibir promociones por WhatsApp/email</span>
        </label>

        {isEdit && !form.isActive && (
          <label className="flex items-center gap-2 rounded-md bg-brand-50 p-2 text-sm">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={e => update_('isActive', e.target.checked)}
            />
            <span>Reactivar cliente archivado</span>
          </label>
        )}

        {error && <p className="rounded-md bg-terra-100 p-2 text-sm text-terra-700">{error}</p>}

        <div className="flex gap-2 pt-2">
          <Button variant="secondary" onClick={onClose} fullWidth>Cancelar</Button>
          <Button fullWidth onClick={submit} loading={submitting} disabled={!canSubmit}>
            {isEdit ? 'Guardar cambios' : 'Crear cliente'}
          </Button>
        </div>
      </Card>
    </div>
  )
}
