import { useQuery } from '@tanstack/react-query'
import { getSalonHours, type SalonHoursDto } from '@/api/admin'

/**
 * Hook compartido para leer el horario del salón. Usado por:
 *  - HorarioPage (form de edición)
 *  - AgendaTimeline (dimear las franjas cerradas)
 *  - Futuro: portal público de booking (validar slots ofrecidos)
 *
 * Cache de 5 minutos — el horario no cambia seguido, no necesitamos
 * refrescar agresivamente.
 */
export function useSalonHours() {
  return useQuery<SalonHoursDto>({
    queryKey: ['salonHours'],
    queryFn: getSalonHours,
    staleTime: 5 * 60 * 1000,
  })
}
