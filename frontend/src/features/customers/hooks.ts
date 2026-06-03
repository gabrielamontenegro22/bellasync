import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createCustomer,
  deleteCustomer,
  getCustomer,
  getCustomerAppointments,
  listCustomers,
  updateCustomer,
  type CreateCustomerRequest,
  type UpdateCustomerRequest,
} from '@/api/customers'

const KEY = 'customers'

export function useCustomers(params: {
  search?: string
  page?: number
  pageSize?: number
  includeInactive?: boolean
} = {}) {
  return useQuery({
    queryKey: [KEY, params],
    queryFn: () => listCustomers(params),
    // Mantiene la página anterior visible mientras carga la nueva
    placeholderData: prev => prev,
  })
}

/**
 * Cliente individual con stats frescos. El panel detalle del CRM lo
 * usa para asegurarse de que tras editar, los datos en pantalla son
 * los del backend y no la copia capturada al hacer click en la lista.
 */
export function useCustomer(customerId: string | null) {
  return useQuery({
    queryKey: [KEY, customerId, 'detail'],
    queryFn: () => getCustomer(customerId!),
    enabled: !!customerId,
  })
}

/**
 * Historial completo de citas de un cliente. Habilitado solo si hay
 * customerId (cuando el panel detalle se monta).
 */
export function useCustomerAppointments(customerId: string | null) {
  return useQuery({
    queryKey: [KEY, customerId, 'appointments'],
    queryFn: () => getCustomerAppointments(customerId!),
    enabled: !!customerId,
  })
}

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: [KEY] })
}

export function useCreateCustomer() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (req: CreateCustomerRequest) => createCustomer(req),
    onSuccess: () => invalidate(qc),
  })
}

export function useUpdateCustomer() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateCustomerRequest }) =>
      updateCustomer(id, req),
    onSuccess: () => invalidate(qc),
  })
}

export function useDeleteCustomer() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteCustomer(id),
    onSuccess: () => invalidate(qc),
  })
}
