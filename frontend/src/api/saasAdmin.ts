import { api } from './axios'

/**
 * Endpoints del panel SuperAdmin (dueño de BellaSync). Solo el user
 * con role=SuperAdmin (TenantId=Empty) puede llamarlos. Cross-tenant:
 * ve facturas de TODOS los salones.
 */

export interface PendingValidationRow {
  invoiceId: string
  tenantId: string
  tenantName: string
  tenantSlug: string

  planCode: string
  planName: string
  amount: number

  issuedAt: string         // ISO UTC
  dueDate: string
  reportedAt: string
  reportedMethod: string
  reportedReference: string | null

  periodStart: string
  periodEnd: string
}

export async function listPendingValidations(): Promise<PendingValidationRow[]> {
  const { data } = await api.get<PendingValidationRow[]>(
    '/api/SaasAdmin/subscriptions/pending-validations',
  )
  return data
}

export async function validateSubscriptionPayment(invoiceId: string): Promise<void> {
  await api.post(`/api/SaasAdmin/subscriptions/invoices/${invoiceId}/validate`)
}

export async function rejectSubscriptionPayment(
  invoiceId: string,
  reason: string,
): Promise<void> {
  await api.post(
    `/api/SaasAdmin/subscriptions/invoices/${invoiceId}/reject`,
    { reason },
  )
}
