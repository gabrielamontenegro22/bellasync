/**
 * Tipos espejo de los DTOs del backend (BellaSync.Application.Features.Auth.Dtos).
 * Si cambia un DTO en C#, replicar acá.
 */

/** Respuesta estándar de los endpoints register-salon y login. */
export interface AuthResponse {
  token: string
  expiresAtUtc: string  // ISO 8601 — DateTime de C# se serializa así
  userId: string
  email: string
  fullName: string
  role: string  // valor del enum: "SalonAdmin" | "Receptionist" | "Stylist"
  tenantId: string
  tenantName: string
  tenantSlug: string
}

/** Body del POST /api/Auth/register-salon */
export interface RegisterSalonRequest {
  salonName: string
  adminFullName: string
  adminEmail: string
  adminPassword: string
}

/** Body del POST /api/Auth/login */
export interface LoginRequest {
  email: string
  password: string
}

/**
 * Forma de error según RFC 7807 (ProblemDetails).
 * ASP.NET devuelve esto en validaciones (400) y errores de negocio (401, 409, etc.).
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Solo presente en 400 ValidationProblem: errores agrupados por campo. */
  errors?: Record<string, string[]>
}

/** Datos del usuario autenticado, listos para consumir desde la UI. */
export interface AuthenticatedUser {
  userId: string
  email: string
  fullName: string
  role: string
  tenantId: string
  tenantName: string
  tenantSlug: string
}
