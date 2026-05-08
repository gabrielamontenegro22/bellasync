import { useMemo, useState } from 'react'
import { Plus, Loader2, AlertCircle, Users } from 'lucide-react'
import type {
  CreateStylistRequest,
  StylistResponse,
  UpdateStylistRequest,
} from '@/api/stylists'
import { StylistCard } from './components/StylistCard'
import { StylistModal } from './components/StylistModal'
import { StylistFilters } from './components/StylistFilters'
import {
  useStylists,
  useCreateStylist,
  useUpdateStylist,
  useDeleteStylist,
} from './hooks'
import type { StylistFormData } from './schemas'
import type { StatusFilter } from './types'
import { cls } from '@/lib/cls'

/**
 * Vista principal del catálogo de Estilistas.
 * Replica fielmente la sección "Page" de stylists.jsx + integra:
 *  - Datos reales del backend
 *  - Modal con asignación M:N de servicios
 *
 * Conecta con:
 *  - GET    /api/Stylists?includeInactive=true
 *  - POST   /api/Stylists
 *  - PUT    /api/Stylists/{id}
 *  - DELETE /api/Stylists/{id}
 */
export function StylistsPage() {
  const [filter, setFilter] = useState<StatusFilter>('all')
  const [query, setQuery] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<StylistResponse | null>(null)

  const stylistsQ = useStylists(true) // includeInactive=true
  const createMut = useCreateStylist()
  const updateMut = useUpdateStylist()
  const deleteMut = useDeleteStylist()

  const stylists = stylistsQ.data ?? []

  /* -------------------- Filtrado + KPIs -------------------- */

  const filtered = useMemo(() => {
    return stylists.filter((s) => {
      if (filter === 'active' && s.status !== 'Active') return false
      if (filter === 'vacation' && s.status !== 'Vacation') return false
      if (filter === 'inactive' && s.status !== 'Inactive') return false
      if (
        query &&
        !`${s.fullName} ${s.role} ${s.email ?? ''}`.toLowerCase().includes(query.toLowerCase())
      ) {
        return false
      }
      return true
    })
  }, [stylists, filter, query])

  const counts = useMemo(
    () => ({
      all: stylists.length,
      active: stylists.filter((s) => s.status === 'Active').length,
      vacation: stylists.filter((s) => s.status === 'Vacation').length,
      inactive: stylists.filter((s) => s.status === 'Inactive').length,
    }),
    [stylists],
  )

  /* -------------------- Handlers -------------------- */

  const handleOpenNew = () => {
    setEditing(null)
    setModalOpen(true)
  }

  const handleOpenEdit = (s: StylistResponse) => {
    setEditing(s)
    setModalOpen(true)
  }

  const handleClose = () => {
    setModalOpen(false)
    setEditing(null)
  }

  const handleSave = async (form: StylistFormData, originalId?: string) => {
    const basePayload: CreateStylistRequest = {
      fullName: form.fullName.trim(),
      role: form.role.trim(),
      email: form.email?.trim() || null,
      phone: form.phone?.trim() || null,
      idNumber: form.idNumber?.trim() || null,
      color: form.color?.trim() || null,
      hireDate: form.hireDate?.trim() || null,
      serviceIds: form.serviceIds,
    }

    if (originalId) {
      const updatePayload: UpdateStylistRequest = {
        ...basePayload,
        status: form.status,
      }
      await updateMut.mutateAsync({ id: originalId, payload: updatePayload })
    } else {
      await createMut.mutateAsync(basePayload)
    }
  }

  const handleDelete = async (id: string) => {
    await deleteMut.mutateAsync(id)
  }

  const handleToggleStatus = async (s: StylistResponse) => {
    const newStatus = s.status === 'Inactive' ? 'Active' : 'Inactive'
    const payload: UpdateStylistRequest = {
      fullName: s.fullName,
      role: s.role,
      email: s.email,
      phone: s.phone,
      idNumber: s.idNumber,
      color: s.color,
      hireDate: s.hireDate,
      status: newStatus,
      serviceIds: s.services.map((sv) => sv.id),
    }
    await updateMut.mutateAsync({ id: s.id, payload })
  }

  const handleHardDelete = async (s: StylistResponse) => {
    if (!window.confirm(`¿Eliminar a ${s.fullName} del equipo? Esto la marca como Inactiva.`))
      return
    await deleteMut.mutateAsync(s.id)
  }

  /* -------------------- Render -------------------- */

  return (
    <div className="px-6 lg:px-10 pt-8 pb-10">
      {/* Page header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
            Tu equipo
          </div>
          <h1 className="font-serif text-[42px] lg:text-[52px] leading-[1.02] tracking-tight text-warm-800 mt-1">
            Estilistas
          </h1>
          <p className="text-[14px] text-warm-500 mt-2 max-w-2xl">
            Gestiona quién hace parte de tu salón. Cada estilista tendrá sus propias citas en
            la agenda y métricas de desempeño cuando esté disponible el módulo de Agenda.
          </p>
        </div>

        <button
          type="button"
          onClick={handleOpenNew}
          className="px-3.5 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13px] font-medium flex items-center gap-1.5 shadow-soft hidden sm:flex"
        >
          <Plus size={15} /> Nuevo estilista
        </button>
      </div>

      {/* KPI strip */}
      {!stylistsQ.isLoading && stylists.length > 0 && (
        <div className="mt-7 grid sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <Kpi
            label="Personas activas"
            value={counts.active}
            sub={`de ${counts.all} en total`}
            accent="brand"
          />
          <Kpi label="En vacaciones" value={counts.vacation} sub="temporalmente fuera" accent="gold" />
          <Kpi label="Inactivas" value={counts.inactive} sub="archivadas" accent="warm" />
          <Kpi
            label="Citas (90d)"
            value={0}
            sub="cuando haya agenda"
            accent="warm"
            placeholder
          />
        </div>
      )}

      {/* Toolbar */}
      {!stylistsQ.isLoading && stylists.length > 0 && (
        <div className="mt-6 -mx-6 lg:-mx-10">
          <StylistFilters
            stylists={stylists}
            filter={filter}
            onFilterChange={setFilter}
            query={query}
            onQueryChange={setQuery}
            onNew={handleOpenNew}
          />
        </div>
      )}

      {/* Loading */}
      {stylistsQ.isLoading && (
        <div className="mt-10 py-14 rounded-2xl bg-white border border-warm-150 text-center">
          <Loader2 className="w-8 h-8 text-brand-700 animate-spin mx-auto" />
          <div className="text-[14px] text-warm-600 mt-3">Cargando estilistas…</div>
        </div>
      )}

      {/* Error */}
      {stylistsQ.isError && !stylistsQ.isLoading && (
        <div className="mt-10 py-10 rounded-2xl bg-terra-100/40 border border-terra-300/60 text-center">
          <AlertCircle className="w-8 h-8 text-terra-500 mx-auto" />
          <div className="font-serif text-[20px] text-warm-800 mt-3">
            No se pudo cargar el equipo
          </div>
          <div className="text-[13px] text-warm-500 mt-2 max-w-sm mx-auto">
            Verifica que el backend esté corriendo en{' '}
            <code className="font-mono">http://localhost:5059</code>
          </div>
          <button
            type="button"
            onClick={() => stylistsQ.refetch()}
            className="mt-4 text-[13px] text-brand-700 font-medium hover:underline"
          >
            Reintentar
          </button>
        </div>
      )}

      {/* Empty total */}
      {!stylistsQ.isLoading && !stylistsQ.isError && stylists.length === 0 && (
        <div className="mt-10 py-16 rounded-2xl bg-white border border-warm-150 border-dashed text-center max-w-2xl mx-auto">
          <div className="w-14 h-14 rounded-full bg-brand-50 text-brand-700 flex items-center justify-center mx-auto">
            <Users size={24} />
          </div>
          <div className="font-serif text-[24px] text-warm-800 mt-4">
            Aún no hay estilistas en tu equipo
          </div>
          <div className="text-[13px] text-warm-500 mt-2 max-w-sm mx-auto">
            Suma a la primera estilista para que pueda tomar citas.
          </div>
          <button
            type="button"
            onClick={handleOpenNew}
            className="mt-6 px-5 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13.5px] font-medium inline-flex items-center gap-1.5 shadow-soft"
          >
            <Plus size={15} />
            Agregar primera estilista
          </button>
        </div>
      )}

      {/* Sin resultados con filtro */}
      {!stylistsQ.isLoading && stylists.length > 0 && filtered.length === 0 && (
        <div className="mt-6 rounded-2xl border-2 border-dashed border-warm-200 py-14 text-center">
          <div className="text-[14px] text-warm-600">Sin resultados con esos filtros.</div>
          <button
            type="button"
            onClick={() => {
              setFilter('all')
              setQuery('')
            }}
            className="text-[12.5px] text-brand-700 hover:underline mt-2"
          >
            Limpiar filtros
          </button>
        </div>
      )}

      {/* Grid */}
      {!stylistsQ.isLoading && filtered.length > 0 && (
        <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-4">
          {filtered.map((s) => (
            <StylistCard
              key={s.id}
              stylist={s}
              onEdit={handleOpenEdit}
              onToggleStatus={handleToggleStatus}
              onDelete={handleHardDelete}
            />
          ))}
          <AddCard onClick={handleOpenNew} />
        </div>
      )}

      {/* Modal */}
      {modalOpen && (
        <StylistModal
          initial={editing}
          onClose={handleClose}
          onSave={handleSave}
          onDelete={editing ? handleDelete : undefined}
        />
      )}
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Subcomponentes                                                            */
/* -------------------------------------------------------------------------- */

function Kpi({
  label,
  value,
  sub,
  accent,
  placeholder,
}: {
  label: string
  value: number
  sub: string
  accent: 'brand' | 'gold' | 'warm' | 'terra'
  placeholder?: boolean
}) {
  const accents = {
    brand: 'text-brand-700',
    gold: 'text-gold-600',
    warm: 'text-warm-600',
    terra: 'text-terra-500',
  }
  return (
    <div className={cls('bg-white rounded-xl border border-warm-150 p-4 shadow-soft', placeholder && 'opacity-70')}>
      <div className="text-[10.5px] tracking-[0.18em] uppercase text-warm-500 font-medium">
        {label}
      </div>
      <div
        className={cls(
          'font-serif text-[36px] leading-none tabular-nums mt-2',
          accents[accent],
        )}
      >
        {placeholder ? '—' : value}
      </div>
      <div className="text-[11px] text-warm-500 mt-1.5">{sub}</div>
    </div>
  )
}

function AddCard({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group rounded-2xl border-2 border-dashed border-warm-250 hover:border-brand-500 hover:bg-brand-50/30 flex flex-col items-center justify-center text-warm-500 hover:text-brand-700 transition-all min-h-[280px] p-8 text-center"
    >
      <div className="w-12 h-12 rounded-full bg-warm-100 group-hover:bg-brand-100 flex items-center justify-center mb-3 transition">
        <Plus size={20} strokeWidth={2.2} />
      </div>
      <div className="text-[14px] font-medium">Agregar estilista</div>
      <div className="text-[11.5px] mt-1 text-warm-500">Suma a alguien al equipo</div>
    </button>
  )
}
