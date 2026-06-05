import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { X, Plus, Pencil, Archive as ArchiveIcon, RotateCcw, Check } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  listCategories, createCategory, updateCategory,
  archiveCategory, reactivateCategory,
  type ProductCategory, type ProductTone,
} from '@/api/inventory'

interface Props {
  open: boolean
  onClose: () => void
  onChanged: () => void
}

const TONE_OPTIONS: { value: ProductTone; label: string; cls: string }[] = [
  { value: 'Rose',  label: 'Rosa',  cls: 'bg-[#f5dfd8]' },
  { value: 'Amber', label: 'Ámbar', cls: 'bg-[#f1e3c1]' },
  { value: 'Sand',  label: 'Arena', cls: 'bg-[#ece1cf]' },
  { value: 'Olive', label: 'Oliva', cls: 'bg-[#dde6d4]' },
  { value: 'Wine',  label: 'Vino',  cls: 'bg-[#e8d2d4]' },
  { value: 'Mist',  label: 'Bruma', cls: 'bg-[#dde7eb]' },
]

/**
 * Modal "Gestionar categorías de inventario". La admin las usa para
 * adaptar el catálogo a su salón:
 *   - Barbería borra Uñas/Spa/Depilación, agrega "Productos para barba".
 *   - Estética renombra "Cabello" → "Color".
 *   - Salón unisex con todo activo deja las 5 default.
 *
 * Reglas:
 *   - Nombre único por tenant (el backend devuelve 409 si se repite).
 *   - No se puede archivar una categoría con productos activos asignados —
 *     el backend devuelve 409 con mensaje accionable que mostramos.
 *   - Archivadas siguen apareciendo si togglea "Mostrar archivadas".
 */
export function CategoriesModal({ open, onClose, onChanged }: Props) {
  const qc = useQueryClient()
  const [showArchived, setShowArchived] = useState(false)
  const [editing, setEditing] = useState<ProductCategory | null>(null)
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data: categories = [], isLoading } = useQuery({
    queryKey: ['inventoryCategoriesManage', showArchived],
    queryFn: () => listCategories(showArchived),
    enabled: open,
  })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['inventoryCategoriesManage'] })
    qc.invalidateQueries({ queryKey: ['inventoryCategories'] })
    onChanged()
  }

  const archiveMut = useMutation({
    mutationFn: archiveCategory,
    onSuccess: () => { setError(null); refresh() },
    onError: (e) => setError(extractApiError(e, 'No se pudo archivar la categoría.')),
  })

  const reactivateMut = useMutation({
    mutationFn: reactivateCategory,
    onSuccess: () => { setError(null); refresh() },
    onError: (e) => setError(extractApiError(e, 'No se pudo reactivar la categoría.')),
  })

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4 anim-fade"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm"/>
      <div
        className="relative w-full sm:max-w-[600px] max-h-[88vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-pop flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        {/* Head */}
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between flex-shrink-0">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium mb-1.5">
              Inventario
            </div>
            <div className="font-serif text-[26px] text-warm-800 leading-tight">
              Categorías del salón
            </div>
            <div className="text-[12.5px] text-warm-500 mt-1.5">
              Adaptá las categorías a los servicios que ofrece tu salón.
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center"
          >
            <X size={18}/>
          </button>
        </div>

        {/* Toolbar */}
        <div className="px-6 py-3 border-b border-warm-150 flex items-center justify-between flex-shrink-0 bg-warm-50/50">
          <label className="text-[12.5px] text-warm-600 flex items-center gap-2">
            <input
              type="checkbox"
              checked={showArchived}
              onChange={e => setShowArchived(e.target.checked)}
              className="rounded"
            />
            Mostrar archivadas
          </label>
          {!creating && !editing && (
            <button
              onClick={() => { setCreating(true); setError(null) }}
              className="px-3 py-1.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[12.5px] font-medium flex items-center gap-1.5"
            >
              <Plus size={13}/> Nueva categoría
            </button>
          )}
        </div>

        {/* Body */}
        <div className="overflow-y-auto flex-1 px-6 py-4 space-y-2">
          {/* Form inline crear */}
          {creating && (
            <CategoryEditor
              initial={null}
              onCancel={() => { setCreating(false); setError(null) }}
              onSaved={() => { setCreating(false); setError(null); refresh() }}
              onError={setError}
            />
          )}

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}

          {isLoading && (
            <div className="text-center py-8 text-[13px] text-warm-500">Cargando…</div>
          )}

          {!isLoading && categories.length === 0 && !creating && (
            <div className="text-center py-10">
              <div className="text-[13.5px] text-warm-600">No hay categorías.</div>
              <button
                onClick={() => setCreating(true)}
                className="mt-3 text-[13px] text-brand-700 font-medium hover:underline"
              >
                Crear la primera →
              </button>
            </div>
          )}

          {categories.map(c => {
            const isEditing = editing?.id === c.id
            if (isEditing) {
              return (
                <CategoryEditor
                  key={c.id}
                  initial={c}
                  onCancel={() => { setEditing(null); setError(null) }}
                  onSaved={() => { setEditing(null); setError(null); refresh() }}
                  onError={setError}
                />
              )
            }
            return (
              <CategoryRow
                key={c.id}
                category={c}
                disabled={archiveMut.isPending || reactivateMut.isPending}
                onEdit={() => { setEditing(c); setError(null) }}
                onArchive={() => archiveMut.mutate(c.id)}
                onReactivate={() => reactivateMut.mutate(c.id)}
              />
            )
          })}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-warm-150 bg-warm-50/50 flex justify-end flex-shrink-0">
          <button
            onClick={onClose}
            className="px-4 py-2.5 rounded-lg text-warm-600 hover:bg-warm-100 text-[13.5px] font-medium"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────
