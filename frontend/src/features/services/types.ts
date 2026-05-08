import type { ServiceCategoryEnum } from '@/api/services'

/**
 * Metadata de UI para cada categoría del backend.
 * - label: nombre legible en español (con tildes y eñes)
 * - gradient: CSS background para la "foto" placeholder de la card
 * - badgeBg / badgeFg: colores del chip de categoría sobre la foto
 */
export interface CategoryMeta {
  id: ServiceCategoryEnum
  label: string
  gradient: string
  badgeBg: string
  badgeFg: string
}

export const CATEGORIES: CategoryMeta[] = [
  {
    id: 'Cabello',
    label: 'Cabello',
    gradient: 'linear-gradient(135deg, #d6bc78 0%, #8f7341 60%, #46423a 100%)',
    badgeBg: 'bg-[#f1e3c1]',
    badgeFg: 'text-[#7a5b1f]',
  },
  {
    id: 'Unas',
    label: 'Uñas',
    gradient: 'linear-gradient(135deg, #f5dfd8 0%, #c9a86a 50%, #8a4a3c 100%)',
    badgeBg: 'bg-[#f5dfd8]',
    badgeFg: 'text-[#8a4a3c]',
  },
  {
    id: 'Estetica',
    label: 'Estética',
    gradient: 'linear-gradient(135deg, #b9ddd2 0%, #2a7064 60%, #0a4842 100%)',
    badgeBg: 'bg-[#dde6d4]',
    badgeFg: 'text-[#3f5a37]',
  },
  {
    id: 'Maquillaje',
    label: 'Maquillaje',
    gradient: 'linear-gradient(135deg, #faf8f5 0%, #f4dde2 50%, #c9a86a 100%)',
    badgeBg: 'bg-[#f4dde2]',
    badgeFg: 'text-[#824354]',
  },
  {
    id: 'Depilacion',
    label: 'Depilación',
    gradient: 'linear-gradient(135deg, #5d7a8a 0%, #2e2b25 100%)',
    badgeBg: 'bg-[#dde7eb]',
    badgeFg: 'text-[#3e5664]',
  },
  {
    id: 'Otros',
    label: 'Otros',
    gradient: 'linear-gradient(135deg, #f4f1ec 0%, #cbc3b4 60%, #80796a 100%)',
    badgeBg: 'bg-warm-100',
    badgeFg: 'text-warm-700',
  },
]

/** Map por id para acceso rápido. */
export const CATEGORY_BY_ID: Record<ServiceCategoryEnum, CategoryMeta> = CATEGORIES.reduce(
  (acc, c) => ({ ...acc, [c.id]: c }),
  {} as Record<ServiceCategoryEnum, CategoryMeta>,
)

/** Filtro de status para la grilla. */
export type StatusFilter = 'all' | 'active' | 'inactive'

/** Formatea un número como moneda colombiana. */
export const fmtCOP = (n: number): string =>
  '$' + Math.round(n).toLocaleString('es-CO')
