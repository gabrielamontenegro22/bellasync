import { z } from 'zod'

/**
 * Schemas Zod que ESPEJAN los Validators de FluentValidation del backend
 * (BellaSync.Application.Features.Auth.Validators).
 *
 * Si cambia una regla en el backend, replicarla acá. Los mensajes están en
 * español para que aparezcan tal cual en los formularios.
 */

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'El correo electrónico es obligatorio.')
    .email('Formato de correo electrónico inválido.'),
  password: z
    .string()
    .min(1, 'La contraseña es obligatoria.'),
})

export const registerSchema = z.object({
  salonName: z
    .string()
    .trim()
    .min(3, 'El nombre del salón debe tener al menos 3 caracteres.')
    .max(100, 'El nombre del salón no puede superar los 100 caracteres.'),
  adminFullName: z
    .string()
    .trim()
    .min(3, 'El nombre del administrador debe tener al menos 3 caracteres.')
    .max(150, 'El nombre del administrador no puede superar los 150 caracteres.'),
  adminEmail: z
    .string()
    .trim()
    .min(1, 'El correo electrónico es obligatorio.')
    .email('Formato de correo electrónico inválido.')
    .max(150, 'El correo no puede superar los 150 caracteres.'),
  adminPassword: z
    .string()
    .min(8, 'La contraseña debe tener al menos 8 caracteres.')
    .max(100, 'La contraseña no puede superar los 100 caracteres.')
    .regex(/[A-Z]/, 'La contraseña debe incluir al menos una letra mayúscula.')
    .regex(/[a-z]/, 'La contraseña debe incluir al menos una letra minúscula.')
    .regex(/[0-9]/, 'La contraseña debe incluir al menos un número.'),
})

export type LoginFormData    = z.infer<typeof loginSchema>
export type RegisterFormData = z.infer<typeof registerSchema>
