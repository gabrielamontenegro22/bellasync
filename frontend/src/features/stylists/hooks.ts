import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createStylist,
  deleteStylist,
  getStylist,
  listStylists,
  updateStylist,
  type CreateStylistRequest,
  type StylistResponse,
  type UpdateStylistRequest,
} from '@/api/stylists'

/**
 * Hooks de TanStack Query para el módulo de Estilistas.
 * Manejan caché, invalidación, estados de loading/error y refetch automático.
 */

const QK = {
  all: ['stylists'] as const,
  list: (includeInactive: boolean) => ['stylists', 'list', { includeInactive }] as const,
  detail: (id: string) => ['stylists', 'detail', id] as const,
}

/**
 * Lista todos los estilistas del salón.
 * Por defecto solo no-inactivos. Pasar `includeInactive=true` para incluir archivados.
 */
export function useStylists(includeInactive = false) {
  return useQuery({
    queryKey: QK.list(includeInactive),
    queryFn: () => listStylists(includeInactive),
  })
}

/** Detalle de un estilista. */
export function useStylist(id: string | undefined) {
  return useQuery({
    queryKey: QK.detail(id ?? ''),
    queryFn: () => getStylist(id!),
    enabled: Boolean(id),
  })
}

/** Crea un estilista. Invalida la lista para que se refresque. */
export function useCreateStylist() {
  const qc = useQueryClient()
  return useMutation<StylistResponse, Error, CreateStylistRequest>({
    mutationFn: createStylist,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QK.all })
    },
  })
}

/** Edita un estilista. Invalida lista y detalle. */
export function useUpdateStylist() {
  const qc = useQueryClient()
  return useMutation<StylistResponse, Error, { id: string; payload: UpdateStylistRequest }>({
    mutationFn: ({ id, payload }) => updateStylist(id, payload),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: QK.all })
      qc.invalidateQueries({ queryKey: QK.detail(id) })
    },
  })
}

/** Soft delete (Status → Inactive). */
export function useDeleteStylist() {
  const qc = useQueryClient()
  return useMutation<void, Error, string>({
    mutationFn: deleteStylist,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QK.all })
    },
  })
}
