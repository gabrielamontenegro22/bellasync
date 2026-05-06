import { isAxiosError } from 'axios'
import type { ProblemDetails } from '@/types/auth'

/**
 * Extrae un mensaje legible de un error de Axios para mostrar al usuario.
 *
 *   - 400 ValidationProblem  → primer mensaje de error de campo
 *   - 401/403/404/409 etc.   → `detail` o `title` del ProblemDetails
 *   - Sin response (ECONNREFUSED, timeout, CORS) → mensaje específico
 *   - Cualquier otro error   → fallback configurable
 */
export function extractApiError(error: unknown, fallback = 'Ocurrió un error inesperado.'): string {
  if (isAxiosError(error)) {
    if (error.response) {
      const data = error.response.data as ProblemDetails | undefined
      if (data?.errors) {
        const firstField = Object.values(data.errors).flat()[0]
        if (firstField) return firstField
      }
      if (data?.detail) return data.detail
      if (data?.title)  return data.title
      return `Error ${error.response.status}: ${error.message}`
    }
    if (error.request) {
      const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5059'
      return `No se pudo conectar al servidor en ${baseUrl}. ¿Está corriendo el backend?`
    }
  }
  if (error instanceof Error) return error.message
  return fallback
}

/**
 * Extrae los errores de validación POR CAMPO de un response 400.
 * Útil para asignar errores específicos a inputs de un formulario.
 *
 * Backend devuelve PascalCase: { errors: { "AdminEmail": ["msg"] } }
 * Lo convertimos a camelCase para matchear los names del form: { adminEmail: "msg" }
 */
export function extractFieldErrors(error: unknown): Record<string, string> {
  if (!isAxiosError(error)) return {}
  const data = error.response?.data as ProblemDetails | undefined
  if (!data?.errors) return {}
  const out: Record<string, string> = {}
  for (const [field, messages] of Object.entries(data.errors)) {
    if (Array.isArray(messages) && messages.length > 0) {
      const camelKey = field.charAt(0).toLowerCase() + field.slice(1)
      out[camelKey] = messages[0]
    }
  }
  return out
}
