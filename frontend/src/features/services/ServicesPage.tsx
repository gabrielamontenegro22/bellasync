import { useMemo, useState } from 'react'
import { Plus, Sparkles, Loader2, AlertCircle } from 'lucide-react'
import {
  type CreateServiceRequest,
  type ServiceCategoryEnum,
  type ServiceResponse,
  type UpdateServiceRequest,
} from '@/api/services'
import { ServiceCard } from './components/ServiceCard'
import { ServiceModal } from './components/ServiceModal'
import { ServiceFilters } from './components/ServiceFilters'
import {
  useServices,
  useCreateService,
  useUpdateService,
  useDeleteService,
} from './hooks'
import { serviceExtrasStorage } from './storage'
import type { ServiceFormData } from './schemas'
import type { StatusFilter } from './types'
import { useIsAdmin } from '@/features/auth/useAuth'

/**
 * Vista principal del Catálogo de Servicios.
 * Replica la sección "ServicesView" del mockup config-servicios.jsx.
 *
 * Conecta con:
 *  - GET    /api/Services            (listServices vía useServices)
 *  - POST   /api/Services            (createService vía useCreateService)
 *  - PUT    /api/Services/{id}       (updateService vía useUpdateService)
 *  - DELETE /api/Services/{id}       (deleteService vía useDeleteService)
 *
 * Trae TODOS los servicios (incluye inactivos) para que el filtro de status
 * pueda mostrarlos en el frontend.
 */
