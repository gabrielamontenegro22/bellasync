import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createCustomer,
  deleteCustomer,
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
