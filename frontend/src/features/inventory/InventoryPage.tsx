import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box, AlertTriangle, AlertCircle, Wallet, Plus, Search, Tag,
  Pencil, Activity, MoreHorizontal, Archive as ArchiveIcon,
} from 'lucide-react'
import { useAuth, usePermissions } from '@/features/auth/useAuth'
import { cls } from '@/lib/cls'
import { fmtCop } from '@/features/customers/lib/customerLook'
import {
  listProducts, getInventorySummary, archiveProduct, listCategories,
  type Product, type ProductStatus, type ProductTone,
} from '@/api/inventory'
import { extractApiError } from '@/lib/extractApiError'
import { ProductMovementModal } from './components/ProductMovementModal'
import { ProductFormModal } from './components/ProductFormModal'
import { ProductMovementsModal } from './components/ProductMovementsModal'
import { CategoriesModal } from './components/CategoriesModal'

/**
 * /inventario — página principal del módulo. Espeja el mockup
 * Inventario.html/inventory.jsx:
 *  - Header con tenantName + título + counts inline
 *  - 4 KPI cards (total, valor, low, agotados)
 *  - Alert banner amarillo si hay productos con stock bajo
 *  - Filtros: categorías pills + status chips + búsqueda
 *  - Tabla con avatar + datos + barra progreso + valor total + menú
 *  - Modal "Registrar movimiento" con tabs Entrada/Salida/Ajuste
 *  - Modal "Nuevo producto" / "Editar producto"
 *  - FAB mobile
 *
 * Permisos:
 *  - GET (lectura): admin + recepción (la página renderiza para ambos).
 *  - Mutations (crear, editar, archivar, movimientos): admin siempre,
 *    recepción solo con canEditInventory. Los botones se ocultan
 *    server-driven; backend igual rechaza 403 si la UI se bypassa.
 */

// ─────────────────────────────────────────────────────────────────
// Constantes visuales
// ─────────────────────────────────────────────────────────────────

// Map de tone → clases tailwind para el avatar (espeja TONES del mockup).
const TONE_CLS: Record<ProductTone, { bg: string; fg: string }> = {
  Rose:  { bg: 'bg-[#f5dfd8]', fg: 'text-[#8a4a3c]' },
  Amber: { bg: 'bg-[#f1e3c1]', fg: 'text-[#7a5b1f]' },
  Sand:  { bg: 'bg-[#ece1cf]', fg: 'text-[#6b563a]' },
  Olive: { bg: 'bg-[#dde6d4]', fg: 'text-[#3f5a37]' },
  Wine:  { bg: 'bg-[#e8d2d4]', fg: 'text-[#7a3d44]' },
  Mist:  { bg: 'bg-[#dde7eb]', fg: 'text-[#3e5664]' },
}

const STATUS_LABEL: Record<ProductStatus, { label: string; dot: string; text: string }> = {
  ok:   { label: 'OK',         dot: 'bg-brand-500', text: 'text-brand-700' },
  warn: { label: 'Limítrofe',  dot: 'bg-gold-400',  text: 'text-gold-600'  },
  low:  { label: 'Stock bajo', dot: 'bg-gold-500',  text: 'text-gold-600'  },
  out:  { label: 'Agotado',    dot: 'bg-terra-500', text: 'text-terra-500' },
}

const initialsOf = (name: string) =>
  name.split(' ').slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase()

const fmtRelativeDays = (iso: string | null) => {
  if (!iso) return '—'
  const d = new Date(iso)
  const days = Math.floor((Date.now() - d.getTime()) / 86_400_000)
  return days < 1 ? 'hoy' : `hace ${days} ${days === 1 ? 'día' : 'días'}`
}

const fmtDate = (iso: string | null) => {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('es-CO', {
    day: 'numeric', month: 'short', year: 'numeric',
  })
}

// ─────────────────────────────────────────────────────────────────
// Página
// ─────────────────────────────────────────────────────────────────

