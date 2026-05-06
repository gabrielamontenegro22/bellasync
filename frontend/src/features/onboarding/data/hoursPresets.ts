import type { DayId, DayRange, HoursPresetId } from '../types'

interface HoursPreset {
  label: string
  days: Record<DayId, DayRange>
}

/**
 * Plantillas de horario rápidas. Mismas que data.jsx → HOURS_PRESETS.
 */
export const HOURS_PRESETS: Record<Exclude<HoursPresetId, 'custom'>, HoursPreset> = {
  classic: {
    label: 'Lun–Sáb · 9am–7pm',
    days: { mon:[9,19], tue:[9,19], wed:[9,19], thu:[9,19], fri:[9,19], sat:[9,19], sun:null },
  },
  martes_dom: {
    label: 'Mar–Dom · 10am–8pm',
    days: { mon:null, tue:[10,20], wed:[10,20], thu:[10,20], fri:[10,20], sat:[10,20], sun:[10,20] },
  },
  lun_dom: {
    label: 'Lun–Dom · 8am–8pm',
    days: { mon:[8,20], tue:[8,20], wed:[8,20], thu:[8,20], fri:[8,20], sat:[8,20], sun:[8,20] },
  },
}

/** Lista ordenada de los 7 días con su nombre en español. */
export const DAYS: Array<{ id: DayId; label: string }> = [
  { id: 'mon', label: 'Lunes' },
  { id: 'tue', label: 'Martes' },
  { id: 'wed', label: 'Miércoles' },
  { id: 'thu', label: 'Jueves' },
  { id: 'fri', label: 'Viernes' },
  { id: 'sat', label: 'Sábado' },
  { id: 'sun', label: 'Domingo' },
]
