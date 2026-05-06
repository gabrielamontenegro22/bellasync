import type { SuggestedService } from '../types'

/**
 * Catálogo sugerido para el paso 4. Mismos valores que data.jsx → SUGGESTED_SERVICES.
 * El campo `defOn` indica si el servicio aparece pre-seleccionado por defecto.
 */
export const SUGGESTED_SERVICES: SuggestedService[] = [
  { id: 'srv1',  name: 'Manicure tradicional',     cat: 'Uñas',       price: 25_000,  dur: 45,  emoji: '💅', defOn: true  },
  { id: 'srv2',  name: 'Manicure semipermanente',  cat: 'Uñas',       price: 55_000,  dur: 60,  emoji: '💎', defOn: true  },
  { id: 'srv3',  name: 'Pedicure spa',             cat: 'Uñas',       price: 60_000,  dur: 60,  emoji: '🦶', defOn: true  },
  { id: 'srv4',  name: 'Corte de cabello',         cat: 'Cabello',    price: 45_000,  dur: 45,  emoji: '✂️', defOn: true  },
  { id: 'srv5',  name: 'Color completo',           cat: 'Cabello',    price: 180_000, dur: 120, emoji: '🎨', defOn: true  },
  { id: 'srv6',  name: 'Mechas / babylights',      cat: 'Cabello',    price: 240_000, dur: 180, emoji: '✨', defOn: false },
  { id: 'srv7',  name: 'Alisado keratina',         cat: 'Cabello',    price: 280_000, dur: 180, emoji: '💫', defOn: false },
  { id: 'srv8',  name: 'Cepillado / brushing',     cat: 'Cabello',    price: 35_000,  dur: 30,  emoji: '🌬️', defOn: true  },
  { id: 'srv9',  name: 'Diseño de cejas',          cat: 'Rostro',     price: 30_000,  dur: 30,  emoji: '👁️', defOn: true  },
  { id: 'srv10', name: 'Lifting de pestañas',      cat: 'Rostro',     price: 90_000,  dur: 60,  emoji: '👀', defOn: false },
  { id: 'srv11', name: 'Limpieza facial',          cat: 'Rostro',     price: 120_000, dur: 75,  emoji: '💆', defOn: false },
  { id: 'srv12', name: 'Maquillaje social',        cat: 'Maquillaje', price: 130_000, dur: 60,  emoji: '💄', defOn: false },
]

export const SERVICE_CATEGORIES_ORDER = ['Uñas', 'Cabello', 'Rostro', 'Maquillaje', 'Otros'] as const
