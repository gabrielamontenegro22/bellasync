import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button, Card, Input, SearchablePicker } from '@/components/ui'
import { listServices, type ServiceResponse } from '@/api/services'
import { listStylists, type StylistResponse } from '@/api/stylists'
import { createCustomer, listCustomers, type CustomerResponse } from '@/api/customers'
import { extractApiError } from '@/lib/extractApiError'
import { useAuth } from '@/features/auth/useAuth'
import { useCreateAppointment } from '../hooks'

/**
 * Modal "Nueva cita" para la Agenda. Selecciona cliente (con autocomplete),
 * servicio y estilista (filtrado por servicio), y datetime. Llama a
 * useCreateAppointment que invalida la agenda al persistir.
 */
export function NewAppointmentModal({
  defaultDate, defaultCustomer = null, onClose,
}: {
  defaultDate: string
  /** Pre-selecciona un cliente (cuando se abre desde el detalle del cliente
   *  en el CRM o desde el panel de detalle de una cita). */
  defaultCustomer?: CustomerResponse | null
  onClose: () => void
}) {
  const { user } = useAuth()
  const isAdmin = user?.role === 'SalonAdmin'

  const [customer, setCustomer] = useState<CustomerResponse | null>(defaultCustomer)
  const [serviceId, setServiceId] = useState('')
  const [stylistId, setStylistId] = useState('')
  const [startAtLocal, setStartAtLocal] = useState(`${defaultDate}T10:00`)
  const [notes, setNotes] = useState('')
  const [bypassAdvance, setBypassAdvance] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const servicesQ = useQuery({ queryKey: ['services'], queryFn: () => listServices() })
  const stylistsQ = useQuery({ queryKey: ['stylists'], queryFn: () => listStylists() })

  // Estilistas filtrados por el servicio seleccionado
  const availableStylists = useMemo(() => {
    if (!serviceId) return stylistsQ.data ?? []
    return (stylistsQ.data ?? []).filter(s =>
      s.services.some(svc => svc.id === serviceId)
      && s.status !== 'Inactive',
    )
  }, [stylistsQ.data, serviceId])

  // Resetear stylist si deja de poder hacer el service
  useEffect(() => {
    if (stylistId && !availableStylists.some(s => s.id === stylistId)) {
      setStylistId('')
    }
  }, [availableStylists, stylistId])

  const create = useCreateAppointment()

  async function submit() {
    if (!customer || !serviceId || !stylistId) return
    setSubmitError(null)
    try {
      await create.mutateAsync({
        customerId: customer.id,
        serviceId,
        stylistId,
        startAtUtc: new Date(startAtLocal).toISOString(),
        notes: notes || null,
        // Solo se manda si el user es admin Y marcó el checkbox.
        // Si lo manda un Receptionist, el backend lo silencia igual.
        bypassAdvanceWindow: isAdmin && bypassAdvance,
      })
      onClose()
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo crear la cita.'))
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4" onClick={onClose}>
      <Card className="w-full max-w-lg space-y-4 p-5" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between">
          <h2 className="font-serif text-xl text-brand-700">Nueva cita</h2>
          <button onClick={onClose} className="text-warm-400 hover:text-warm-600" aria-label="Cerrar">✕</button>
        </div>

        <CustomerAutocomplete selected={customer} onSelect={setCustomer} />

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">Servicio</label>
          <SearchablePicker
            value={serviceId}
            onChange={setServiceId}
            placeholder="Elegir servicio…"
            searchPlaceholder="Buscar servicio…"
            options={(servicesQ.data ?? []).map((s: ServiceResponse) => ({
              value: s.id,
              label: s.name,
              sublabel: `${s.durationMinutes}min · ${formatMoney(s.price)}`,
            }))}
          />
        </div>

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">Estilista</label>
          <SearchablePicker
            value={stylistId}
            onChange={setStylistId}
            disabled={!serviceId}
            placeholder={serviceId ? 'Elegir estilista…' : 'Elegí servicio primero'}
            searchPlaceholder="Buscar estilista…"
            options={availableStylists.map((st: StylistResponse) => ({
              value: st.id,
              label: st.fullName,
              sublabel: st.status === 'Vacation' ? 'En vacaciones' : undefined,
            }))}
          />
          {serviceId && availableStylists.length === 0 && (
            <p className="mt-1 text-xs text-terra-700">Ningún estilista activo hace este servicio.</p>
          )}
        </div>

        <Input
          label="Fecha y hora"
          type="datetime-local"
          value={startAtLocal}
          onChange={e => setStartAtLocal(e.target.value)}
        />

        {isAdmin && (
          <label className="flex items-start gap-2 rounded-md bg-gold-50/50 p-2 text-sm cursor-pointer">
            <input
              type="checkbox"
              checked={bypassAdvance}
              onChange={e => setBypassAdvance(e.target.checked)}
              className="mt-0.5"
            />
            <span>
              <span className="font-medium text-gold-700">Cita imprevista (walk-in)</span>
              <span className="block text-xs text-warm-600">
                Saltar la regla de 30 min de anticipación. Útil cuando el cliente
                llega al salón sin cita previa.
              </span>
            </span>
          </label>
        )}

        <Input label="Notas (opcional)" value={notes} onChange={e => setNotes(e.target.value)} />

        {submitError && <p className="rounded-md bg-terra-100 p-2 text-sm text-terra-700">{submitError}</p>}

        <div className="flex gap-2">
          <Button variant="secondary" onClick={onClose} fullWidth>Cancelar</Button>
          <Button
            fullWidth
            onClick={submit}
            loading={create.isPending}
            disabled={!customer || !serviceId || !stylistId || !startAtLocal}
          >
            Agendar
          </Button>
        </div>
      </Card>
    </div>
  )
}

// ===== Subcomponente: Autocomplete de cliente =====

function CustomerAutocomplete({
  selected, onSelect,
}: { selected: CustomerResponse | null; onSelect: (c: CustomerResponse | null) => void }) {
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState(false)
  const [createMode, setCreateMode] = useState<{ name: string } | null>(null)

  // Debounced — espera 250ms antes de buscar
  const [debouncedSearch, setDebouncedSearch] = useState('')
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 250)
    return () => clearTimeout(t)
  }, [search])

  const query = useQuery({
    queryKey: ['customers-search', debouncedSearch],
    queryFn: () => listCustomers({ search: debouncedSearch, pageSize: 8 }),
    enabled: open && debouncedSearch.length >= 2,
  })

  if (selected) {
    return (
      <div>
        <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">Cliente</label>
        <div className="flex items-center justify-between rounded-md border border-brand-300 bg-brand-50 p-2">
          <div>
            <p className="text-sm font-medium text-warm-900">{selected.fullName}</p>
            <p className="text-xs text-warm-500">{selected.phone}</p>
          </div>
          <button
            type="button"
            onClick={() => { onSelect(null); setSearch(''); setOpen(true) }}
            className="text-xs text-brand-700 hover:underline"
          >
            Cambiar
          </button>
        </div>
      </div>
    )
  }

  // Modo "crear cliente nuevo inline"
  if (createMode) {
    return (
      <InlineCreateCustomer
        initialName={createMode.name}
        onCancel={() => setCreateMode(null)}
        onCreated={c => {
          onSelect(c)
          setCreateMode(null)
          setSearch('')
          setOpen(false)
        }}
      />
    )
  }

  const noResults = query.data && query.data.items.length === 0
  const searchLooksLikePhone = /^[\d\s\-+]{4,}$/.test(search.trim())

  return (
    <div className="relative">
      <Input
        label="Cliente"
        placeholder="Buscar por nombre o teléfono…"
        value={search}
        onChange={e => { setSearch(e.target.value); setOpen(true) }}
        onFocus={() => setOpen(true)}
      />
      {open && search.length >= 2 && (
        <div className="absolute z-10 mt-1 max-h-64 w-full overflow-auto rounded-md border border-warm-200 bg-white shadow-lg">
          {query.isLoading && <p className="px-3 py-2 text-sm text-warm-500">Buscando…</p>}

          {noResults && (
            <div className="px-3 py-2 text-sm">
              <p className="mb-2 text-warm-500">Sin resultados para "{search}".</p>
              <button
                type="button"
                onClick={() => setCreateMode({ name: searchLooksLikePhone ? '' : search })}
                className="w-full rounded-md bg-brand-50 px-3 py-2 text-left text-brand-700 hover:bg-brand-100"
              >
                + Crear cliente nuevo
              </button>
            </div>
          )}

          {query.data?.items.map(c => (
            <button
              key={c.id}
              type="button"
              className="block w-full px-3 py-2 text-left text-sm hover:bg-brand-50"
              onClick={() => { onSelect(c); setOpen(false); setSearch('') }}
            >
              <p className="font-medium text-warm-900">{c.fullName}</p>
              <p className="text-xs text-warm-500">{c.phone}</p>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

/**
 * Form inline para crear un cliente nuevo sin salir del modal de Nueva cita.
 * Solo pide nombre + teléfono (lo mínimo del CreateCustomerValidator).
 * Después del éxito, llama onCreated con el customer recién persistido.
 */
function InlineCreateCustomer({
  initialName, onCreated, onCancel,
}: {
  initialName: string
  onCreated: (c: CustomerResponse) => void
  onCancel: () => void
}) {
  const [fullName, setFullName] = useState(initialName)
  const [phone, setPhone] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    setError(null)
    setSubmitting(true)
    try {
      const created = await createCustomer({ fullName: fullName.trim(), phone: phone.trim() })
      onCreated(created)
    } catch (e) {
      setError(extractApiError(e, 'No se pudo crear el cliente.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="rounded-md border border-brand-300 bg-brand-50/30 p-3">
      <div className="mb-2 flex items-center justify-between">
        <label className="text-xs uppercase tracking-wide text-warm-500">Nuevo cliente</label>
        <button
          type="button"
          onClick={onCancel}
          className="text-xs text-warm-400 hover:text-warm-600"
        >
          Cancelar
        </button>
      </div>
      <div className="space-y-2">
        <Input
          placeholder="Nombre completo"
          value={fullName}
          onChange={e => setFullName(e.target.value)}
        />
        <Input
          placeholder="Teléfono (WhatsApp)"
          value={phone}
          onChange={e => setPhone(e.target.value)}
        />
        {error && <p className="rounded bg-terra-100 p-2 text-xs text-terra-700">{error}</p>}
        <Button
          onClick={submit}
          loading={submitting}
          disabled={fullName.trim().length < 3 || !phone.trim()}
          fullWidth
          size="sm"
        >
          Crear y seleccionar
        </Button>
      </div>
    </div>
  )
}

function formatMoney(amount: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount)
}
