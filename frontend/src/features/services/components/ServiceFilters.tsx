import { Search } from 'lucide-react'
import { cls } from '@/lib/cls'
import type { ServiceCategoryEnum, ServiceResponse } from '@/api/services'
import { CATEGORIES } from '../types'
import type { StatusFilter } from '../types'

interface ServiceFiltersProps {
  services: ServiceResponse[]
  category: ServiceCategoryEnum | 'all'
  onCategoryChange: (cat: ServiceCategoryEnum | 'all') => void
  status: StatusFilter
  onStatusChange: (s: StatusFilter) => void
  query: string
  onQueryChange: (q: string) => void
}

/**
 * Barra de filtros: pills de categorías con counter + chips de estado + buscador.
 * Replica el bloque "filtros" del mockup config-servicios.jsx.
 */
export function ServiceFilters({
  services,
  category,
  onCategoryChange,
  status,
  onStatusChange,
  query,
  onQueryChange,
}: ServiceFiltersProps) {
  const totalCount = services.length

  return (
    <div className="mt-7 flex flex-col lg:flex-row lg:items-center gap-4">
      {/* Pills de categorías */}
      <div className="flex items-center gap-1 overflow-x-auto -mx-1 px-1">
        <CategoryPill
          label="Todos"
          count={totalCount}
          active={category === 'all'}
          onClick={() => onCategoryChange('all')}
        />
        {CATEGORIES.map((c) => {
          const count = services.filter((s) => s.category === c.id).length
          return (
            <CategoryPill
              key={c.id}
              label={c.label}
              count={count}
              active={category === c.id}
              onClick={() => onCategoryChange(c.id)}
            />
          )
        })}
      </div>

      <div className="hidden lg:block w-px h-6 bg-warm-200" />

      {/* Filtro de status */}
      <div className="flex items-center gap-2">
        <StatusChip
          label="Todos"
          active={status === 'all'}
          onClick={() => onStatusChange('all')}
        />
        <StatusChip
          label="Activos"
          dotClass="bg-brand-500"
          active={status === 'active'}
          onClick={() => onStatusChange('active')}
        />
        <StatusChip
          label="Inactivos"
          dotClass="bg-warm-300"
          active={status === 'inactive'}
          onClick={() => onStatusChange('inactive')}
        />
      </div>

      {/* Buscador */}
      <div className="lg:ml-auto flex items-center gap-2 px-3 py-2 rounded-lg bg-white border border-warm-200 focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700 lg:w-[280px]">
        <Search size={15} className="text-warm-400" />
        <input
          value={query}
          onChange={(e) => onQueryChange(e.target.value)}
          placeholder="Buscar servicio…"
          className="flex-1 outline-none text-[13px] text-warm-800 placeholder:text-warm-400 bg-transparent"
        />
      </div>
    </div>
  )
}

/* ---------- Sub-componentes ---------- */

interface CategoryPillProps {
  label: string
  count: number
  active: boolean
  onClick: () => void
}

function CategoryPill({ label, count, active, onClick }: CategoryPillProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        'px-3.5 py-2 rounded-full text-[12.5px] whitespace-nowrap transition',
        active ? 'bg-warm-800 text-white' : 'text-warm-600 hover:bg-warm-100',
      )}
    >
      {label}
      <span
        className={cls(
          'ml-1.5 text-[11px] tabular-nums',
          active ? 'text-white/70' : 'text-warm-400',
        )}
      >
        {count}
      </span>
    </button>
  )
}

interface StatusChipProps {
  label: string
  dotClass?: string
  active: boolean
  onClick: () => void
}

function StatusChip({ label, dotClass, active, onClick }: StatusChipProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        'px-3 py-1.5 rounded-full text-[12.5px] flex items-center gap-1.5 border transition',
        active
          ? 'bg-white border-warm-800 text-warm-800 font-medium'
          : 'bg-white border-warm-200 text-warm-600 hover:border-warm-300',
      )}
    >
      {dotClass && <span className={cls('w-1.5 h-1.5 rounded-full', dotClass)} />}
      {label}
    </button>
  )
}
