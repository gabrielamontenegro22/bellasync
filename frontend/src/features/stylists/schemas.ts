import { z } from 'zod'

/**
 * Schema Zod del formulario de Estilista.
 * Espeja las reglas del backend (CreateStylistValidator + UpdateStylistValidator):
 *  - FullName: 2-150 chars
 *  - Role: 1-80 chars (string libre)
 *  - Email: opcional, formato válido si se envía, max 150
 *  - Phone: opcional, regex teléfono colombiano si se envía, max 30
 *  - IdNumber: opcional, max 30
 *  - Color: opcional, hex válido si se envía
 *  - HireDate: opcional, no futura
 *  - Status: enum (solo se usa al editar)
 *  - ServiceIds: array de Guids únicos
 */
export const stylistSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(2, 'El nombre debe tener al menos 2 caracteres.')
    .max(150, 'El nombre no puede superar los 150 caracteres.'),

  role: z
    .string()
    .trim()
    .min(1, 'El cargo es obligatorio.')
    .max(80, 'El cargo no puede superar los 80 caracteres.'),

  email: z
    .string()
    .trim()
    .email('Formato de correo electrónico inválido.')
    .max(150, 'El correo no puede superar los 150 caracteres.')
    .optional()
    .or(z.literal('')),

  phone: z
    .string()
    .trim()
    .regex(/^\+?\d[\d\s-]{8,}$/, 'Teléfono inválido.')
    .max(30, 'El teléfono no puede superar los 30 caracteres.')
    .optional()
    .or(z.literal('')),

  idNumber: z
    .string()
    .trim()
    .max(30, 'La cédula no puede superar los 30 caracteres.')
    .optional()
    .or(z.literal('')),

  color: z
    .string()
    .regex(/^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/, 'Color hex inválido.')
    .optional()
    .or(z.literal('')),

  hireDate: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Fecha inválida (YYYY-MM-DD).')
    .optional()
    .or(z.literal('')),

  status: z.enum(['Active', 'Vacation', 'Inactive']),

  serviceIds: z.array(z.string()),
})

export type StylistFormData = z.infer<typeof stylistSchema>

/** Valores por defecto al crear un estilista nuevo. Color = hex del tono "sage". */
export const defaultStylistForm: StylistFormData = {
  fullName: '',
  role: 'Estilista',
  email: '',
  phone: '',
  idNumber: '',
  color: '#d6e6dd', // tono "sage" por default
  hireDate: '',
  status: 'Active',
  serviceIds: [],
}
