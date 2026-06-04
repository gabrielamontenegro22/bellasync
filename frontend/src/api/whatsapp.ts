import { api } from './axios'

/**
 * Tipos del módulo WhatsApp. Espejo de los DTOs C#.
 *
 * Kind: string identificador del catálogo del backend. Valores actuales:
 *   "ConfirmCreated" | "Reminder24h" | "Ready2h" | "PendingDeposit" | "Birthday"
 * Si en el futuro el catálogo crece, frontend no rompe — la página los
 * itera dinámicamente.
 */
export interface WhatsAppTemplateDto {
  kind: string
  title: string         // label en español, del catálogo backend
  description: string   // hint de cuándo se dispara
  body: string          // con placeholders {nombre}, {fecha}, etc.
  isEnabled: boolean
}

export interface WhatsAppMessageDto {
  id: string
  kind: string
  customerPhone: string
  renderedBody: string
  appointmentId: string | null
  status: 'Queued' | 'Sent' | 'Failed' | 'Cancelled' | string
  queuedAt: string  // ISO
  sentAt: string | null
  failedAt: string | null
  failureReason: string | null
}

/**
 * GET /api/Admin/whatsapp/templates
 * Devuelve todos los kinds del catálogo, con body/isEnabled persistidos
 * (o defaults si nunca se guardaron para ese tenant).
 */
export async function getWhatsAppTemplates(): Promise<WhatsAppTemplateDto[]> {
  const { data } = await api.get<WhatsAppTemplateDto[]>('/api/Admin/whatsapp/templates')
  return data
}

/**
 * PUT /api/Admin/whatsapp/templates/{kind}
 * Upsert del body + isEnabled de un kind.
 */
export async function updateWhatsAppTemplate(
  kind: string,
  body: string,
  isEnabled: boolean,
): Promise<void> {
  await api.put(`/api/Admin/whatsapp/templates/${kind}`, { body, isEnabled })
}

/**
 * GET /api/Admin/whatsapp/messages?status=…&take=…
 * Últimos N mensajes, opcionalmente filtrados por status.
 */
export async function listWhatsAppMessages(
  params: { status?: string; take?: number } = {},
): Promise<WhatsAppMessageDto[]> {
  const { data } = await api.get<WhatsAppMessageDto[]>('/api/Admin/whatsapp/messages', {
    params,
  })
  return data
}

/**
 * POST /api/Admin/whatsapp/messages/{id}/retry
 * Resetea un mensaje Failed a Queued para que el próximo tick lo reintente.
 */
export async function retryWhatsAppMessage(id: string): Promise<void> {
  await api.post(`/api/Admin/whatsapp/messages/${id}/retry`)
}
