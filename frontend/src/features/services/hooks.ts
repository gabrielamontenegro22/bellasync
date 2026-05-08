import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createService,
  deleteService,
  getService,
  listServices,
  updateService,
  type CreateServiceRequest,
  type ServiceResponse,
  type UpdateServiceRequest,
} from '@/api/services'
import { serviceExtrasStorage } from './storage'

/**
 * Hooks de TanStack Query para el módulo de Servicios.
 * Manejan caché, invalidación, estados de loading/error y refetch automático.
 */

const QK = {
  all: ['services'] as const,
  list: (includeInactive: boolean) => ['services', 'list', { includeInactive }] as const,
  detail: (id: string) => ['services', 'detail', id] as const,
}

/**
 * Lista todos los servicios del salón.
 * Por defecto solo activos. Pasar `includeInactive=true` para incluir archivados.
 */
export function useServices(includeInactive = false) {
  return useQuery({
    queryKey: QK.list(includeInactive),
    queryFn: () => listServices(includeInactive),
  })
}

/** Detalle de un servicio. */
export function useService(id: string | undefined) {
  return useQuery({
    queryKey: QK.detail(id ?? ''),
    queryFn: () => getService(id!),
    enabled: Boolean(id),
  })
}

/**
 * Crea un servicio. Invalida la lista para que se refresque automáticamente.
 */
export function useCreateService() {
  const qc = useQueryClient()
  return useMutation<ServiceResponse, Error, CreateServiceRequest>({
    mutationFn: createService,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QK.all })
    },
  })
}

/**
 * Edita un servicio. Invalida la lista y el detalle.
 */
export function useUpdateService() {
  const qc = useQueryClient()
  return useMutation<ServiceResponse, Error, { id: string; payload: UpdateServiceRequest }>({
    mutationFn: ({ id, payload }) => updateService(id, payload),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: QK.all })
      qc.invalidateQueries({ queryKey: QK.detail(id) })
    },
  })
}

/**
 * Soft delete de un servicio.
 * También limpia los extras locales (anticipo, estilistas asignados).
 */
export function useDeleteService() {
  const qc = useQueryClient()
  return useMutation<void, Error, string>({
    mutationFn: deleteService,
    onSuccess: (_data, id) => {
      serviceExtrasStorage.remove(id)
      qc.invalidateQueries({ queryKey: QK.all })
    },
  })
}
