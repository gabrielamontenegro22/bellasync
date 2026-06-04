import { useQuery } from '@tanstack/react-query'
import { getCommissionsSetting } from '@/api/admin'

/**
 * Hook compartido para saber si el módulo de Comisiones está activo
 * en el salón actual. Lo usan el sidebar (para mostrar/ocultar el
 * item), la página /comisiones (para redirect si está OFF), y el
 * form de Servicios (para mostrar/ocultar el campo % de comisión).
 *
 * Cache de 5 minutos — el toggle se cambia raramente; cuando se
 * cambia, invalidamos la key ['commissionsSetting'] desde el form.
 */
export function useCommissionsSetting() {
  return useQuery({
    queryKey: ['commissionsSetting'],
    queryFn: getCommissionsSetting,
    staleTime: 5 * 60 * 1000,
  })
}