export function ServicesPage() {
  const isAdmin = useIsAdmin()
  // Estado de filtros
  const [category, setCategory] = useState<ServiceCategoryEnum | 'all'>('all')
  const [status, setStatus] = useState<StatusFilter>('all')
  const [query, setQuery] = useState('')

  // Estado del modal
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<ServiceResponse | null>(null)

  // Data + mutations
  const servicesQ = useServices(true) // includeInactive=true
  const createMut = useCreateService()
  const updateMut = useUpdateService()
  const deleteMut = useDeleteService()

  const services = servicesQ.data ?? []

  // Filtrado en memoria
  const filtered = useMemo(() => {
    return services.filter((s) => {
      if (category !== 'all' && s.category !== category) return false
      if (status === 'active' && !s.isActive) return false
      if (status === 'inactive' && s.isActive) return false
      if (query && !s.name.toLowerCase().includes(query.toLowerCase())) return false
      return true
    })
  }, [services, category, status, query])

  const activeCount = services.filter((s) => s.isActive).length
  const inactiveCount = services.length - activeCount

  /* -------------------- Handlers -------------------- */

  const handleOpenNew = () => {
    setEditing(null)
    setModalOpen(true)
  }

  const handleOpenEdit = (svc: ServiceResponse) => {
    setEditing(svc)
    setModalOpen(true)
  }

  const handleClose = () => {
    setModalOpen(false)
    setEditing(null)
  }

  const handleSave = async (form: ServiceFormData, originalId?: string) => {
    // Todos los campos del schema (incluyendo anticipo) van al backend.
    // Solo `assignedStylistIds` queda local hasta que tengamos F5 (Estilistas).
    const apiPayload: CreateServiceRequest = {
      name: form.name.trim(),
      description: form.description?.trim() || null,
      category: form.category,
      durationMinutes: form.durationMinutes,
      price: form.price,
      commissionPercentage: form.commissionPercentage,
      color: form.color?.trim() || null,
      requiresDeposit: form.requiresDeposit,
      depositPercentage: form.depositPercentage,
    }

    let saved: ServiceResponse
    if (originalId) {
      const updatePayload: UpdateServiceRequest = {
        ...apiPayload,
        isActive: form.isActive,
      }
      saved = await updateMut.mutateAsync({ id: originalId, payload: updatePayload })
    } else {
      saved = await createMut.mutateAsync(apiPayload)
    }

    // Solo persistimos `assignedStylistIds` localmente (queda hasta F5)
    serviceExtrasStorage.save(saved.id, {
      assignedStylistIds: form.assignedStylistIds,
    })
  }

  const handleDelete = async (id: string) => {
    await deleteMut.mutateAsync(id)
  }

  const handleToggleActive = async (svc: ServiceResponse) => {
    const payload: UpdateServiceRequest = {
      name: svc.name,
      description: svc.description,
      category: svc.category,
      durationMinutes: svc.durationMinutes,
      price: svc.price,
      commissionPercentage: svc.commissionPercentage,
      color: svc.color,
      requiresDeposit: svc.requiresDeposit,
      depositPercentage: svc.depositPercentage,
      isActive: !svc.isActive,
    }
    await updateMut.mutateAsync({ id: svc.id, payload })
    // assignedStylistIds locales se mantienen sin cambios
  }

  const handleDuplicate = async (svc: ServiceResponse) => {
    const extras = serviceExtrasStorage.get(svc.id)
    const payload: CreateServiceRequest = {
      name: `${svc.name} (copia)`,
      description: svc.description,
      category: svc.category,
      durationMinutes: svc.durationMinutes,
      price: svc.price,
      commissionPercentage: svc.commissionPercentage,
      color: svc.color,
      requiresDeposit: svc.requiresDeposit,
      depositPercentage: svc.depositPercentage,
    }
    const created = await createMut.mutateAsync(payload)
    // Copiamos también los stylists asignados al servicio nuevo
    serviceExtrasStorage.save(created.id, {
      assignedStylistIds: extras.assignedStylistIds,
    })
  }

  const clearFilters = () => {
    setCategory('all')
    setStatus('all')
    setQuery('')
  }

  /* -------------------- Render -------------------- */

  return (
    <div className="px-6 lg:px-10 py-8">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <div className="text-[11px] tracking-[0.18em] uppercase text-warm-400 font-medium mb-1.5">
            Configuración › Catálogo
          </div>
          <h1 className="font-serif text-[28px] sm:text-[40px] lg:text-[44px] leading-[1.05] tracking-tight text-warm-800">
            Catálogo de servicios
          </h1>
          {!servicesQ.isLoading && (
            <div className="mt-2.5 text-[13.5px] text-warm-500">
              <strong className="font-semibold text-warm-700 tabular-nums">{activeCount}</strong>{' '}
              servicios activos ·{' '}
              <strong className="text-warm-700 tabular-nums">{inactiveCount}</strong> inactivos
            </div>
          )}
        </div>

        {isAdmin && (
          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={handleOpenNew}
              className="px-4 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13.5px] font-medium flex items-center gap-1.5 shadow-soft"
            >
              <Plus size={15} />
              Nuevo servicio
            </button>
          </div>
        )}
      </div>

      {/* Filtros — solo se muestran si hay servicios */}
      {!servicesQ.isLoading && services.length > 0 && (
        <ServiceFilters
          services={services}
          category={category}
          onCategoryChange={setCategory}
          status={status}
          onStatusChange={setStatus}
          query={query}
          onQueryChange={setQuery}
        />
      )}

      {/* Estado: cargando */}
      {servicesQ.isLoading && (
        <div className="mt-10 py-14 rounded-2xl bg-white border border-warm-150 text-center">
          <Loader2 className="w-8 h-8 text-brand-700 animate-spin mx-auto" />
          <div className="text-[14px] text-warm-600 mt-3">Cargando servicios…</div>
        </div>
      )}

      {/* Estado: error */}
      {servicesQ.isError && !servicesQ.isLoading && (
        <div className="mt-10 py-10 rounded-2xl bg-terra-100/40 border border-terra-300/60 text-center">
          <AlertCircle className="w-8 h-8 text-terra-500 mx-auto" />
          <div className="font-serif text-[20px] text-warm-800 mt-3">
            No se pudo cargar el catálogo
          </div>
          <div className="text-[13px] text-warm-500 mt-2 max-w-sm mx-auto">
            Verifica que el backend esté corriendo en{' '}
            <code className="font-mono">http://localhost:5059</code>
          </div>
          <button
            type="button"
            onClick={() => servicesQ.refetch()}
            className="mt-4 text-[13px] text-brand-700 font-medium hover:underline"
          >
            Reintentar
          </button>
        </div>
      )}

      {/* Estado: vacío total (no hay servicios en el tenant) */}
      {!servicesQ.isLoading && !servicesQ.isError && services.length === 0 && (
        <div className="mt-10 py-16 rounded-2xl bg-white border border-warm-150 border-dashed text-center max-w-2xl mx-auto">
          <div className="w-14 h-14 rounded-full bg-brand-50 text-brand-700 flex items-center justify-center mx-auto">
            <Sparkles size={24} />
          </div>
          <div className="font-serif text-[24px] text-warm-800 mt-4">
            Aún no tenés servicios en el catálogo
          </div>
          <div className="text-[13px] text-warm-500 mt-2 max-w-sm mx-auto">
            {isAdmin
              ? 'Crea tu primer servicio para que tus clientes puedan agendar.'
              : 'Pedile a la administradora del salón que cree los servicios del catálogo.'}
          </div>
          {isAdmin && (
            <button
              type="button"
              onClick={handleOpenNew}
              className="mt-6 px-5 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13.5px] font-medium inline-flex items-center gap-1.5 shadow-soft"
            >
              <Plus size={15} />
              Crear primer servicio
            </button>
          )}
        </div>
      )}

      {/* Estado: con servicios pero filtro vacío */}
      {!servicesQ.isLoading &&
        services.length > 0 &&
        filtered.length === 0 && (
          <div className="mt-10 py-14 rounded-2xl bg-white border border-warm-150 border-dashed text-center">
            <div className="w-12 h-12 rounded-full bg-warm-100 text-warm-500 flex items-center justify-center mx-auto">
              <Sparkles size={20} />
            </div>
            <div className="font-serif text-[20px] text-warm-700 mt-3">
              No hay servicios con esos filtros.
            </div>
            <button
              type="button"
              onClick={clearFilters}
              className="mt-3 text-[13px] text-brand-700 font-medium hover:underline"
            >
              Limpiar filtros
            </button>
          </div>
        )}

      {/* Grid de cards */}
      {!servicesQ.isLoading && filtered.length > 0 && (
        <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
          {filtered.map((svc) => (
            <ServiceCard
              key={svc.id}
              service={svc}
              onEdit={isAdmin ? handleOpenEdit : undefined}
              onDuplicate={isAdmin ? handleDuplicate : undefined}
              onToggleActive={isAdmin ? handleToggleActive : undefined}
            />
          ))}
        </div>
      )}

      {/* Modal */}
      {modalOpen && (
        <ServiceModal
          initial={editing}
          onClose={handleClose}
          onSave={handleSave}
          onDelete={editing ? handleDelete : undefined}
        />
      )}
    </div>
  )
}
