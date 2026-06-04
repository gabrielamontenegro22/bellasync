/**
 * Catálogo de bancos/billeteras + marcas de tarjeta que el modelo de
 * pagos usa como "provider". Editá esta lista cuando aparezca un banco
 * que la admin esté usando seguido — el backend acepta cualquier string
 * de hasta 50 chars así que agregar es no-break.
 *
 * Orden: los más usados arriba. El picker es searchable cuando >6
 * elementos, así que mantenelo razonable (12 es el sweet spot).
 */

/**
 * Bancos y billeteras digitales para transferencia. La cuenta de
 * billetera (Nequi, Daviplata) está conceptualmente junta con bancos
 * porque desde el punto de vista del cobro funcionan igual: el cliente
 * te transfiere y aparece un comprobante.
 */
export const TRANSFER_PROVIDERS: string[] = [
  'Bancolombia',
  'Nequi',
  'Daviplata',
  'Davivienda',
  'BBVA',
  'Banco de Bogotá',
  'AV Villas',
  'Banco Popular',
  'Scotiabank Colpatria',
  'Banco Caja Social',
  'Banco Itaú',
  'Banco Agrario',
]

/**
 * Marcas de tarjeta. Lo que viene impreso en el voucher del datáfono.
 */
export const CARD_BRANDS: string[] = [
  'Visa',
  'Mastercard',
  'American Express',
  'Diners Club',
]

/**
 * Brand → color del dot/barra en /caja. Si no está, gris.
 * Mantenemos colores específicos para los 4-5 más comunes que la admin
 * va a ver casi todos los días.
 */
export const PROVIDER_COLORS: Record<string, { dot: string; tone: string }> = {
  'Bancolombia':       { dot: 'bg-[#d4a72b]', tone: 'text-[#7a5e2d] bg-[#f6ecd0]' },
  'Nequi':             { dot: 'bg-[#c026a8]', tone: 'text-[#7a2d6b] bg-[#f3dcee]' },
  'Daviplata':         { dot: 'bg-[#d33333]', tone: 'text-[#9a2828] bg-[#f6dede]' },
  'Davivienda':        { dot: 'bg-[#ed1c27]', tone: 'text-[#8a1a20] bg-[#fde0e2]' },
  'BBVA':              { dot: 'bg-[#1464a5]', tone: 'text-[#0f4475] bg-[#dde9f5]' },
  'Banco de Bogotá':   { dot: 'bg-[#003594]', tone: 'text-[#001f5c] bg-[#dbe2f0]' },
  'Visa':              { dot: 'bg-[#1a1f71]', tone: 'text-[#0f1248] bg-[#dde0ee]' },
  'Mastercard':        { dot: 'bg-[#eb001b]', tone: 'text-[#a30013] bg-[#fcdde1]' },
  'American Express':  { dot: 'bg-[#006fcf]', tone: 'text-[#004e92] bg-[#dceaf6]' },
  'Diners Club':       { dot: 'bg-[#0079be]', tone: 'text-[#005383] bg-[#dceaf3]' },
}

/** Fallback para providers no listados (otro banco, marca rara). */
export const PROVIDER_FALLBACK_COLOR = {
  dot: 'bg-warm-400',
  tone: 'text-warm-600 bg-warm-100',
}
