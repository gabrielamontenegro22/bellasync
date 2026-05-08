import { Search, Plus } from 'lucide-react'
import { cls } from '@/lib/cls'
import type { StylistResponse } from '@/api/stylists'
import type { StatusFilter } from '../types'

interface StylistFiltersProps {
  stylists: StylistResponse[]
  filter: StatusFilter
  onFilterChange: (f: StatusFilter) => void
  query: string
  onQueryChange: (q: string) => void
  onNew: () => void
}

const FILTERS: Array<{ id: StatusFilter; label: string }> = [
  { id: 'all',      label: 'Todos' },
  { id: 'active',   label: 'Activas' },
  { id: 'vacation', label: 'Vacaciones' },
  { id: 'inactive', label: 'Inactivas' },
]

/**
 * Toolbar de filtros: pills de status con counter + buscador + botón "Nuevo".
 * Replica el bloque "Toolbar" del mockup stylists.jsx.
 */
export function StylistFilters({
  stylists,
  filter,
  onFilterChange,
  query,
  onQueryChange,
  onNew,
}: StylistFiltersProps) {
  const counts = {
    all: stylists.length,
    active: stylists.filter((s) => s.status === 'Active').length,
    vacation: stylists.filter((s) => s.status === 'Vacation').length,
    inactive: stylists.filter((s) => s.status === 'Inactive').length,
  }

  return (
    <div className="px-6 lg:px-10 pb-5 flex flex-col sm:flex-row sm:items-center gap-3">
      {/* Pills */}
      <div className="flex items-center gap-1 p-1 bg-warm-100 rounded-lg">
        {FILTERS.map((f) => (
          <button
            key={f.id}
            type="button"
            onClick={() => onFilterChange(f.id)}
            className={cls(
              'px-3 py-1.5 rounded-md text-[12px] font-medium flex items-center gap-1.5 transition',
              filter === f.id
                ? 'bg-white text-warm-800 shadow-sm'
                : 'text-warm-500 hover:text-warm-700',
            )}
          >
            {f.label}
            <span
              className={cls(
                'text-[10.5px] tabular-nums px-1.5 rounded-full',
                filter === f.id
                  ? 'bg-brand-700 text-white'
                  : 'bg-warm-200 text-warm-600',
              )}
            >
              {counts[f.id]}
            </span>
          </button>
        ))}
      </div>

      {/* Buscador */}
      <div className="relative flex-1 sm:max-w-xs ml-auto">
        <Search
          size={15}
          className="absolute left-3 top-1/2 -translate-y-1/2 text-warm-400"
        />
        <input
          value={query}
          onChange={(e) => onQueryChange(e.target.value)}
          placeholder="Buscar por nombre, rol o correo…"
          className="w-full pl-9 pr-3 py-2 rounded-lg bg-white border border-warm-200 text-[12.5px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none"
        />
      </div>

      {/* Nuevo (en mobile aparece debajo) */}
      <button
        type="button"
        onClick={onNew}
        className="px-3.5 py-2 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium flex items-center justify-center gap-1.5 shadow-soft sm:hidden"
      >
        <Plus size={15} /> Nuevo
      </button>
    </div>
  )
}