export function InventoryPage() {
  const { user } = useAuth()
  const { canEditInventory } = usePermissions()
  const qc = useQueryClient()

  // categoryId='all' = sin filtro. Cuando la admin pickea una específica
  // guardamos su Guid acá para pasarlo al backend.
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [statusFilter, setStatusFilter] = useState<'all' | ProductStatus | 'ok' | 'low' | 'out'>('all')
  const [query, setQuery] = useState('')
  const [bannerOpen, setBannerOpen] = useState(true)

  // Modal de movimiento. Si initialProduct está seteado, abre pre-seleccionando ese.
  const [movementModal, setMovementModal] = useState<{ initialProduct?: Product } | null>(null)
  // Modal de form (nuevo o editar). product=undefined → crear; con product → editar.
  const [formModal, setFormModal] = useState<{ product?: Product } | null>(null)
  // Modal de historial.
  const [historyModal, setHistoryModal] = useState<Product | null>(null)
  // Modal de gestión de categorías custom del salón.
  const [categoriesOpen, setCategoriesOpen] = useState(false)

  // Categorías activas del tenant. La admin las gestiona vía CategoriesModal.
  const { data: categories = [] } = useQuery({
    queryKey: ['inventoryCategories'],
    queryFn: () => listCategories(false),
  })

  const { data: products = [], isLoading } = useQuery({
    queryKey: ['inventoryProducts', categoryFilter, statusFilter, query],
    queryFn: () => listProducts({
      categoryId: categoryFilter === 'all' ? undefined : categoryFilter,
      status: statusFilter,
      query: query || undefined,
    }),
  })

  const { data: summary } = useQuery({
    queryKey: ['inventorySummary'],
    queryFn: getInventorySummary,
  })

  const refreshAll = () => {
    qc.invalidateQueries({ queryKey: ['inventoryProducts'] })
    qc.invalidateQueries({ queryKey: ['inventorySummary'] })
    qc.invalidateQueries({ queryKey: ['inventoryCategories'] })
  }

  const totalValueVisible = useMemo(
    () => products.reduce((acc, p) => acc + p.stock * p.cost, 0),
    [products]
  )

  const handleArchive = async (p: Product) => {
    if (!confirm(`¿Archivar "${p.name}"? Sus movimientos históricos se mantienen.`)) return
    try {
      await archiveProduct(p.id)
      refreshAll()
    } catch (e) {
      alert(extractApiError(e, 'No se pudo archivar el producto.'))
    }
  }

  return (
    <div className="flex-1 min-w-0 bg-warm-50">
      {/* ─── Header ─── */}
      <header className="bg-white border-b border-warm-150 px-6 lg:px-10 py-6 flex items-start gap-4">
        <div className="flex-1 min-w-0">
          <div className="text-[11.5px] tracking-[0.18em] uppercase text-warm-400 font-medium mb-1.5">
            {user?.tenantName ?? 'Salón'}
          </div>
          <h1 className="font-serif text-[40px] lg:text-[48px] leading-[1] tracking-tight text-warm-800">
            Inventario
          </h1>
          <div className="mt-2.5 text-[14px] text-warm-500 flex items-center gap-x-4 gap-y-1 flex-wrap">
            <span><strong className="font-semibold text-warm-700">{summary?.totalProducts ?? 0}</strong> productos</span>
            {(summary?.lowStockCount ?? 0) > 0 && (
              <>
                <span className="text-warm-300">·</span>
                <span className="flex items-center gap-1.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-gold-500"/>
                  <strong className="font-semibold text-gold-600">{summary?.lowStockCount}</strong> con stock bajo
                </span>
              </>
            )}
            {(summary?.outOfStockCount ?? 0) > 0 && (
              <>
                <span className="text-warm-300">·</span>
                <span className="flex items-center gap-1.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-terra-500"/>
                  <strong className="font-semibold text-terra-500">{summary?.outOfStockCount}</strong> agotados
                </span>
              </>
            )}
          </div>
        </div>

        {canEditInventory && (
          <div className="hidden md:flex items-center gap-2.5">
            <button
              onClick={() => setCategoriesOpen(true)}
              className="px-3.5 py-2.5 rounded-lg border border-warm-200 text-warm-700 hover:bg-warm-50 text-[13.5px] font-medium flex items-center gap-1.5"
              title="Crear, renombrar o archivar categorías de productos de tu salón"
            >
              <Tag size={15}/> Categorías
            </button>
            <button
              onClick={() => setFormModal({})}
              disabled={categories.length === 0}
              title={categories.length === 0 ? 'Primero creá al menos una categoría' : undefined}
              className={cls(
                'px-3.5 py-2.5 rounded-lg border text-[13.5px] font-medium flex items-center gap-1.5',
                categories.length === 0
                  ? 'border-warm-200 text-warm-400 cursor-not-allowed'
                  : 'border-warm-200 text-warm-700 hover:bg-warm-50',
              )}
            >
              <Plus size={15}/> Nuevo producto
            </button>
            <button
              onClick={() => setMovementModal({})}
              className="px-4 py-2.5 rounded-lg bg-brand-700 hover:bg-brand-800 text-white text-[13.5px] font-medium flex items-center gap-1.5 shadow-soft"
            >
              <Plus size={15}/> Registrar movimiento
            </button>
          </div>
        )}
      </header>

      {/* ─── Alert banner ─── */}
      {bannerOpen && summary && summary.lowStockCount > 0 && (
        <div className="mx-6 lg:mx-10 mt-5 rounded-xl border border-gold-200 bg-gold-50 px-5 py-3.5 flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-white border border-gold-200 text-gold-600 flex items-center justify-center flex-shrink-0">
            <AlertTriangle size={15}/>
          </div>
          <div className="flex-1 text-[13.5px] text-warm-700 leading-snug">
            <strong className="font-semibold text-gold-600">{summary.lowStockCount} productos</strong> por debajo de su stock mínimo.
            <button
              onClick={() => setStatusFilter('low')}
              className="ml-2 text-brand-700 font-medium hover:underline"
            >
              Ver alertas →
            </button>
          </div>
          <button onClick={() => setBannerOpen(false)} className="p-1 text-warm-400 hover:text-warm-700">×</button>
        </div>
      )}

      {/* ─── KPIs ─── */}
      <div className="px-6 lg:px-10 mt-6 grid grid-cols-2 lg:grid-cols-4 gap-4">
        <Kpi label="Total productos" value={String(summary?.totalProducts ?? 0)} sub="en catálogo" icon={Box}/>
        <Kpi label="Valor del inventario" value={fmtCop(summary?.totalValueCop ?? 0)} sub="al costo de compra" icon={Wallet} tone="brand"/>
        <Kpi label="Stock bajo" value={String(summary?.lowStockCount ?? 0)} sub="por debajo del mínimo" tone="gold" icon={AlertTriangle}/>
        <Kpi label="Agotados" value={String(summary?.outOfStockCount ?? 0)} sub="reposición urgente" tone="terra" icon={AlertCircle}/>
      </div>

      {/* ─── Filtros ─── */}
      <div className="px-6 lg:px-10 mt-8">
        <div className="flex flex-col lg:flex-row lg:items-center gap-4">
          <div className="flex items-center gap-1 overflow-x-auto -mx-1 px-1">
            <button
              onClick={() => setCategoryFilter('all')}
              className={cls(
                'px-3.5 py-2 rounded-full text-[13px] whitespace-nowrap transition',
                categoryFilter === 'all' ? 'bg-warm-800 text-white' : 'text-warm-600 hover:bg-warm-100',
              )}
            >
              Todos
            </button>
            {categories.map(c => (
              <button
                key={c.id}
                onClick={() => setCategoryFilter(c.id)}
                className={cls(
                  'px-3.5 py-2 rounded-full text-[13px] whitespace-nowrap transition',
                  categoryFilter === c.id ? 'bg-warm-800 text-white' : 'text-warm-600 hover:bg-warm-100',
                )}
              >
                {c.name}
              </button>
            ))}
          </div>

          <div className="hidden lg:block w-px h-6 bg-warm-200 mx-1"/>

          <div className="flex items-center gap-2 overflow-x-auto -mx-1 px-1">
            {([
              { id: 'all', label: 'Todos',     count: summary?.totalProducts ?? 0, dot: null },
              { id: 'ok',  label: 'OK',        count: summary?.okCount ?? 0,        dot: 'bg-brand-500' },
              { id: 'low', label: 'Stock bajo', count: summary?.lowStockCount ?? 0, dot: 'bg-gold-500' },
              { id: 'out', label: 'Agotados',  count: summary?.outOfStockCount ?? 0, dot: 'bg-terra-500' },
            ] as const).map(s => (
              <button key={s.id} onClick={() => setStatusFilter(s.id as typeof statusFilter)}
                className={cls(
                  'px-3 py-1.5 rounded-full text-[12.5px] whitespace-nowrap flex items-center gap-1.5 border transition',
                  statusFilter === s.id
                    ? 'bg-white border-warm-800 text-warm-800 font-medium'
                    : 'bg-white border-warm-200 text-warm-600 hover:border-warm-300',
                )}>
                {s.dot && <span className={cls('w-1.5 h-1.5 rounded-full', s.dot)}/>}
                {s.label}
                <span className="text-warm-400 tabular-nums">{s.count}</span>
              </button>
            ))}
          </div>

          <div className="lg:ml-auto flex items-center gap-2 w-full lg:w-auto">
            <div className="flex-1 lg:flex-none lg:w-[280px] flex items-center gap-2 px-3 py-2 rounded-lg bg-white border border-warm-200 focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700">
              <Search size={15} className="text-warm-400"/>
              <input
                value={query}
                onChange={e => setQuery(e.target.value)}
                placeholder="Buscar por producto o marca…"
                className="flex-1 outline-none text-[13px] text-warm-800 placeholder:text-warm-400 bg-transparent"
              />
            </div>
          </div>
        </div>
      </div>

      {/* ─── Tabla ─── */}
      <div className="px-6 lg:px-10 mt-5 mb-10">
        {/* overflow-x-auto sin overflow-y explícito provoca que algunos browsers
            (Chrome/Edge) reserven scrollbar vertical "fantasma" cuando el
            contenido es más ancho que el container. Forzamos overflow-y-visible
            para que la tabla nunca scrollee vertical (queremos scroll de
            página, no del card). */}
        <div className="bg-white rounded-2xl border border-warm-150">
          <div className="overflow-x-auto overflow-y-visible">
            <table className="w-full text-[13px]">
              <thead>
                <tr className="bg-warm-50/60 border-b border-warm-150 text-[10.5px] tracking-[0.14em] uppercase text-warm-500">
                  <th className="text-left font-medium pl-6 pr-3 py-3">Producto</th>
                  <th className="text-left font-medium px-3 py-3">Categoría</th>
                  <th className="text-left font-medium px-3 py-3">Stock</th>
                  <th className="text-left font-medium px-3 py-3">Mín.</th>
                  <th className="text-left font-medium px-3 py-3">Estado</th>
                  <th className="text-left font-medium px-3 py-3">Última entrada</th>
                  <th className="text-right font-medium px-3 py-3">Valor total</th>
                  <th className="pr-6 pl-3 py-3"/>
                </tr>
              </thead>
              <tbody>
                {products.map(p => (
                  <ProductRow
                    key={p.id}
                    p={p}
                    canEdit={canEditInventory}
                    onMovement={() => setMovementModal({ initialProduct: p })}
                    onEdit={() => setFormModal({ product: p })}
                    onHistory={() => setHistoryModal(p)}
                    onArchive={() => handleArchive(p)}
                  />
                ))}
              </tbody>
            </table>
          </div>

          {!isLoading && products.length === 0 && (
            <div className="py-16 text-center">
              <div className="text-[14px] text-warm-500">No se encontraron productos con esos filtros.</div>
              <button
                onClick={() => { setCategoryFilter('all'); setStatusFilter('all'); setQuery('') }}
                className="mt-3 text-[13px] text-brand-700 font-medium hover:underline"
              >
                Limpiar filtros
              </button>
            </div>
          )}

          {products.length > 0 && (
            <div className="px-6 py-3.5 border-t border-warm-150 flex items-center justify-between text-[12px] text-warm-500 bg-warm-50/40">
              <span>Mostrando <strong className="text-warm-700">{products.length}</strong> productos</span>
              <span className="tabular-nums">Valor visible: <strong className="text-warm-700">{fmtCop(totalValueVisible)}</strong></span>
            </div>
          )}
        </div>
      </div>

      {/* ─── FAB mobile ─── */}
      {canEditInventory && (
        <button
          onClick={() => setMovementModal({})}
          className="md:hidden fixed bottom-5 right-5 z-40 px-5 py-3.5 rounded-full bg-brand-700 text-white shadow-pop flex items-center gap-2 text-[13.5px] font-medium"
        >
          <Plus size={16}/> Movimiento
        </button>
      )}

      {/* ─── Modales ─── */}
      {movementModal && (
        <ProductMovementModal
          open
          initialProduct={movementModal.initialProduct}
          onClose={() => setMovementModal(null)}
          onSaved={() => { setMovementModal(null); refreshAll() }}
        />
      )}
      {formModal && (
        <ProductFormModal
          open
          product={formModal.product}
          onClose={() => setFormModal(null)}
          onSaved={() => { setFormModal(null); refreshAll() }}
        />
      )}
      {historyModal && (
        <ProductMovementsModal
          open
          product={historyModal}
          onClose={() => setHistoryModal(null)}
        />
      )}
      {categoriesOpen && (
        <CategoriesModal
          open
          onClose={() => setCategoriesOpen(false)}
          onChanged={refreshAll}
        />
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────
// KPI card
// ─────────────────────────────────────────────────────────────────

function Kpi({
  label, value, sub, tone = 'neutral', icon: Icon,
}: {
  label: string
  value: string
  sub: string
  tone?: 'neutral' | 'brand' | 'gold' | 'terra'
  icon: React.ComponentType<{ size?: number; className?: string }>
}) {
  const cardCls = {
    neutral: 'bg-white border-warm-150',
    brand:   'bg-brand-50 border-brand-100',
    gold:    'bg-gold-50 border-gold-200',
    terra:   'bg-terra-100/60 border-terra-300/60',
  }[tone]
  const iconCls = {
    neutral: 'bg-warm-100 text-warm-600',
    brand:   'bg-white text-brand-700 border border-brand-100',
    gold:    'bg-white text-gold-600 border border-gold-200',
    terra:   'bg-white text-terra-500 border border-terra-300/60',
  }[tone]

  return (
    <div className={cls('rounded-xl border p-5 lg:p-6', cardCls)}>
      <div className="flex items-start justify-between gap-4">
        <div className="text-[11.5px] tracking-[0.16em] uppercase text-warm-500 font-medium">{label}</div>
        <div className={cls('w-8 h-8 rounded-lg flex items-center justify-center', iconCls)}>
          <Icon size={16}/>
        </div>
      </div>
      <div className="font-serif text-[36px] lg:text-[40px] leading-none mt-3 text-warm-800 tabular-nums">{value}</div>
      <div className="text-[12px] text-warm-500 mt-2">{sub}</div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────
// Row de producto
// ─────────────────────────────────────────────────────────────────

function ProductRow({
  p, canEdit, onMovement, onEdit, onHistory, onArchive,
}: {
  p: Product
  canEdit: boolean
  onMovement: () => void
  onEdit: () => void
  onHistory: () => void
  onArchive: () => void
}) {
  const tone = TONE_CLS[p.tone] ?? TONE_CLS.Olive
  const status = STATUS_LABEL[p.status as ProductStatus] ?? STATUS_LABEL.ok
  const total = p.stock * p.cost
  const target = Math.max(p.minStock * 2, 1)
  const pct = Math.min(100, Math.round((p.stock / target) * 100))
  const barColor = {
    ok:   'bg-brand-500',
    warn: 'bg-gold-400',
    low:  'bg-gold-500',
    out:  'bg-warm-300',
  }[p.status as ProductStatus] ?? 'bg-warm-300'
  return (
    <tr className={cls(
      'border-b border-warm-100 hover:bg-warm-50/50 transition-colors',
      p.status === 'out' && 'opacity-90',
    )}>
      <td className="py-4 pl-6 pr-3 align-middle">
        <div className="flex items-center gap-3 min-w-0">
          <div className={cls(
            'w-10 h-10 rounded-lg flex items-center justify-center font-serif text-[15px] flex-shrink-0',
            tone.bg, tone.fg,
          )}>
            {initialsOf(p.name)}
          </div>
          <div className="min-w-0">
            <div className="text-[13.5px] font-medium text-warm-800 truncate leading-tight">{p.name}</div>
            <div className="text-[11.5px] text-warm-500 mt-0.5">{p.brand} · {p.unit}</div>
          </div>
        </div>
      </td>
      <td className="py-4 px-3 align-middle">
        <span className="text-[12px] text-warm-600">{p.categoryName || '—'}</span>
      </td>
      <td className="py-4 px-3 align-middle whitespace-nowrap">
        <div className="font-serif text-[20px] tabular-nums text-warm-800 leading-none">{p.stock}</div>
      </td>
      <td className="py-4 px-3 align-middle whitespace-nowrap">
        <div className="text-[13px] tabular-nums text-warm-500">{p.minStock}</div>
      </td>
      <td className="py-4 px-3 align-middle min-w-[180px]">
        <div className="flex items-center gap-3">
          <div className="w-full">
            <div className="h-1.5 rounded-full bg-warm-100 overflow-hidden">
              <div className={cls('h-full rounded-full transition-all', barColor)} style={{ width: pct + '%' }}/>
            </div>
          </div>
          <span className={cls(
            'text-[10.5px] tracking-[0.12em] uppercase font-semibold whitespace-nowrap',
            status.text,
          )}>
            {status.label}
          </span>
        </div>
      </td>
      <td className="py-4 px-3 align-middle whitespace-nowrap">
        <div className="text-[12.5px] text-warm-700 tabular-nums">{fmtDate(p.lastInAt)}</div>
        <div className="text-[11px] text-warm-400 mt-0.5">{fmtRelativeDays(p.lastInAt)}</div>
      </td>
      <td className="py-4 px-3 align-middle text-right whitespace-nowrap min-w-[120px]">
        <div className="text-[13px] tabular-nums text-warm-800 font-medium">{fmtCop(total)}</div>
        <div className="text-[11px] text-warm-400 mt-0.5">{fmtCop(p.cost)} c/u</div>
      </td>
      <td className="py-4 pr-6 pl-3 align-middle">
        <RowMenu
          canEdit={canEdit}
          onMovement={onMovement}
          onEdit={onEdit}
          onHistory={onHistory}
          onArchive={onArchive}
        />
      </td>
    </tr>
  )
}

// ─────────────────────────────────────────────────────────────────
// Menú "···" del row
// ─────────────────────────────────────────────────────────────────

function RowMenu({
  canEdit, onMovement, onEdit, onHistory, onArchive,
}: {
  canEdit: boolean
  onMovement: () => void
  onEdit: () => void
  onHistory: () => void
  onArchive: () => void
}) {
  const [open, setOpen] = useState(false)

  return (
    <div className="relative">
      <button
        onClick={() => setOpen(o => !o)}
        className="p-1.5 rounded-md hover:bg-warm-100 text-warm-500"
      >
        <MoreHorizontal size={16}/>
      </button>
      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)}/>
          <div className="absolute right-0 mt-1.5 w-56 rounded-xl bg-white shadow-pop border border-warm-150 py-1.5 z-50 anim-fade">
            {canEdit && (
              <MenuItem icon={Plus} label="Registrar movimiento"
                onClick={() => { setOpen(false); onMovement() }}/>
            )}
            {canEdit && (
              <MenuItem icon={Pencil} label="Editar producto"
                onClick={() => { setOpen(false); onEdit() }}/>
            )}
            <MenuItem icon={Activity} label="Ver historial"
              onClick={() => { setOpen(false); onHistory() }}/>
            {canEdit && (
              <MenuItem icon={ArchiveIcon} label="Archivar"
                onClick={() => { setOpen(false); onArchive() }}/>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function MenuItem({
  icon: Icon, label, onClick,
}: {
  icon: React.ComponentType<{ size?: number; className?: string }>
  label: string
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      className="w-full flex items-center gap-2.5 px-3 py-2 text-[13px] text-warm-700 hover:bg-warm-50 text-left"
    >
      <Icon size={14} className="text-warm-500"/> {label}
    </button>
  )
}

