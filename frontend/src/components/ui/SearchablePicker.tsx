import { useEffect, useMemo, useRef, useState } from 'react'
import { Check, ChevronDown, Search } from 'lucide-react'
import { cls } from '@/lib/cls'

export interface PickerOption {
  value: string
  /** Lo que se renderiza en grande dentro del row. */
  label: string
  /** Renglón secundario, color tenue (duración, precio, etc.). Opcional. */
  sublabel?: string
  /**
   * Texto adicional que se incluye en el filtro de búsqueda pero no
   * se renderiza. Útil para que "60min" o "balayage" matcheen aunque
   * no estén visibles. Default: label + sublabel.
   */
  searchText?: string
  /** Si true, no es seleccionable. Se muestra en gris. */
  disabled?: boolean
}

interface Props {
  value: string
  onChange: (value: string) => void
  options: PickerOption[]
  /** Texto cuando no hay nada seleccionado. */
  placeholder?: string
  /** Placeholder del input de búsqueda dentro del dropdown. */
  searchPlaceholder?: string
  /** Texto cuando ningún match. */
  emptyMessage?: string
  /** Si los hits son <= a este número, el input de búsqueda se esconde. Default 8. */
  searchableThreshold?: number
  disabled?: boolean
  /** Tamaño del control. */
  size?: 'sm' | 'md'
  /** Mostrar X para limpiar. Default false. */
  clearable?: boolean
  className?: string
}

/**
 * Picker genérico con búsqueda. Reemplaza los `<select>` nativos del
 * browser que se ven feos y no tienen búsqueda — clave cuando hay
 * muchas opciones (catálogo de servicios, lista de bancos, etc.).
 *
 * UX:
 *  - Click en el control abre el dropdown.
 *  - Si options.length > searchableThreshold, arriba aparece input
 *    con typeahead que filtra en vivo (case-insensitive, busca en
 *    label + sublabel + searchText).
 *  - Keyboard: ↑/↓ para navegar resultados, Enter selecciona, Esc cierra.
 *  - Click afuera cierra.
 *  - Si el value seleccionado ya no existe en options (option deleted),
 *    se muestra el value crudo entre paréntesis.
 */
export function SearchablePicker({
  value,
  onChange,
  options,
  placeholder = 'Elegir…',
  searchPlaceholder = 'Buscar…',
  emptyMessage = 'Sin coincidencias',
  searchableThreshold = 8,
  disabled = false,
  size = 'md',
  clearable = false,
  className,
}: Props) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  const showSearch = options.length > searchableThreshold

  // Click fuera cierra.
  useEffect(() => {
    if (!open) return
    function onDocClick(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) {
        setOpen(false)
        setQuery('')
      }
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [open])

  // Focus al input cuando abre.
  useEffect(() => {
    if (open && showSearch) {
      const t = setTimeout(() => inputRef.current?.focus(), 10)
      return () => clearTimeout(t)
    }
  }, [open, showSearch])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return options
    return options.filter((o) => {
      const haystack = (
        o.searchText ?? `${o.label} ${o.sublabel ?? ''}`
      ).toLowerCase()
      return haystack.includes(q)
    })
  }, [options, query])

  // Reset active index cuando cambia el filtro.
  useEffect(() => {
    setActiveIndex(0)
  }, [query, open])

  const selectedOpt = options.find((o) => o.value === value)

  const handleSelect = (opt: PickerOption) => {
    if (opt.disabled) return
    onChange(opt.value)
    setOpen(false)
    setQuery('')
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIndex((i) => Math.min(filtered.length - 1, i + 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex((i) => Math.max(0, i - 1))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      const hit = filtered[activeIndex]
      if (hit) handleSelect(hit)
    } else if (e.key === 'Escape') {
      setOpen(false)
      setQuery('')
    }
  }

  const sizeClasses =
    size === 'sm'
      ? 'px-2.5 py-1.5 text-[12.5px]'
      : 'px-3 py-2.5 text-[13px]'

  return (
    <div ref={rootRef} className={cls('relative', className)}>
      <button
        type="button"
        onClick={() => !disabled && setOpen((v) => !v)}
        disabled={disabled}
        className={cls(
          'w-full flex items-center justify-between gap-2 rounded-lg bg-white border transition',
          sizeClasses,
          disabled
            ? 'border-warm-150 text-warm-400 cursor-not-allowed'
            : selectedOpt
            ? 'border-warm-200 text-warm-800 hover:border-warm-300'
            : 'border-warm-200 text-warm-400 hover:border-warm-300',
          open && 'border-brand-400 ring-2 ring-brand-100',
        )}
      >
        <span className="flex-1 text-left truncate">
          {selectedOpt
            ? selectedOpt.label
            : value
            ? <span className="italic">({value})</span>
            : placeholder}
        </span>
        <div className="flex items-center gap-1 text-warm-400">
          {clearable && selectedOpt && !disabled && (
            <span
              role="button"
              tabIndex={0}
              onClick={(e) => {
                e.stopPropagation()
                onChange('')
              }}
              className="hover:text-warm-700 px-0.5"
              aria-label="Limpiar"
            >
              ×
            </span>
          )}
          <ChevronDown
            size={15}
            strokeWidth={1.8}
            className={cls('transition', open && 'rotate-180')}
          />
        </div>
      </button>

      {open && (
        <div className="absolute z-20 mt-1.5 w-full rounded-lg bg-white border border-warm-200 shadow-pop max-h-[280px] overflow-hidden flex flex-col">
          {showSearch && (
            <div className="px-2.5 py-2 border-b border-warm-150 flex items-center gap-2">
              <Search size={14} strokeWidth={1.8} className="text-warm-400 flex-shrink-0" />
              <input
                ref={inputRef}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={searchPlaceholder}
                className="flex-1 bg-transparent text-[13px] text-warm-800 placeholder:text-warm-400 outline-none"
              />
            </div>
          )}
          <div ref={listRef} className="overflow-y-auto">
            {filtered.length === 0 ? (
              <div className="px-3 py-4 text-[12.5px] text-warm-500 text-center">
                {emptyMessage}
                {query && <> para "<strong className="text-warm-700">{query}</strong>"</>}
              </div>
            ) : (
              filtered.map((opt, i) => {
                const isSelected = value === opt.value
                const isActive = i === activeIndex
                return (
                  <button
                    key={opt.value}
                    type="button"
                    onClick={() => handleSelect(opt)}
                    onMouseEnter={() => setActiveIndex(i)}
                    disabled={opt.disabled}
                    className={cls(
                      'w-full px-3 py-2 text-left transition flex items-center gap-2',
                      opt.disabled
                        ? 'text-warm-400 cursor-not-allowed'
                        : isSelected
                        ? 'bg-brand-50 text-brand-800'
                        : isActive
                        ? 'bg-warm-50 text-warm-800'
                        : 'text-warm-700 hover:bg-warm-50',
                    )}
                  >
                    <div className="flex-1 min-w-0">
                      <div className={cls('text-[13px] truncate', isSelected && 'font-medium')}>
                        {opt.label}
                      </div>
                      {opt.sublabel && (
                        <div className={cls(
                          'text-[11.5px] truncate',
                          isSelected ? 'text-brand-600' : 'text-warm-500',
                        )}>
                          {opt.sublabel}
                        </div>
                      )}
                    </div>
                    {isSelected && (
                      <Check size={14} strokeWidth={2.2} className="text-brand-700 flex-shrink-0" />
                    )}
                  </button>
                )
              })
            )}
          </div>
        </div>
      )}
    </div>
  )
}
