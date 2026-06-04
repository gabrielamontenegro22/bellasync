import type { PaymentMethod } from '@/api/payments'
import { PROVIDER_COLORS, PROVIDER_FALLBACK_COLOR } from './paymentCatalog'

/**
 * Look del chip/badge que pintamos en cualquier tabla o card de pago.
 *
 * Reglas:
 *   - Si hay provider, el label es el banco/marca y los colores son
 *     los oficiales de esa marca (definidos en paymentCatalog). Esto
 *     hace que "Bancolombia" siempre se vea amarillo, "Nequi" siempre
 *     morado, etc. — la admin reconoce el banco de un vistazo.
 *   - Si no hay provider, el label es genérico de la categoría
 *     ("Efectivo", "Transferencia", "Tarjeta", "Otro").
 */
export interface PaymentBadge {
  label: string
  /** Clase de bg + text combinada (Tailwind). */
  className: string
  /** Color del dot opcional para listas. */
  dot: string
}

export function getPaymentBadge(
  method: PaymentMethod | string,
  provider: string | null,
): PaymentBadge {
  if (provider) {
    const color = PROVIDER_COLORS[provider] ?? PROVIDER_FALLBACK_COLOR
    return {
      label: provider,
      className: color.tone,
      dot: color.dot,
    }
  }

  // Fallback genéricos por categoría.
  switch (method) {
    case 'Cash':
      return {
        label: 'Efectivo',
        className: 'text-brand-700 bg-brand-50',
        dot: 'bg-brand-500',
      }
    case 'Transfer':
      return {
        label: 'Transferencia',
        className: 'text-warm-700 bg-warm-100',
        dot: 'bg-warm-500',
      }
    case 'Card':
      return {
        label: 'Tarjeta',
        className: 'text-warm-700 bg-warm-100',
        dot: 'bg-warm-500',
      }
    case 'Other':
    default:
      return {
        label: 'Otro',
        className: 'text-warm-600 bg-warm-100',
        dot: 'bg-warm-400',
      }
  }
}
