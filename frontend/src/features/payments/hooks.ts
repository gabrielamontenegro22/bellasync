import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getCustomerPayments,
  registerPayment,
  type RegisterPaymentRequest,
} from '@/api/payments'

const KEY = 'payments'

/**
 * Historial de pagos del cliente — alimenta el tab Pagos del CRM.
 * Habilitado solo si hay customerId (cuando el panel detalle se monta).
 */
export function useCustomerPayments(customerId: string | null) {
  return useQuery({
    queryKey: [KEY, customerId],
    queryFn: () => getCustomerPayments(customerId!),
    enabled: !!customerId,
  })
}

/**
 * Mutation para registrar un pago de una cita. Al persistir, invalida:
 *  - La query de pagos del cliente (para refrescar el tab Pagos del CRM).
 *  - La agenda (la cita ya tiene pagos registrados; el panel detalle puede
 *    mostrarlo en la siguiente versión).
 *  - Los stats de Customers (total invertido cambia).
 */
export function useRegisterPayment() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ appointmentId, req }: { appointmentId: string; req: RegisterPaymentRequest }) =>
      registerPayment(appointmentId, req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [KEY] })
      qc.invalidateQueries({ queryKey: ['agenda'] })
      qc.invalidateQueries({ queryKey: ['customers'] })
    },
  })
}
