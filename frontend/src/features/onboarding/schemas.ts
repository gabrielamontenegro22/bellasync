import { z } from 'zod'

/**
 * Schemas Zod para los pasos del wizard.
 *
 * Para los pasos que se persisten al backend (paso 1 al register-salon,
 * paso 4 al POST /api/Services), las reglas espejan FluentValidation del backend.
 *
 * Para los pasos locales (2, 3, 5) las validaciones son las mismas que mostraba
 * el mockup inline.
 */

export const step1Schema = z.object({
  ownerName: z.string().trim().min(3, 'Ingresa tu nombre'),
  email:     z.string().trim().min(1, 'Correo inválido').email('Correo inválido'),
  password:  z
    .string()
    .min(8, 'Mín. 8 caracteres')
    .max(100, 'Máximo 100 caracteres')
    .regex(/[A-Z]/, 'Debe incluir una mayúscula')
    .regex(/[a-z]/, 'Debe incluir una minúscula')
    .regex(/[0-9]/, 'Debe incluir un número'),
})

export const step2Schema = z.object({
  salonName: z.string().trim().min(2, 'Requerido').max(100),
  nit:       z.string().trim().min(5, 'NIT inválido'),
  address:   z.string().trim().min(5, 'Requerido'),
  city:      z.string().min(1, 'Selecciona ciudad'),
  phone:     z
    .string()
    .trim()
    .regex(/^\+?\d[\d\s-]{8,}$/, 'Teléfono inválido'),
  logoData: z.string().nullable(),
  logoName: z.string(),
})

export const step3Schema = z.object({
  hoursPreset: z.enum(['classic', 'martes_dom', 'lun_dom', 'custom']),
  // Validamos que al menos un día esté abierto
  hours: z.record(
    z.union([z.tuple([z.number(), z.number()]), z.null()]),
  ).refine(
    (h) => Object.values(h).some(Boolean),
    { message: 'Al menos un día debe estar abierto' },
  ),
})

export const step5Schema = z.object({
  plan: z.enum(['basico', 'pro', 'premium']),
})

/**
 * Calcula la fuerza de una contraseña en una escala de 0 a 4.
 * Mismo algoritmo que el mockup (Step1 inline).
 */
export function passwordStrength(password: string): 0 | 1 | 2 | 3 | 4 {
  if (!password) return 0
  if (password.length < 8) return 1
  if (password.length < 12) return 2
  if (/[A-Z]/.test(password) && /\d/.test(password)) return 4
  return 3
}
