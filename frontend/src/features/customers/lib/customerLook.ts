/**
 * Helpers visuales del CRM de clientes.
 *
 * Centraliza:
 *  - Asignación determinística de "tono" pastel para el avatar (por id de cliente).
 *  - Mapeo del tono al par de clases Tailwind (bg / fg).
 *  - Mapeo del tag (VIP/Frecuente/Nuevo/Inactivo) a clases.
 *  - Iniciales del nombre.
 *  - Formato relativo de fecha ("hace 3 días", "ayer", "en 5 días").
 *
 * El tono NO se persiste en BD: se deriva del id del cliente para que
 * cada cliente tenga siempre el mismo color en toda la app sin necesidad
 * de un campo extra en backend.
 */

import type { CustomerResponse } from '@/api/customers'

// 8 pares de tonos pastel del mockup `crm.jsx`. Cada par es {bg, fg}
// usando colores arbitrarios Tailwind (`bg-[#...]`) — son específicos
// al diseño del CRM y no merecen su lugar en la paleta global.
const TONE_PALETTE = [
  { name: 'rose',  bg: 'bg-[#f5dfd8]', fg: 'text-[#8a4a3c]' },
  { name: 'amber', bg: 'bg-[#f1e3c1]', fg: 'text-[#7a5b1f]' },
  { name: 'sand',  bg: 'bg-[#ece1cf]', fg: 'text-[#6b563a]' },
  { name: 'olive', bg: 'bg-[#dde6d4]', fg: 'text-[#3f5a37]' },
  { name: 'wine',  bg: 'bg-[#e8d2d4]', fg: 'text-[#7a3d44]' },
  { name: 'mist',  bg: 'bg-[#dde7eb]', fg: 'text-[#3e5664]' },
  { name: 'blush', bg: 'bg-[#f4dde2]', fg: 'text-[#824354]' },
  { name: 'pine',  bg: 'bg-[#d6e3dc]', fg: 'text-[#2f5345]' },
] as const

/**
 * Asigna un tono determinístico por id de cliente.
 * Suma los char codes del id y toma módulo 8 → siempre el mismo color
 * para el mismo cliente, independiente del orden de la lista.
 */
export function toneOf(customerId: string): typeof TONE_PALETTE[number] {
  let acc = 0
  for (let i = 0; i < customerId.length; i++) acc += customerId.charCodeAt(i)
  return TONE_PALETTE[acc % TONE_PALETTE.length]
}

/** Iniciales del nombre (hasta 2). "Mariana López" → "ML". */
export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map(w => w[0]?.toUpperCase() ?? '')
    .join('')
}

/**
 * Mapeo del Tag derivado del backend a clases visuales del badge.
 * Mismas tonalidades que `TAG_BADGE` en `crm.jsx`.
 */
export const TAG_BADGE: Record<CustomerResponse['tag'], { bg: string; fg: string; border: string }> = {
  VIP:       { bg: 'bg-gold-50',  fg: 'text-gold-600',  border: 'border-gold-200' },
  Frecuente: { bg: 'bg-brand-50', fg: 'text-brand-800', border: 'border-brand-100' },
  Nuevo:     { bg: 'bg-warm-100', fg: 'text-warm-600',  border: 'border-warm-200' },
  Inactivo:  { bg: 'bg-warm-100', fg: 'text-warm-500',  border: 'border-warm-200' },
}

/**
 * "hace 3 días", "ayer", "hoy", "en 2 días". Para mostrar última/próxima
 * visita en la lista de clientes.
 *
 * Recibe ISO 8601 (UTC). null → "—".
 */
export function relativeFrom(iso: string | null): string {
  if (!iso) return '—'
  const date = new Date(iso)
  const today = new Date()
  const days = Math.floor((today.getTime() - date.getTime()) / 86_400_000)
  if (days < 0) {
    const ahead = Math.abs(days)
    if (ahead === 1) return 'mañana'
    if (ahead < 7) return `en ${ahead} días`
    if (ahead < 30) return `en ${Math.floor(ahead / 7)} sem`
    return `en ${Math.floor(ahead / 30)} meses`
  }
  if (days === 0) return 'hoy'
  if (days === 1) return 'ayer'
  if (days < 7) return `hace ${days} días`
  if (days < 30) return `hace ${Math.floor(days / 7)} sem`
  if (days < 365) return `hace ${Math.floor(days / 30)} meses`
  return `hace ${Math.floor(days / 365)} años`
}

const MESES_SHORT = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic']
const MESES_LONG  = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
                     'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre']

/** "22 abr 2026" — formato corto para timeline/historial. */
export function fmtMonth(iso: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return `${d.getDate()} ${MESES_SHORT[d.getMonth()]} ${d.getFullYear()}`
}

/** "22 abr · 10:30 am" — fecha + hora 12h para próxima cita. */
export function fmtDateTime(iso: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  const h = d.getHours()
  const m = d.getMinutes()
  const ampm = h >= 12 ? 'pm' : 'am'
  const hh = ((h + 11) % 12) + 1
  return `${d.getDate()} ${MESES_SHORT[d.getMonth()]} · ${hh}:${String(m).padStart(2, '0')} ${ampm}`
}

/**
 * "14 de marzo" — formato largo para cumpleaños (sin año).
 * Recibe "YYYY-MM-DD" (DateOnly del backend) o null.
 */
export function fmtBday(yyyymmdd: string | null): string | null {
  if (!yyyymmdd) return null
  const [, mm, dd] = yyyymmdd.split('-').map(s => Number(s))
  if (!mm || !dd) return null
  return `${dd} de ${MESES_LONG[mm - 1]}`
}

/**
 * Calcula la edad en años a partir de "YYYY-MM-DD". null si no hay fecha.
 */
export function ageFromBday(yyyymmdd: string | null): number | null {
  if (!yyyymmdd) return null
  const [yyyy, mm, dd] = yyyymmdd.split('-').map(s => Number(s))
  if (!yyyy || !mm || !dd) return null
  const today = new Date()
  let age = today.getFullYear() - yyyy
  const monthDiff = today.getMonth() + 1 - mm
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dd)) age--
  return age
}

/** "$160.000" — formato moneda colombiana. */
export function fmtCop(amount: number): string {
  return '$' + Math.round(amount).toLocaleString('es-CO')
}

/**
 * Construye un link de WhatsApp Web: https://wa.me/<phone_sin_caracteres>.
 * Limpia espacios, guiones, paréntesis del phone.
 */
export function whatsappLink(phone: string): string {
  const cleaned = phone.replace(/[^\d+]/g, '').replace(/^\+/, '')
  return `https://wa.me/${cleaned}`
}
