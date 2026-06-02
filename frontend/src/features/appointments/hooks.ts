import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  cancelAppointment,
  completeAppointment,
  confirmAppointment,
  createAppointment,
  getAgenda,
  markNoShow,
  startAppointment,
  type CreateAppointmentRequest,
} from '@/api/appointments'

const KEY = 'agenda'

export function useAgenda(date: string, stylistId?: string) {
  return useQuery({
    queryKey: [KEY, date, stylistId ?? 'all'],
    queryFn: () => getAgenda(date, stylistId),
  })
}

function useAppointmentMutation<TArgs>(
  mutationFn: (args: TArgs) => Promise<unknown>,
) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn,
    // Invalida toda la agenda — más simple que figurar qué día afectó.
    onSuccess: () => qc.invalidateQueries({ queryKey: [KEY] }),
  })
}

export const useCreateAppointment = () =>
  useAppointmentMutation((req: CreateAppointmentRequest) => createAppointment(req))

export const useConfirmAppointment = () =>
  useAppointmentMutation((id: string) => confirmAppointment(id))

export const useCancelAppointment = () =>
  useAppointmentMutation(({ id, reason }: { id: string; reason?: string }) =>
    cancelAppointment(id, reason))

export const useStartAppointment = () =>
  useAppointmentMutation((id: string) => startAppointment(id))

export const useCompleteAppointment = () =>
  useAppointmentMutation((id: string) => completeAppointment(id))

export const useMarkNoShow = () =>
  useAppointmentMutation((id: string) => markNoShow(id))
