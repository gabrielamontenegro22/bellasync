import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { X, CheckCircle2 } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  createProduct, updateProduct, listCategories,
  type Product,
} from '@/api/inventory'

interface Props {
  open: boolean
  /** Si viene, modo edición. Si no, modo crear. */
  product?: Product
  onClose: () => void
  /** Recibe mensaje para toast. */
  onSaved: (description: string) => void
}

/**
 * Modal "Nuevo producto" / "Editar producto". Form básico con todos los
 * campos del Product (sin Stock — el stock se mueve solo vía movimientos).
 */
export function ProductFormModal({ open, product, onClose, onSaved }: Props) {
  const isEdit = !!product
  const [name, setName] = useState('')
  const [brand, setBrand] = useState('')
  const [categoryId, setCategoryId] = useState<string>('')
  const [unit, setUnit] = useState('')
  const [minStock, setMinStock] = useState('5')
  const [cost, setCost] = useState('')
  const [error, setError] = useState<string | null>(null)
  const submittingRef = useRef(false)

  // Cargar categorías activas del tenant para el dropdown.
  const { data: categories = [] } = useQuery({
    queryKey: ['inventoryCategories'],
    queryFn: () => listCategories(false),
    enabled: open,
  })

  useEffect(() => {
    if (open) {
      setName(product?.name ?? '')
      setBrand(product?.brand ?? '')
      // Si editamos, prellenamos la categoría actual. Si creamos, pickeamos
      // la primera activa por default (la admin elige otra si quiere).
      setCategoryId(product?.categoryId ?? (categories[0]?.id ?? ''))
      setUnit(product?.unit ?? '')
      setMinStock(String(product?.minStock ?? 5))
      setCost(product ? String(Math.round(product.cost)) : '')
      setError(null)
      submittingRef.current = false
    }
  }, [open, product, categories])

  const mut = useMutation({
    mutationFn: async () => {
      const req = {
        name: name.trim(),
        brand: brand.trim(),
        categoryId,
        unit: unit.trim(),
        minStock: parseInt(minStock, 10) || 0,
        cost: parseInt(cost.replace(/[^0-9]/g, ''), 10) || 0,
      }
      if (product) return updateProduct(product.id, req)
      return createProduct(req)
    },
    onSuccess: () => {
      submittingRef.current = false
      const verb = isEdit ? 'Producto actualizado' : 'Producto creado'
      onSaved(`${verb} · ${name.trim()}`)
    },
    onError: (e) => {
      submittingRef.current = false
      setError(extractApiError(e, 'No se pudo guardar el producto.'))
    },
  })

  if (!open) return null

  const canSubmit = name.trim().length > 0
    && brand.trim().length > 0
    && unit.trim().length > 0
    && categoryId !== ''
    && !mut.isPending

  const handleSubmit = () => {
    if (!canSubmit || submittingRef.current) return
    submittingRef.current = true
    setError(null)
    mut.mutate()
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4 anim-fade"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm"/>
      <div
        className="relative w-full sm:max-w-[520px] max-h-[92vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-pop flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        <div className="px-6 pt-6 pb-4 flex items-start justify-between flex-shrink-0">
          <div>
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium mb-1.5">
              Inventario
            </div>
            <div className="font-serif text-[26px] text-warm-800 leading-tight">
              {isEdit ? 'Editar producto' : 'Nuevo producto'}
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center"
          >
            <X size={18}/>
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto flex-1">
          <Field label="Nombre" required>
            <input
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="Ej: Tinte Koleston Perfect 7.0"
              className={inputCls}
            />
          </Field>

          <Field label="Marca" required>
            <input
              value={brand}
              onChange={e => setBrand(e.target.value)}
              placeholder="Ej: Wella"
              className={inputCls}
            />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Categoría" required hint="creá/editá desde el botón Categorías">
              <select
                value={categoryId}
                onChange={e => setCategoryId(e.target.value)}
                className={inputCls}
                disabled={categories.length === 0}
              >
                {categories.length === 0 && <option value="">— Sin categorías —</option>}
                {categories.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </Field>
            <Field label="Unidad" required hint="frasco, tubo, 500ml…">
              <input
                value={unit}
                onChange={e => setUnit(e.target.value)}
                placeholder="frasco"
                className={inputCls}
              />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Stock mínimo" hint="alerta cuando baje de acá">
              <input
                type="text"
                inputMode="numeric"
                value={minStock}
                onChange={e => setMinStock(e.target.value.replace(/[^0-9]/g, ''))}
                placeholder="5"
                className={cls(inputCls, 'tabular-nums')}
              />
            </Field>
            <Field label="Costo unitario (COP)" hint="lo que pagaste al proveedor">
              <div className="relative">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-warm-400 text-[14px]">$</span>
                <input
                  type="text"
                  inputMode="numeric"
                  value={cost}
                  onChange={e => setCost(e.target.value.replace(/[^0-9]/g, ''))}
                  placeholder="0"
                  className={cls(inputCls, 'pl-7 tabular-nums')}
                />
              </div>
            </Field>
          </div>

          {/* El color visual del producto ya no se elige acá: ahora se hereda
              de la categoría (cada categoría tiene su color). Si querés cambiar
              cómo se ve un producto en la tabla, cambiá el color de su categoría
              desde el modal Categorías. */}

          {!isEdit && (
            <div className="rounded-lg bg-warm-50 border border-warm-150 px-3 py-2.5 text-[12px] text-warm-600">
              El stock arranca en <strong className="text-warm-800">0</strong>. Para cargar inventario inicial,
              registrá una entrada después con motivo "Stock inicial".
            </div>
          )}

          {/* Edición: aviso de que el stock NO se cambia desde este form. Es
              error común — el user busca cambiarlo acá y no encuentra el
              campo. Le recordamos que para eso está "Registrar movimiento". */}
          {isEdit && product && (
            <div className="rounded-lg bg-warm-50 border border-warm-150 px-3 py-2.5 text-[12px] text-warm-600 flex items-start gap-2">
              <span className="text-warm-400 mt-0.5">ℹ️</span>
              <span>
                El stock actual (<strong className="text-warm-800">{product.stock} {product.unit}</strong>) se cambia
                registrando un movimiento (entrada / salida / ajuste), no desde acá.
              </span>
            </div>
          )}

          {error && (
            <div className="rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
              {error}
            </div>
          )}
        </div>

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
            {mut.isPending ? 'Guardando…' : (isEdit ? 'Guardar cambios' : 'Crear producto')}
          </button>
        </div>
      </div>
    </div>
  )
}

const inputCls =
  'w-full px-3.5 py-2.5 rounded-lg bg-white border border-warm-200 text-[14px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition'

function Field({
  label, hint, required, children,
}: {
  label: string
  hint?: string
  required?: boolean
  children: React.ReactNode
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">
          {label}
          {required && <span className="text-terra-500 ml-1">*</span>}
        </label>
        {hint && <span className="text-[11px] text-warm-400 text-right">{hint}</span>}
      </div>
      {children}
    </div>
  )
}
