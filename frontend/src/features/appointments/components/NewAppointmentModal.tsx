import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Sparkles } from 'lucide-react'
import { Button, DateTimePicker, Input, Modal, ModalFooter, SearchablePicker } from '@/components/ui'
import { listServices, type ServiceResponse } from '@/api/services'
import { listStylists, type StylistResponse } from '@/api/stylists'
import {
  createCustomer, getCustomer, getCustomerCredits, listCustomers,
  type CustomerCredit, type CustomerResponse,
} from '@/api/customers'
import { extractApiError } from '@/lib/extractApiError'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { useCreateAppointment } from '../hooks'

/**
 * Modal "Nueva cita" para la Agenda. Selecciona cliente (con autocomplete),
 * servicio y estilista (filtrado por servicio), y datetime. Llama a
 * useCreateAppointment que invalida la agenda al persistir.
 */
export function NewAppointmentModal({
  defaultDate, defaultCustomer = null, defaultCustomerId = null, onClose,
}: {
  defaultDate: string
  /** Pre-selecciona un cliente (cuando se abre desde el detalle del cliente
   *  en el CRM o desde el panel de detalle de una cita). */
  defaultCustomer?: CustomerResponse | null
  /**
   * Versión "solo ID" — útil cuando se abre desde un flujo que solo tiene
   * el customerId disponible (ej: post-cancel con crédito). El modal hace
   * el fetch internamente y pre-selecciona al cliente cuando llega.
   * Si se pasan ambos, defaultCustomer tiene prioridad.
   */
  defaultCustomerId?: string | null
  onClose: () => void
}) {
  const { user } = useAuth()
  const isAdmin = user?.role === 'SalonAdmin'

  // Si vino defaultCustomerId pero no defaultCustomer, fetch del cliente.
  const lookupQ = useQuery({
    queryKey: ['customer', defaultCustomerId],
    queryFn: () => getCustomer(defaultCustomerId!),
    enabled: !defaultCustomer && !!defaultCustomerId,
    staleTime: 60_000,
  })

  const [customer, setCustomer] = useState<CustomerResponse | null>(
    defaultCustomer ?? null,
  )
  // Cuando el lookup termina, setea el cliente seleccionado si todavía
  // no había uno (admin podría haber elegido otro mientras tanto).
  useEffect(() => {
    if (!customer && lookupQ.data) setCustomer(lookupQ.data)
  }, [lookupQ.data, customer])
  const [serviceId, setServiceId] = useState('')
  const [stylistId, setStylistId] = useState('')
  const [startAtLocal, setStartAtLocal] = useState(`${defaultDate}T10:00`)
  const [notes, setNotes] = useState('')
  const [bypassAdvance, setBypassAdvance] = useState(false)
  // Crédito disponible del cliente — checkbox para aplicarlo al anticipo
  // de esta cita nueva. null = "no aplicar"; true = "aplicar todos los
  // créditos disponibles FIFO hasta cubrir el anticipo".
  const [applyCredit, setApplyCredit] = useState(true)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const servicesQ = useQuery({ queryKey: ['services'], queryFn: () => listServices() })
  const stylistsQ = useQuery({ queryKey: ['stylists'], queryFn: () => listStylists() })

  // Carga créditos disponibles solo cuando hay cliente seleccionado.
  // staleTime corto para que después de cancelar una cita con CreditPending
  // la nueva info esté disponible casi inmediato al volver a abrir el modal.
  const creditsQ = useQuery({
    queryKey: ['customerCredits', customer?.id],
    queryFn: () => getCustomerCredits(customer!.id),
    enabled: !!customer,
    staleTime: 30_000,
  })

  // Servicio actualmente seleccionado — para calcular el anticipo requerido
  // y mostrar si el crédito disponible alcanza o no.
  const selectedService = useMemo(
    () => servicesQ.data?.find(s => s.id === serviceId),
    [servicesQ.data, serviceId],
  )
  const depositRequired = useMemo(() => {
    if (!selectedService || !selectedService.requiresDeposit) return 0
    return Math.round((selectedService.price * selectedService.depositPercentage) / 100)
  }, [selectedService])

  const totalAvailableCredit = useMemo(
    () => (creditsQ.data ?? []).reduce((sum, c) => sum + c.availableAmount, 0),
    [creditsQ.data],
  )
  const creditCoversDeposit = depositRequired > 0 && totalAvailableCredit >= depositRequired

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
      // Si la admin/recepción dejó el checkbox de crédito marcado Y el
      // crédito disponible cubre el anticipo requerido, mandamos los
      // voucherIds para que el backend los consuma FIFO. Si no cubre,
      // no los mandamos (el backend rechazaría con "insuficiente").
      const voucherIdsToApply = applyCredit && creditCoversDeposit
        ? (creditsQ.data ?? []).map(c => c.voucherId)
        : undefined

      await create.mutateAsync({
        customerId: customer.id,
        serviceId,
        stylistId,
        startAtUtc: new Date(startAtLocal).toISOString(),
        notes: notes || null,
        // Solo se manda si el user es admin Y marcó el checkbox.
        // Si lo manda un Receptionist, el backend lo silencia igual.
        bypassAdvanceWindow: isAdmin && bypassAdvance,
        applyCreditFromVoucherIds: voucherIdsToApply,
      })
      onClose()
    } catch (e) {
      setSubmitError(extractApiError(e, 'No se pudo crear la cita.'))
    }
  }

  return (
    <Modal title="Nueva cita" onClose={onClose} size="md">
      <div className="space-y-4">
        <CustomerAutocomplete selected={customer} onSelect={setCustomer} />

        {/* Crédito disponible — aparece solo si el cliente tiene vouchers
            CreditPending de citas canceladas. Se renderiza arriba del
            servicio porque la admin lo lee primero ("ah, esta clienta
            tenía crédito") y eso puede influir en qué servicio agendar. */}
        {customer && (creditsQ.data?.length ?? 0) > 0 && (
          <CreditAvailableCard
            credits={creditsQ.data!}
            totalAvailable={totalAvailableCredit}
            depositRequired={depositRequired}
            applyChecked={applyCredit}
            onToggleApply={setApplyCredit}
            serviceSelected={!!selectedService}
          />
        )}

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

        <div>
          <label className="mb-1 block text-xs uppercase tracking-wide text-warm-500">
            Fecha y hora
          </label>
          <DateTimePicker
            value={startAtLocal}
            onChange={setStartAtLocal}
            min="today"
            minHour={6}
            maxHour={22}
            fullWidth
          />
        </div>

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

        <ModalFooter error={submitError}>
          <Button variant="secondary" onClick={onClose} fullWidth>Cancelar</Button>
          <Button
            fullWidth
            onClick={submit}
            loading={create.isPending}
            disabled={!customer || !serviceId || !stylistId || !startAtLocal}
          >
            Agendar
          </Button>
        </ModalFooter>
      </div>
    </Modal>
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

/**
 * Card que aparece cuando la cliente tiene crédito disponible. Muestra
 * el total + un checkbox para aplicarlo + estado visual según si cubre
 * el anticipo del servicio seleccionado.
 *
 * Estados visuales:
 *  - Sin servicio elegido aún: solo informa "X de crédito".
 *  - Servicio elegido + crédito >= anticipo: verde, checkbox enable.
 *  - Servicio elegido + crédito < anticipo: amarillo, checkbox disable
 *    con explicación de por qué no se puede aplicar este servicio.
 */
function CreditAvailableCard({
  credits, totalAvailable, depositRequired, applyChecked, onToggleApply, serviceSelected,
}: {
  credits: CustomerCredit[]
  totalAvailable: number
  depositRequired: number
  applyChecked: boolean
  onToggleApply: (v: boolean) => void
  serviceSelected: boolean
}) {
  const covers = depositRequired > 0 && totalAvailable >= depositRequired
  const insufficient = serviceSelected && depositRequired > 0 && totalAvailable < depositRequired
  const noDepositService = serviceSelected && depositRequired === 0

  return (
    <div className={cls(
      'rounded-lg border p-3 space-y-2',
      insufficient
        ? 'border-amber-200 bg-amber-50/50'
        : 'border-brand-200 bg-brand-50/60',
    )}>
      <div className="flex items-start gap-2.5">
        <Sparkles size={16} className="shrink-0 mt-0.5 text-brand-700" />
        <div className="flex-1 min-w-0">
          <div className="text-[13px] font-medium text-warm-800">
            Esta cliente tiene{' '}
            <span className="tabular-nums">{formatMoney(totalAvailable)}</span>{' '}
            de crédito disponible
          </div>
          <div className="text-[11.5px] text-warm-600 mt-0.5 leading-snug">
            De {credits.length === 1 ? 'una cita cancelada' : `${credits.length} citas canceladas`} con anticipo pago.
            {credits.length === 1 && (
              <> Servicio: <span className="italic">{credits[0].originalServiceName}</span>.</>
            )}
          </div>
        </div>
      </div>

      {/* Estado según servicio elegido */}
      {!serviceSelected && (
        <div className="text-[11.5px] text-warm-500 italic">
          Elegí el servicio para ver si el crédito cubre el anticipo.
        </div>
      )}

      {noDepositService && (
        <div className="text-[11.5px] text-warm-500">
          Este servicio no requiere anticipo, así que el crédito queda para una próxima cita.
        </div>
      )}

      {insufficient && (
        <div className="text-[11.5px] text-amber-800">
          El crédito ({formatMoney(totalAvailable)}) no cubre el anticipo de este servicio ({formatMoney(depositRequired)}).
          Elegí un servicio con anticipo menor o aplicá el crédito en otra cita.
        </div>
      )}

      {covers && (
        <label className="flex items-center gap-2 cursor-pointer text-[12.5px] text-warm-700 pt-1 border-t border-brand-100">
          <input
            type="checkbox"
            checked={applyChecked}
            onChange={e => onToggleApply(e.target.checked)}
            className="accent-brand-700"
          />
          <span>
            Aplicar <strong className="tabular-nums">{formatMoney(depositRequired)}</strong> como anticipo de esta cita
            {totalAvailable > depositRequired && (
              <span className="text-warm-500"> (sobran {formatMoney(totalAvailable - depositRequired)} para otra)</span>
            )}
          </span>
        </label>
      )}
    </div>
  )
}
