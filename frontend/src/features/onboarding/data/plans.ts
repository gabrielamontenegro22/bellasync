import type { Plan } from '../types'

/**
 * Planes de suscripción de BellaSync.
 * Mismos valores que el mockup (data.jsx → PLANS).
 */
export const PLANS: Plan[] = [
  {
    id: 'basico',
    name: 'Básico',
    price: 50_000,
    sub: '1–3 estilistas',
    features: [
      'Agenda online',
      'Validación de pagos por WhatsApp',
      'Hasta 100 citas / mes',
      '1 sede',
    ],
  },
  {
    id: 'pro',
    name: 'Profesional',
    price: 90_000,
    sub: '4–8 estilistas',
    features: [
      'Hasta 500 citas / mes',
      'Reportes avanzados',
      'Plantillas WhatsApp ilimitadas',
      'Soporte prioritario',
      'CRM completo',
    ],
    recommended: true,
  },
  {
    id: 'premium',
    name: 'Premium',
    price: 150_000,
    sub: 'Sin límite',
    features: [
      'Citas ilimitadas',
      'Multi-sede consolidada',
      'API y exportación',
      'Manager dedicado',
      'Onboarding presencial',
    ],
  },
]
