import { useQuery } from '@tanstack/react-query'
import { X, ArrowDownRight, ArrowUpRight, RefreshCw } from 'lucide-react'
import { cls } from '@/lib/cls'
import { listMovements, type Product, type MovementKind } from '@/api/inventory'

interface Props {
  open: boolean
  product: Product
  onClose: () => void
}

const KIND_META: Record<MovementKind, { label: string; icon: React.ComponentType<{ size?: number; className?: string }>; cls: string }> = {
  Inflow:     { label: 'Entrada', icon: ArrowDownRight, cls: 'text-brand-700 bg-brand-50' },
  Outflow:    { label: 'Salida',  icon: ArrowUpRight,   cls: 'text-terra-500 bg-terra-100/60' },
  Adjustment: { label: 'Ajuste',  icon: RefreshCw,      cls: 'text-gold-600 bg-gold-50' },
}

const fmtDateTime = (iso: string) => {
  const d = new Date(iso)
  return d.toLocaleString('es-CO', {
    day: 'numeric', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

/**
 * Modal "Ver historial" — lista los movimientos del producto desc por fecha.
 * Solo lectura. Cualquier rol con acceso a /inventario puede ver el historial.
 */
export function ProductMovementsModal({ open, product, onClose }: Props) {
  const { data: movements = [], isLoading } = useQuery({
    queryKey: ['productMovements', product.id],
    queryFn: () => listMovements(product.id),
    enabled: open,
  })

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:justify-center sm:p-4 anim-fade"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-warm-900/40 backdrop-blur-sm"/>
      <div
        className="relative w-full sm:max-w-[640px] max-h-[88vh] bg-white rounded-t-2xl sm:rounded-2xl shadow-pop overflow-hidden anim-pop flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        <div className="px-6 pt-6 pb-4 border-b border-warm-150 flex items-start justify-between flex-shrink-0">
          <div className="min-w-0">
            <div className="text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium mb-1.5">
              Historial de movimientos
            </div>
            <div className="font-serif text-[22px] text-warm-800 leading-tight truncate">
              {product.name}
            </div>
            <div className="text-[12px] text-warm-500 mt-1">
              {product.brand} · stock actual <strong className="text-warm-700">{product.stock}</strong>
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-md hover:bg-warm-100 text-warm-500 flex items-center justify-center flex-shrink-0"
          >
            <X size={18}/>
          </button>
        </div>

        <div className="overflow-y-auto flex-1 px-6 py-4">
          {isLoading ? (
            <div className="text-center py-12 text-[13px] text-warm-500">Cargando…</div>
          ) : movements.length === 0 ? (
            <div className="text-center py-12">
              <div className="text-[13.5px] text-warm-600">Este producto todavía no tiene movimientos.</div>
              <div className="text-[12px] text-warm-400 mt-1">
                Registrá una entrada con motivo "Stock inicial" para arrancar.
              </div>
            </div>
          ) : (
            <ul className="space-y-2.5">
              {movements.map(m => {
                const meta = KIND_META[m.kind] ?? KIND_META.Adjustment
                const delta = m.stockAfter - m.stockBefore
                return (
                  <li key={m.id} className="rounded-xl border border-warm-150 px-4 py-3 flex items-start gap-3">
                    <div className={cls('w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0', meta.cls)}>
                      <meta.icon size={16}/>
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-[13.5px] font-medium text-warm-800">{meta.label}</span>
                        <span className={cls(
                          'text-[11.5px] font-semibold tabular-nums px-1.5 py-0.5 rounded',
                          delta > 0 ? 'bg-brand-50 text-brand-700'
                            : delta < 0 ? 'bg-terra-100/60 text-terra-500'
                            : 'bg-warm-100 text-warm-600',
                        )}>
                          {delta > 0 ? '+' : ''}{delta}
                        </span>
                      </div>
                      <div className="text-[12.5px] text-warm-600 mt-0.5">{m.reason}</div>
                      {m.notes && (
                        <div className="text-[11.5px] text-warm-500 mt-1 italic">"{m.notes}"</div>
                      )}
                      <div className="text-[11px] text-warm-400 mt-1.5 flex items-center gap-2 flex-wrap">
                        <span className="tabular-nums">{fmtDateTime(m.registeredAt)}</span>
                        {m.registeredByUserName && (
                          <>
                            <span>·</span>
                            <span>Por {m.registeredByUserName}</span>
                          </>
                        )}
                        <span>·</span>
                        <span className="tabular-nums">{m.stockBefore} → {m.stockAfter}</span>
                      </div>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        <div className="px-6 py-4 border-t border-warm-150 bg-warm-50/50 flex-shrink-0 flex justify-end">
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
