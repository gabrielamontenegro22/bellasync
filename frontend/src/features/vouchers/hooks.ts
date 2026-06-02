import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listPendingVouchers, validateVoucher, type VoucherDecision } from '@/api/vouchers'

const KEY = 'vouchers-pending'

export function usePendingVouchers() {
  return useQuery({
    queryKey: [KEY],
    queryFn: listPendingVouchers,
    // Refresca cada 30s — la cola es muy dinámica
    refetchInterval: 30_000,
  })
}

export function useValidateVoucher() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, decision, notes }: { id: string; decision: VoucherDecision; notes?: string }) =>
      validateVoucher(id, decision, notes),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [KEY] })
      qc.invalidateQueries({ queryKey: ['agenda'] }) // refresca agenda también
    },
  })
}
