import { useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { X, CheckCircle2 } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  createProduct, updateProduct,
  type Product, type ProductCategory, type ProductTone,
} from '@/api/inventory'

interface Props {
  open: boolean
  /** Si viene, modo edición. Si no, modo crear. */
  product?: Product
  onClose: () => void
  onSaved: () => void
}

const CATEGORY_OPTIONS: { value: ProductCategory; label: string }[] = [
  { value: 'Hair',        label: 'Cabello' },
  { value: 'Nails',       label: 'Uñas' },
  { value: 'Hairremoval', label: 'Depilación' },
  { value: 'Spa',         label: 'Spa' },
  { value: 'Accessories', label: 'Accesorios' },
]

const TONE_OPTIONS: { value: ProductTone; label: string; cls: string }[] = [
  { value: 'Rose',  label: 'Rosa',     cls: 'bg-[#f5dfd8]' },
  { value: 'Amber', label: 'Ámbar',    cls: 'bg-[#f1e3c1]' },
  { value: 'Sand',  label: 'Arena',    cls: 'bg-[#ece1cf]' },
  { value: 'Olive', label: 'Oliva',    cls: 'bg-[#dde6d4]' },
  { value: 'Wine',  label: 'Vino',     cls: 'bg-[#e8d2d4]' },
  { value: 'Mist',  label: 'Bruma',    cls: 'bg-[#dde7eb]' },
]

/**
 * Modal "Nuevo producto" / "Editar producto". Form básico con todos los
 * campos del Product (sin Stock — el stock se mueve solo vía movimientos).
 */
export function ProductFormModal({ open, product, onClose, onSaved }: Props) {
  const isEdit = !!product
  const [name, setName] = useState('')
  const [brand, setBrand] = useState('')
  const [category, setCategory] = useState<ProductCategory>('Hair')
  const [unit, setUnit] = useState('')
  const [minStock, setMinStock] = useState('5')
  const [cost, setCost] = useState('')
  const [tone, setTone] = useState<ProductTone>('Amber')
  const [error, setError] = useState<string | null>(null)
  const submittingRef = useRef(false)

  useEffect(() => {
    if (open) {
      setName(product?.name ?? '')
      setBrand(product?.brand ?? '')
      setCategory((product?.category as ProductCategory) ?? 'Hair')
      setUnit(product?.unit ?? '')
      setMinStock(String(product?.minStock ?? 5))
      setCost(product ? String(Math.round(product.cost)) : '')
      setTone((product?.tone as ProductTone) ?? 'Amber')
      setError(null)
      submittingRef.current = false
    }
  }, [open, product])

  const mut = useMutation({
    mutationFn: async () => {
      const req = {
        name: name.trim(),
        brand: brand.trim(),
        category,
        unit: unit.trim(),
        minStock: parseInt(minStock, 10) || 0,
        cost: parseInt(cost.replace(/[^0-9]/g, ''), 10) || 0,
        tone,
      }
      if (product) return updateProduct(product.id, req)
      return createProduct(req)
    },
    onSuccess: () => {
      submittingRef.current = false
      onSaved()
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
            <Field label="Categoría" required>
              <select
                value={category}
                onChange={e => setCategory(e.target.value as ProductCategory)}
                className={inputCls}
              >
                {CATEGORY_OPTIONS.map(c => (
                  <option key={c.value} value={c.value}>{c.label}</option>
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

          <Field label="Color visual" hint="cómo se ve el avatar del producto en la tabla">
            <div className="flex gap-2 flex-wrap">
              {TONE_OPTIONS.map(t => (
                <button
                  key={t.value}
                  type="button"
                  onClick={() => setTone(t.value)}
                  className={cls(
                    'h-9 px-3 rounded-lg border flex items-center gap-2 text-[12.5px] transition',
                    tone === t.value
                      ? 'border-brand-700 ring-2 ring-brand-700/15 text-warm-800 font-medium'
                      : 'border-warm-200 text-warm-600 hover:border-warm-300',
                  )}
                >
                  <span className={cls('w-4 h-4 rounded', t.cls)}/>
                  {t.label}
                </button>
              ))}
            </div>
          </Field>

          {!isEdit && (
            <div className="rounded-lg bg-warm-50 border border-warm-150 px-3 py-2.5 text-[12px] text-warm-600">
              El stock arranca en <strong className="text-warm-800">0</strong>. Para cargar inventario inicial,
              registrá una entrada después con motivo "Stock inicial".
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
