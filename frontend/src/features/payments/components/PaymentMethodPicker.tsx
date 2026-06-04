import { Banknote, CreditCard, Smartphone } from 'lucide-react'
import { cls } from '@/lib/cls'
import { SearchablePicker } from '@/components/ui'
import type { PaymentMethod } from '@/api/payments'
import { CARD_BRANDS, TRANSFER_PROVIDERS } from '../paymentCatalog'

interface Props {
  method: PaymentMethod
  provider: string | null
  onChange: (method: PaymentMethod, provider: string | null) => void
  /**
   * Si querés esconder "Otro" — útil en formularios donde forzamos al
   * usuario a elegir entre los 3 principales. Por default lo dejamos
   * visible como escape (cheque, divisa, etc.).
   */
  hideOther?: boolean
  /** Margen de error: si una lista tiene más opciones que esto, se vuelve searchable. */
  searchableThreshold?: number
}

/**
 * Selector de método de pago + proveedor.
 *
 * Diseño:
 *  - 3 chips arriba: Efectivo / Transferencia / Tarjeta (+ "Otro" opcional).
 *  - Si Transferencia → debajo aparece el picker de banco/billetera.
 *    Con 12 opciones se vuelve searchable (input con dropdown filtrado).
 *  - Si Tarjeta → debajo aparece picker de marca (Visa/MC/AmEx/Diners) —
 *    son solo 4, queda como chips grandes.
 *  - Si Efectivo u Otro → no se muestra picker secundario.
 *
 * Reglas de UX:
 *  - Cambiar de método resetea el provider (no tiene sentido conservar
 *    "Bancolombia" si pasaste a Tarjeta).
 *  - El threshold default es 6: hasta 6 opciones se ven como chips,
 *    más se vuelven dropdown searchable.
 */
export function PaymentMethodPicker({
  method,
  provider,
  onChange,
  hideOther = false,
  searchableThreshold = 6,
}: Props) {
  const handleMethod = (m: PaymentMethod) => {
    // Reset provider al cambiar de método.
    onChange(m, null)
  }

  return (
    <div className="space-y-3">
      {/* Top: 3 (o 4) chips en grid 2×2 — garantiza que ningún label
          se trunque incluso en modales angostos (max-w-md ≈ 448px). */}
      <div className={cls('grid gap-2', hideOther ? 'grid-cols-3' : 'grid-cols-2')}>
        <MethodChip
          active={method === 'Cash'}
          onClick={() => handleMethod('Cash')}
          icon={<Banknote size={16} strokeWidth={1.8} />}
          label="Efectivo"
        />
        <MethodChip
          active={method === 'Transfer'}
          onClick={() => handleMethod('Transfer')}
          icon={<Smartphone size={16} strokeWidth={1.8} />}
          label="Transferencia"
        />
        <MethodChip
          active={method === 'Card'}
          onClick={() => handleMethod('Card')}
          icon={<CreditCard size={16} strokeWidth={1.8} />}
          label="Tarjeta"
        />
        {!hideOther && (
          <MethodChip
            active={method === 'Other'}
            onClick={() => handleMethod('Other')}
            icon={<span className="text-[14px] leading-none tracking-tighter">•••</span>}
            label="Otro"
          />
        )}
      </div>

      {/* Sub-picker según el método */}
      {method === 'Transfer' && (
        <ProviderPicker
          options={TRANSFER_PROVIDERS}
          value={provider}
          onChange={(p) => onChange('Transfer', p)}
          placeholder="¿De qué banco viene la plata?"
          searchableThreshold={searchableThreshold}
          searchPlaceholder="Buscar banco o billetera…"
        />
      )}

      {method === 'Card' && (
        <ProviderPicker
          options={CARD_BRANDS}
          value={provider}
          onChange={(p) => onChange('Card', p)}
          placeholder="¿Qué marca de tarjeta?"
          searchableThreshold={searchableThreshold}
          searchPlaceholder="Buscar marca…"
        />
      )}
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Chip principal (los 3-4 métodos arriba)
// ───────────────────────────────────────────────────────────────────────

function MethodChip({
  active, onClick, icon, label,
}: {
  active: boolean
  onClick: () => void
  icon: React.ReactNode
  label: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cls(
        'flex items-center justify-center gap-2 px-3 py-3 rounded-xl',
        'text-[13px] font-medium border transition',
        active
          // Estilo soft: tinte brand suave en vez de negro pleno.
          // Combina con el resto del modal (TOTAL pill, botón principal)
          // y deja el chip claro sin ser estridente.
          ? 'bg-brand-50 text-brand-800 border-brand-300 ring-2 ring-offset-1 ring-brand-100'
          : 'bg-white text-warm-700 border-warm-200 hover:border-warm-300 hover:bg-warm-50',
      )}
    >
      <span className={active ? 'text-brand-700' : 'text-warm-400'}>{icon}</span>
      <span className="whitespace-nowrap">{label}</span>
    </button>
  )
}

// ───────────────────────────────────────────────────────────────────────
// Provider picker:
//   - <= threshold → chips
//   - > threshold  → input searchable con dropdown filtrado
// ───────────────────────────────────────────────────────────────────────

function ProviderPicker({
  options,
  value,
  onChange,
  placeholder,
  searchableThreshold,
  searchPlaceholder,
}: {
  options: string[]
  value: string | null
  onChange: (provider: string | null) => void
  placeholder: string
  searchableThreshold: number
  searchPlaceholder: string
}) {
  // Hasta el threshold mostramos chips visibles (≤6 = mejor que dropdown).
  // Por encima delegamos al SearchablePicker compartido — mismo look del
  // resto de los selects de la app.
  if (options.length <= searchableThreshold) {
    return (
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
        {options.map((p) => {
          const active = value === p
          return (
            <button
              key={p}
              type="button"
              onClick={() => onChange(active ? null : p)}
              className={cls(
                'px-2.5 py-2 rounded-lg text-[12px] font-medium border transition truncate',
                active
                  ? 'bg-brand-700 text-white border-brand-700'
                  : 'bg-white text-warm-600 border-warm-200 hover:border-warm-300',
              )}
            >
              {p}
            </button>
          )
        })}
      </div>
    )
  }

  return (
    <SearchablePicker
      value={value ?? ''}
      onChange={(v) => onChange(v || null)}
      placeholder={placeholder}
      searchPlaceholder={searchPlaceholder}
      emptyMessage="Sin coincidencias"
      // Threshold bajo para que apenas haya algo se vea el buscador.
      searchableThreshold={searchableThreshold - 1}
      options={options.map((p) => ({ value: p, label: p }))}
    />
  )
}
