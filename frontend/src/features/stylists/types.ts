import type { StylistStatusEnum } from '@/api/stylists'

/**
 * Tonos de avatar del mockup (rose, amber, sage, plum).
 * Cada tono se mapea a un color hex que se persiste en `Stylist.color` del backend.
 *
 * Cuando crea/edita una estilista, la usuaria elige un tono → guardamos el hex.
 * Cuando renderizamos, leemos el hex y elegimos el tono más cercano para la UI.
 */
export type AvatarTone = 'rose' | 'amber' | 'sage' | 'plum'

export interface ToneMeta {
  id: AvatarTone
  /** Color hex que se persiste en el backend cuando se elige este tono. */
  hex: string
  /** Clases Tailwind para fondo + texto del avatar. */
  bg: string
  fg: string
}

export const TONES: ToneMeta[] = [
  { id: 'rose',  hex: '#f3d9dc', bg: 'bg-[#f3d9dc]', fg: 'text-[#7a3d44]' },
  { id: 'amber', hex: '#f0e0c0', bg: 'bg-[#f0e0c0]', fg: 'text-[#7a5e2d]' },
  { id: 'sage',  hex: '#d6e6dd', bg: 'bg-[#d6e6dd]', fg: 'text-[#3d6453]' },
  { id: 'plum',  hex: '#dfd2e3', bg: 'bg-[#dfd2e3]', fg: 'text-[#5b3d6b]' },
]

const TONE_BY_HEX: Record<string, ToneMeta> = TONES.reduce(
  (acc, t) => ({ ...acc, [t.hex.toLowerCase()]: t }),
  {} as Record<string, ToneMeta>,
)

/**
 * Resuelve qué tono usar para renderizar un avatar.
 * Si el `color` del backend matchea uno de los hex de TONES, se usa ese tono.
 * Si no matchea (color custom), se usa el fallback `sage`.
 */
export function resolveTone(color: string | null | undefined): ToneMeta {
  if (!color) return TONES[2] // sage por default
  return TONE_BY_HEX[color.toLowerCase()] ?? TONES[2]
}

/* -------------------------------------------------------------------------- */
/*  Status badges                                                             */
/* -------------------------------------------------------------------------- */

export interface StatusMeta {
  id: StylistStatusEnum
  label: string
  /** Clase Tailwind del puntito. */
  dot: string
  /** Clases del pill (bg + text + ring). */
  pill: string
}

export const STATUS_META: Record<StylistStatusEnum, StatusMeta> = {
  Active: {
    id: 'Active',
    label: 'Activa',
    dot: 'bg-brand-500',
    pill: 'bg-brand-50 text-brand-800 ring-1 ring-brand-200',
  },
  Vacation: {
    id: 'Vacation',
    label: 'Vacaciones',
    dot: 'bg-gold-400',
    pill: 'bg-gold-50 text-gold-600 ring-1 ring-gold-200',
  },
  Inactive: {
    id: 'Inactive',
    label: 'Inactiva',
    dot: 'bg-warm-300',
    pill: 'bg-warm-100 text-warm-500 ring-1 ring-warm-200',
  },
}

/* -------------------------------------------------------------------------- */
/*  Roles sugeridos (string libre, pero mostramos opciones comunes)           */
/* -------------------------------------------------------------------------- */

export const ROLE_SUGGESTIONS = [
  'Estilista',
  'Estilista Senior',
  'Colorista',
  'Manicurista',
  'Maquilladora',
  'Esteticista',
  'Aprendiz',
  'Recepcionista',
] as const

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                   */
/* -------------------------------------------------------------------------- */

/** "Carolina Rodríguez" → "CR" */
export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .join('')
}

/** Formato fecha "abr 2024" en español. */
export function fmtJoinedDate(iso: string | null): string {
  if (!iso) return '—'
  try {
    return new Date(iso)
      .toLocaleDateString('es-CO', { month: 'short', year: 'numeric' })
      .replace('.', '')
  } catch {
    return iso
  }
}

/** Filtros de status para la toolbar. */
export type StatusFilter = 'all' | 'active' | 'vacation' | 'inactive'
