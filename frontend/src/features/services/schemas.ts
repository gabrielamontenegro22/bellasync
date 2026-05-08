import { z } from 'zod'

/**
 * Schema Zod del formulario de Servicio.
 *
 * Espeja ServiceValidationRules.cs del backend:
 *  - Name: 1-100 chars
 *  - Description: max 500 chars (opcional)
 *  - Price: 10.000 a 500.000 COP
 *  - DurationMinutes: 1 a 480 minutos (8 horas)
 *  - CommissionPercentage: 0 a 100
 *  - Color: hex #RGB o #RRGGBB (opcional)
 */
export const serviceSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'El nombre es obligatorio.')
    .max(100, 'El nombre no puede superar los 100 caracteres.'),

  description: z
    .string()
    .trim()
    .max(500, 'La descripción no puede superar los 500 caracteres.')
    .optional()
    .or(z.literal('')),

  category: z.enum(['Cabello', 'Unas', 'Estetica', 'Maquillaje', 'Depilacion', 'Otros']),

  durationMinutes: z
    .number({ message: 'Duración inválida.' })
    .int('La duración debe ser un número entero.')
    .min(1, 'La duración mínima es 1 minuto.')
    .max(480, 'La duración máxima es 8 horas (480 minutos).'),

  price: z
    .number({ message: 'Precio inválido.' })
    .min(10_000, 'El precio mínimo es $10.000.')
    .max(500_000, 'El precio máximo es $500.000.'),

  commissionPercentage: z
    .number({ message: 'Comisión inválida.' })
    .min(0, 'La comisión no puede ser negativa.')
    .max(100, 'La comisión no puede superar el 100%.'),

  color: z
    .string()
    .regex(/^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/, 'Color hex inválido (ej. #1f5d50).')
    .optional()
    .or(z.literal('')),

  isActive: z.boolean(),

  // Campos extra que NO viajan al backend pero se persisten en localStorage:
  requiresDeposit: z.boolean(),
  depositPercentage: z
    .number()
    .min(0, 'El anticipo no puede ser negativo.')
    .max(100, 'El anticipo no puede superar el 100%.'),
  assignedStylistIds: z.array(z.string()),
})

export type ServiceFormData = z.infer<typeof serviceSchema>

/** Valores por defecto al crear un servicio nuevo. */
export const defaultServiceForm: ServiceFormData = {
  name: '',
  description: '',
  category: 'Cabello',
  durationMinutes: 60,
  price: 50_000,
  commissionPercentage: 30,
  color: '',
  isActive: true,
  requiresDeposit: false,
  depositPercentage: 30,
  assignedStylistIds: [],
}
