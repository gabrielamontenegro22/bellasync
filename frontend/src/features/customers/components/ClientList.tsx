import { useState, useEffect, useMemo } from 'react'
import { Search } from 'lucide-react'
import type { CustomerResponse } from '@/api/customers'
import { cls } from '@/lib/cls'
import { useAuth } from '@/features/auth/useAuth'
import { useCustomers } from '../hooks'
import { TAG_BADGE, initialsOf, relativeFrom, toneOf } from '../lib/customerLook'

const TAB_FILTERS = [
  { id: 'all',    label: 'Todos' },
  { id: 'frec',   label: 'Frecuentes' },
  { id: 'nuevos', label: 'Nuevos' },
  { id: 'inact',  label: 'Inactivos' },
] as const

type TabId = typeof TAB_FILTERS[number]['id']

interface ClientListProps {
  selectedId: string | null
  onSelect: (c: CustomerResponse) => void
  onNew: () => void
}

/**
 * Panel izquierdo del CRM: lista de clientes con búsqueda y filtros por tag.
 *
 * - El search input es local + debounced antes de pegarle al backend.
 * - Los filtros por tag (Todos/Frecuentes/Nuevos/Inactivos) son client-side:
 *   pedimos siempre los archivados (`includeInactive`) y filtramos en memoria.
 *   Razón: simplifica el endpoint y los conteos quedan consistentes.
 */
export function ClientList({ selectedId, onSelect, onNew }: ClientListProps) {
  const { user } = useAuth()
  const [tab, setTab] = useState<TabId>('all')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')

  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 300)
    return () => clearTimeout(t)
  }, [search])

  // Pedimos hasta 100 clientes — la lista de un salón típico cabe ahí.
  // Si llegan a tener más, hay que paginar.
  const { data, isLoading, error } = useCustomers({
    search: debouncedSearch || undefined,
    page: 1,
    pageSize: 100,
    includeInactive: true,
  })

  const items = data?.items ?? []

  const counts = useMemo(() => ({
    all:    items.length,
    frec:   items.filter(c => c.tag === 'Frecuente' || c.tag === 'VIP').length,
    nuevos: items.filter(c => c.tag === 'Nuevo').length,
    inact:  items.filter(c => c.tag === 'Inactivo').length,
  }), [items])

  const filtered = useMemo(() => {
    return items.filter(c => {
      if (tab === 'frec'   && c.tag !== 'Frecuente' && c.tag !== 'VIP') return false
      if (tab === 'nuevos' && c.tag !== 'Nuevo') return false
      if (tab === 'inact'  && c.tag !== 'Inactivo') return false
      return true
    })
  }, [items, tab])

  // En mobile (<md) la aside ocupa todo el ancho — el detalle no se ve hasta tap.
  // En tablet/desktop (≥md) se reduce a 38% (max 440px) para que el panel derecho
  // con la ficha tenga lugar. Antes el breakpoint era `lg` (1024px) así que iPad
  // (768px) se quedaba en modo "solo lista" y se veía vacío a la derecha.
  return (
    <aside className="w-full md:w-[38%] md:max-w-[440px] flex-shrink-0 border-r border-warm-150 bg-white flex flex-col min-h-0">
      {/* Header con título + búsqueda + tabs */}
      <div className="px-5 pt-5 pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="text-[11px] tracking-[0.18em] uppercase text-warm-400 font-medium mb-1.5 truncate">
              {user?.tenantName ?? 'CRM'}
            </div>
            <h1 className="font-serif text-[32px] leading-none text-warm-800">Clientes</h1>
            <div className="text-[12.5px] text-warm-500 mt-1.5">
              {counts.all} en tu base · {counts.frec} frecuentes
            </div>
          </div>
          <button
            type="button"
            onClick={onNew}
            className="shrink-0 px-3 py-2 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium"
          >
            + Nuevo
          </button>
        </div>

        {/* Búsqueda */}
        <div className="mt-4 flex items-center gap-2 px-3 py-2.5 rounded-xl border border-warm-200 bg-warm-50/60 focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700 focus-within:bg-white">
          <Search size={15} className="text-warm-400" />
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Buscar por nombre o WhatsApp…"
            className="flex-1 outline-none text-[13.5px] text-warm-800 placeholder:text-warm-400 bg-transparent"
          />
        </div>

        {/* Tabs */}
        <div className="mt-3 flex items-center gap-1 overflow-x-auto -mx-1 px-1">
          {TAB_FILTERS.map(t => (
            <button
              key={t.id}
              type="button"
              onClick={() => setTab(t.id)}
              className={cls(
                'px-3 py-1.5 rounded-full text-[12.5px] whitespace-nowrap flex items-center gap-1.5 transition',
                tab === t.id ? 'bg-warm-800 text-white' : 'text-warm-600 hover:bg-warm-100',
              )}
            >
              {t.label}
              <span className={cls(
                'tabular-nums text-[11px]',
                tab === t.id ? 'text-white/70' : 'text-warm-400',
              )}>
                {counts[t.id]}
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* Lista scrollable */}
      <div className="flex-1 overflow-y-auto px-2 pb-5">
        {isLoading && (
          <div className="py-12 text-center text-[13px] text-warm-500">Cargando…</div>
        )}
        {error && (
          <div className="py-12 text-center text-[13px] text-terra-500">
            No se pudo cargar la lista.
          </div>
        )}
        {!isLoading && !error && filtered.length === 0 && (
          <div className="py-12 text-center text-[13px] text-warm-500">
            {debouncedSearch ? `Sin resultados para "${debouncedSearch}".` : 'Sin resultados.'}
          </div>
        )}

        {filtered.map(c => {
          const sel = selectedId === c.id
          const tone = toneOf(c.id)
          const tag = TAG_BADGE[c.tag]
          return (
            <button
              key={c.id}
              type="button"
              onClick={() => onSelect(c)}
              className={cls(
                'w-full text-left flex items-start gap-3 p-3 rounded-xl transition border',
                sel
                  ? 'bg-brand-50/60 border-brand-100'
                  : 'border-transparent hover:bg-warm-50',
              )}
            >
              <div className={cls(
                'w-11 h-11 rounded-full flex items-center justify-center font-serif text-[15px] flex-shrink-0',
                tone.bg, tone.fg,
              )}>
                {initialsOf(c.fullName)}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className={cls(
                    'text-[13.5px] truncate',
                    sel ? 'text-brand-800 font-semibold' : 'text-warm-800 font-medium',
                  )}>
                    {c.fullName}
                  </span>
                </div>
                <div className="text-[11.5px] text-warm-500 mt-0.5 tabular-nums">{c.phone}</div>
                <div className="flex items-center gap-2 mt-1.5">
                  <span className={cls(
                    'text-[10px] tracking-[0.1em] uppercase font-medium px-1.5 py-0.5 rounded-md border',
                    tag.bg, tag.fg, tag.border,
                  )}>
                    {c.tag}
                  </span>
                  <span className="text-[11px] text-warm-400">·</span>
                  <span className="text-[11px] text-warm-500">{c.visits} visitas</span>
                </div>
              </div>
              <div className="text-right flex-shrink-0">
                <div className="text-[11px] text-warm-400">Última</div>
                <div className="text-[11.5px] text-warm-600 mt-0.5">
                  {relativeFrom(c.lastVisitAt)}
                </div>
              </div>
            </button>
          )
        })}
      </div>
    </aside>
  )
}
