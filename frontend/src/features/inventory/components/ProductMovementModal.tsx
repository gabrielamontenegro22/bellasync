import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { X, ArrowRight, RefreshCw, CheckCircle2, Search } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  registerMovement, listProducts,
  type Product, type MovementKind,
} from '@/api/inventory'

interface Props {
  open: boolean
  /** Si viene seteado, abre con ese producto pre-seleccionado. */
  initialProduct?: Product
  onClose: () => void
  /** Recibe descripción del mov para que el padre muestre toast. */
  onSaved: (description: string) => void
}

/**
 * Modal "Registrar movimiento" — espejo del MovementModal del mockup
 * inventory.jsx. 3 tabs (Entrada/Salida/Ajuste), selector de producto
 * con búsqueda, cantidad con +/-, motivos predefinidos y notas.
 *
 * Convención del Qty según el tipo:
 *   - Inflow / Outflow: cantidad delta a sumar/restar.
 *   - Adjustment: NUEVO stock total (no el delta).
 *
 * El backend valida que para Outflow no se exceda el stock y devuelve
 * un mensaje accionable que mostramos al usuario.
 */
export function ProductMovementModal({ open, initialProduct, onClose, onSaved }: Props) {
  const [tab, setTab] = useState<MovementKind>('Inflow')
  const [productId, setProductId] = useState<string>(initialProduct?.id ?? '')
  const [productQuery, setProductQuery] = useState<string>(initialProduct?.name ?? '')
  const [picking, setPicking] = useState(false)
  const [qty, setQty] = useState('')
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const submittingRef = useRef(false)

  // Reset cada vez que se abre.
  useEffect(() => {
    if (open) {
      setTab('Inflow')
      setProductId(initialProduct?.id ?? '')
      setProductQuery(initialProduct?.name ?? '')
      setPicking(false)
      setQty('')
      setReason('')
      setNotes('')
      setError(null)
      submittingRef.current = false
    }
  }, [open, initialProduct])

  // Búsqueda de productos para el picker. Solo busca si hay >=2 caracteres
  // o si está vacío (para mostrar los primeros). Limitado a 8 en la UI.
  const { data: searchResults = [] } = useQuery({
    queryKey: ['inventoryPicker', productQuery],
    queryFn: () => listProducts({ query: productQuery || undefined }),
    enabled: picking,
  })

  const selected = searchResults.find(p => p.id === productId)
    ?? (initialProduct && initialProduct.id === productId ? initialProduct : undefined)

  const REASONS: Record<MovementKind, string[]> = {
    Inflow:     ['Compra a proveedor', 'Devolución de cliente', 'Transferencia entre sedes', 'Stock inicial'],
    Outflow:    ['Consumo en servicio', 'Merma / daño', 'Uso interno (capacitación)', 'Devolución a proveedor'],
    Adjustment: ['Ajuste por inventario físico', 'Corrección de error de captura', 'Vencimiento de producto', 'Otro'],
  }

  const mut = useMutation({
    mutationFn: registerMovement,
    onSuccess: () => {
      submittingRef.current = false
      // Toast con verbo + producto (espeja la convención del mockup).
      const verb = tab === 'Inflow' ? 'Entrada'
        : tab === 'Outflow' ? 'Salida'
        : 'Ajuste'
      const prodName = selected?.name ?? 'producto'
      onSaved(`${verb} registrada · ${prodName}`)
    },
    onError: (e) => {
      submittingRef.current = false
      setError(extractApiError(e, 'No se pudo registrar el movimiento.'))
    },
  })

  if (!open) return null

  const qtyNum = parseInt(qty.replace(/[^0-9]/g, ''), 10) || 0
  const canSubmit = !!productId && !!reason && (qtyNum > 0 || (tab === 'Adjustment' && qty.trim() !== ''))
    && !mut.isPending

  const handleSubmit = () => {
    if (!canSubmit || submittingRef.current) return
    submittingRef.current = true
    setError(null)
    mut.mutate({
      productId,
      kind: tab,
      qty: qtyNum,
      reason,
      notes: notes.trim() || null,
    })
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4 anim-fade"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm"/>
      <div
        className="relative w-full sm:max-w-[560px] max-h-[92vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-pop flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        {/* Head */}
        <div className="px-6 pt-6 pb-4 flex items-start justify-between flex-shrink-0">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium mb-1.5">
              Inventario
            </div>
            <div className="font-serif text-[26px] text-warm-800 leading-tight">
              Registrar movimiento
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center"
            aria-label="Cerrar"
          >
            <X size={18}/>
          </button>
        </div>

        {/* Tabs */}
        <div className="px-6">
          <div className="inline-flex p-1 rounded-xl bg-warm-100">
            {([
              { id: 'Inflow' as const,     label: 'Entrada', icon: ArrowRight, rotate: '-rotate-90' },
              { id: 'Outflow' as const,    label: 'Salida',  icon: ArrowRight, rotate: 'rotate-90' },
              { id: 'Adjustment' as const, label: 'Ajuste',  icon: RefreshCw,  rotate: '' },
            ]).map(t => (
              <button
                key={t.id}
                onClick={() => { setTab(t.id); setReason('') }}
                className={cls(
                  'px-4 py-2 rounded-lg text-[13px] font-medium flex items-center gap-1.5 transition',
                  tab === t.id ? 'bg-white text-warm-800 shadow-soft' : 'text-warm-500 hover:text-warm-700',
                )}
              >
                <t.icon size={13} className={t.rotate}/> {t.label}
              </button>
            ))}
          </div>
        </div>

        {/* Body */}
        <div className="px-6 py-5 space-y-5 overflow-y-auto flex-1">
          {/* Selector de producto */}
          <div>
            <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
              Producto
            </label>
            <div className="mt-1.5 relative">
              <div className="flex items-center gap-2 px-3 py-2.5 rounded-lg border border-warm-200 bg-white focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700">
                <Search size={15} className="text-warm-400"/>
                <input
                  value={selected ? selected.name : productQuery}
                  onChange={e => { setProductQuery(e.target.value); setProductId(''); setPicking(true) }}
                  onFocus={() => setPicking(true)}
                  placeholder="Buscar por nombre o marca…"
                  className="flex-1 outline-none text-[13.5px] text-warm-800 placeholder:text-warm-400 bg-transparent"
                />
                {selected && (
                  <button
                    onClick={() => { setProductId(''); setProductQuery(''); setPicking(true) }}
                    className="text-warm-400 hover:text-warm-700"
                  >
                    <X size={14}/>
                  </button>
                )}
              </div>
              {picking && !selected && searchResults.length > 0 && (
                <div className="absolute left-0 right-0 mt-1.5 rounded-xl bg-white border border-warm-150 shadow-pop max-h-64 overflow-y-auto z-10">
                  {searchResults.slice(0, 8).map(p => (
                    <button
                      key={p.id}
                      onClick={() => {
                        setProductId(p.id)
                        setProductQuery(p.name)
                        setPicking(false)
                      }}
                      className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-warm-50 text-left"
                    >
                      <div className="w-8 h-8 rounded-md bg-warm-100 text-warm-600 flex items-center justify-center font-serif text-[12px]">
                        {p.name.split(' ').slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase()}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-[13px] text-warm-800 truncate">{p.name}</div>
                        <div className="text-[11px] text-warm-500">{p.brand} · stock {p.stock}</div>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
            {selected && (
              <div className="mt-2 text-[11.5px] text-warm-500">
                Stock actual: <strong className="text-warm-700">{selected.stock} {selected.unit}</strong> · mínimo {selected.minStock}
              </div>
            )}
          </div>

          {/* Cantidad + Fecha (display) en 2 columnas, igual al mockup */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
                {tab === 'Adjustment' ? 'Nuevo stock total' : 'Cantidad'}
              </label>
              <div className="mt-1.5 flex items-center rounded-lg border border-warm-200 bg-white focus-within:ring-2 focus-within:ring-brand-700/15 focus-within:border-brand-700 overflow-hidden">
                {tab !== 'Adjustment' && (
                  <button
                    type="button"
                    onClick={() => setQty(q => String(Math.max(0, (parseInt(q, 10) || 0) - 1)))}
                    className="px-3 py-2.5 text-warm-500 hover:bg-warm-50"
                  >
                    −
                  </button>
                )}
                <input
                  type="text"
                  inputMode="numeric"
                  value={qty}
                  onChange={e => setQty(e.target.value.replace(/[^0-9]/g, ''))}
                  placeholder="0"
                  className="flex-1 px-3 py-2.5 outline-none text-[14px] text-warm-800 text-center tabular-nums"
                />
                {tab !== 'Adjustment' && (
                  <button
                    type="button"
                    onClick={() => setQty(q => String((parseInt(q, 10) || 0) + 1))}
                    className="px-3 py-2.5 text-warm-500 hover:bg-warm-50"
                  >
                    +
                  </button>
                )}
              </div>
              {/* Hint específico para "marcar 0 en inventario físico" — el caso
                  que la admin pidió clarificar. Solo se muestra en Ajuste
                  cuando la cantidad escrita es exactamente 0. */}
              {tab === 'Adjustment' && qty === '0' && (
                <p className="text-[11px] text-gold-600 mt-1.5">
                  ⚠️ El stock quedará en 0 (Agotado).
                </p>
              )}
            </div>
            <div>
              <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
                Fecha
              </label>
              <div className="mt-1.5 flex items-center gap-2 px-3 py-2.5 rounded-lg border border-warm-200 bg-warm-50/50">
                <span className="text-[13.5px] text-warm-700">
                  {new Date().toLocaleDateString('es-CO', {
                    day: 'numeric', month: 'short', year: 'numeric',
                  })}
                </span>
              </div>
            </div>
          </div>

          {/* Motivo */}
          <div>
            <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
              Motivo
            </label>
            <div className="mt-1.5 grid grid-cols-2 gap-2">
              {REASONS[tab].map(r => (
                <button
                  key={r}
                  onClick={() => setReason(r)}
                  className={cls(
                    'px-3 py-2.5 rounded-lg border text-[12.5px] text-left transition',
                    reason === r
                      ? 'bg-brand-50 border-brand-700 text-brand-800 ring-2 ring-brand-700/15'
                      : 'bg-white border-warm-200 text-warm-700 hover:border-warm-300',
                  )}
                >
                  {r}
                </button>
              ))}
            </div>
          </div>

          {/* Notas */}
          <div>
            <label className="text-[11.5px] tracking-[0.12em] uppercase text-warm-500 font-medium">
              Notas (opcional)
            </label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              placeholder={tab === 'Adjustment'
                ? 'Ej. Ajuste tras inventario físico del 1 may.'
                : 'Comentario interno…'}
              className="mt-1.5 w-full px-3 py-2.5 rounded-lg border border-warm-200 bg-white text-[13px] text-warm-800 placeholder:text-warm-400 outline-none focus:ring-2 focus:ring-brand-700/15 focus:border-brand-700 resize-none"
            />
          </div>

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-warm-150 flex items-center justify-end gap-2.5 bg-warm-50/50 flex-shrink-0">
          <button
            onClick={onClose}
            className="px-4 py-2.5 rounded-lg text-warm-600 hover:bg-warm-100 text-[13.5px] font-medium"
          >
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={!canSubmit}
            className={cls(
              'px-5 py-2.5 rounded-lg text-[13.5px] font-medium flex items-center gap-1.5 transition',
              canSubmit
                ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft'
                : 'bg-warm-200 text-warm-400 cursor-not-allowed',
            )}
          >
            <CheckCircle2 size={14}/>
            {mut.isPending ? 'Guardando…' : 'Guardar movimiento'}
          </button>
        </div>
      </div>
    </div>
  )
}