// Row de categoría (modo visualización)
// ─────────────────────────────────────────────────────────────────

function CategoryRow({
  category, disabled, onEdit, onArchive, onReactivate,
}: {
  category: ProductCategory
  disabled: boolean
  onEdit: () => void
  onArchive: () => void
  onReactivate: () => void
}) {
  const tone = TONE_OPTIONS.find(t => t.value === category.tone)
  const blockedArchive = category.isActive && category.activeProductsCount > 0

  return (
    <div className={cls(
      'rounded-xl border px-4 py-3 flex items-center gap-3',
      category.isActive ? 'border-warm-150 bg-white' : 'border-warm-150 bg-warm-50/60 opacity-70',
    )}>
      <div className={cls('w-8 h-8 rounded-lg flex-shrink-0', tone?.cls ?? 'bg-warm-200')}/>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <div className="text-[14px] font-medium text-warm-800 truncate">{category.name}</div>
          {!category.isActive && (
            <span className="text-[10.5px] tracking-[0.12em] uppercase font-semibold text-warm-400 bg-warm-100 px-1.5 py-0.5 rounded">
              Archivada
            </span>
          )}
        </div>
        <div className="text-[11.5px] text-warm-500 mt-0.5">
          {category.activeProductsCount} {category.activeProductsCount === 1 ? 'producto activo' : 'productos activos'}
        </div>
      </div>
      <div className="flex items-center gap-1">
        {category.isActive ? (
          <>
            <button
              onClick={onEdit}
              disabled={disabled}
              title="Editar"
              className="p-2 rounded-md text-warm-500 hover:bg-warm-100 hover:text-warm-700"
            >
              <Pencil size={14}/>
            </button>
            <button
              onClick={onArchive}
              disabled={disabled || blockedArchive}
              title={blockedArchive
                ? 'No se puede archivar: tiene productos activos. Re-categorizalos primero.'
                : 'Archivar'}
              className={cls(
                'p-2 rounded-md',
                blockedArchive
                  ? 'text-warm-300 cursor-not-allowed'
                  : 'text-warm-500 hover:bg-warm-100 hover:text-warm-700',
              )}
            >
              <ArchiveIcon size={14}/>
            </button>
          </>
        ) : (
          <button
            onClick={onReactivate}
            disabled={disabled}
            title="Reactivar"
            className="p-2 rounded-md text-warm-500 hover:bg-warm-100 hover:text-warm-700"
          >
            <RotateCcw size={14}/>
          </button>
        )}
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────
// Editor inline (crear o editar)
// ─────────────────────────────────────────────────────────────────

function CategoryEditor({
  initial, onCancel, onSaved, onError,
}: {
  initial: ProductCategory | null
  onCancel: () => void
  onSaved: () => void
  onError: (msg: string) => void
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [tone, setTone] = useState<ProductTone>(initial?.tone ?? 'Olive')

  const mut = useMutation({
    mutationFn: async () => {
      const req = { name: name.trim(), tone }
      if (initial) return updateCategory(initial.id, req)
      return createCategory(req)
    },
    onSuccess: onSaved,
    onError: (e) => onError(extractApiError(e, 'No se pudo guardar la categoría.')),
  })

  const canSubmit = name.trim().length > 0 && !mut.isPending

  return (
    <div className="rounded-xl border border-brand-200 bg-brand-50/40 px-4 py-3.5 space-y-3">
      <div className="text-[11.5px] tracking-[0.12em] uppercase text-brand-700 font-semibold">
        {initial ? 'Editar categoría' : 'Nueva categoría'}
      </div>

      <div>
        <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium block mb-1">
          Nombre
        </label>
        <input
          autoFocus
          value={name}
          onChange={e => setName(e.target.value)}
          placeholder="Ej: Pestañas y cejas"
          maxLength={60}
          className="w-full px-3 py-2 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none"
        />
      </div>

      <div>
        <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium block mb-1.5">
          Color
        </label>
        <div className="flex gap-1.5 flex-wrap">
          {TONE_OPTIONS.map(t => (
            <button
              key={t.value}
              type="button"
              onClick={() => setTone(t.value)}
              className={cls(
                'h-9 px-3 rounded-lg border flex items-center gap-1.5 text-[12px] transition',
                tone === t.value
                  ? 'border-brand-700 ring-2 ring-brand-700/15 text-warm-800 font-medium'
                  : 'border-warm-200 text-warm-600 hover:border-warm-300',
              )}
            >
              <span className={cls('w-3.5 h-3.5 rounded', t.cls)}/>
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <div className="flex justify-end gap-2 pt-1">
        <button
          onClick={onCancel}
          className="px-3 py-1.5 rounded-lg text-warm-600 hover:bg-warm-100 text-[12.5px] font-medium"
        >
          Cancelar
        </button>
        <button
          onClick={() => mut.mutate()}
          disabled={!canSubmit}
          className={cls(
            'px-4 py-1.5 rounded-lg text-[12.5px] font-medium flex items-center gap-1.5',
            canSubmit
              ? 'bg-brand-700 hover:bg-brand-800 text-white'
              : 'bg-warm-200 text-warm-400 cursor-not-allowed',
          )}
        >
          <Check size={13}/>
          {mut.isPending ? 'Guardando…' : (initial ? 'Guardar' : 'Crear')}
        </button>
      </div>
    </div>
  )
}
