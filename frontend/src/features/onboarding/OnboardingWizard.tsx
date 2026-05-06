import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { Stepper, STEPS } from './components/Stepper'
import { Summary } from './components/Summary'
import { SuccessScreen } from './components/SuccessScreen'
import { Step1Account } from './steps/Step1Account'
import { Step2SalonInfo } from './steps/Step2SalonInfo'
import { Step3Hours } from './steps/Step3Hours'
import { Step4Services } from './steps/Step4Services'
import { Step5Plan } from './steps/Step5Plan'

import { useWizard } from './useWizard'
import { wizardStorage } from './storage'
import { useAuth } from '@/features/auth/useAuth'
import { createService, mapWizardCategory } from '@/api/services'
import { extractApiError } from '@/lib/extractApiError'
import { SUGGESTED_SERVICES } from './data/suggestedServices'

/**
 * Wizard de onboarding completo (5 pasos + pantalla de éxito).
 *
 * Flujo de submit final:
 *  1. POST /api/Auth/register-salon con email/password/nombre   ← REAL al backend
 *  2. Guarda JWT en AuthContext
 *  3. POST /api/Services por cada servicio activo                ← REAL al backend
 *  4. Persiste pasos 2/3/5 en localStorage como `pending sync`   ← LOCAL (backend WIP)
 *  5. Muestra SuccessScreen
 *  6. CTA "Ir al panel" -> /dashboard
 */
export function OnboardingWizard() {
  const navigate = useNavigate()
  const { register: registerSalon } = useAuth()
  const { step, maxReached, done, data, set, next, back, goTo, finish, reset } = useWizard()

  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const onSubmit = async () => {
    setSubmitting(true)
    setSubmitError(null)
    try {
      // 1. Registrar el salón (paso 1) → backend real, devuelve JWT
      await registerSalon({
        salonName:     data.salonName,
        adminFullName: data.ownerName,
        adminEmail:    data.email.trim().toLowerCase(),
        adminPassword: data.password,
      })

      // 2. Crear servicios activos en el backend (paso 4) → silenciosamente.
      //    Si alguno falla, lo logueamos pero no bloqueamos el flujo.
      const allServices = [
        ...SUGGESTED_SERVICES.map((s) => ({ id: s.id, name: s.name, cat: s.cat })),
        ...data.customServices.map((s) => ({ id: s.id, name: s.name, cat: s.cat })),
      ]

      const activeServices = allServices.filter((s) => data.servicesOn[s.id])

      await Promise.allSettled(
        activeServices.map((s) => {
          const fields = data.servicesData[s.id]
          if (!fields) return Promise.resolve()
          return createService({
            name: s.name,
            price: fields.price,
            durationMinutes: fields.dur,
            category: mapWizardCategory(s.cat),
          }).catch((err) => {
            // Log silencioso — no rompemos el onboarding por un servicio individual.
            console.warn(`No se pudo crear el servicio "${s.name}":`, err)
          })
        }),
      )

      // 3. Persistir datos pendientes (NIT, dirección, horarios, plan)
      //    para sincronizar cuando el backend tenga endpoints.
      wizardStorage.savePending({
        nit:         data.nit,
        address:     data.address,
        city:        data.city,
        phone:       data.phone,
        logoData:    data.logoData,
        logoName:    data.logoName,
        hoursPreset: data.hoursPreset,
        hours:       data.hours,
        plan:        data.plan,
      })

      // 4. Mostrar SuccessScreen
      finish()
    } catch (err) {
      setSubmitError(extractApiError(err, 'No se pudo completar el registro.'))
    } finally {
      setSubmitting(false)
    }
  }

  // Pantalla de éxito post-submit
  if (done) {
    return (
      <SuccessScreen
        data={data}
        onGoToPanel={() => navigate('/dashboard')}
        onRestart={reset}
      />
    )
  }

  const cur = STEPS[step - 1]

  return (
    <div className="min-h-screen bg-cream">
      {/* Header sticky */}
      <header className="border-b border-warm-150 bg-cream/80 backdrop-blur sticky top-0 z-30">
        <div className="max-w-6xl mx-auto px-6 lg:px-10 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-brand-700 flex items-center justify-center text-white">
              <span className="font-serif text-[18px] leading-none translate-y-[1px]">B</span>
            </div>
            <div className="font-serif text-[20px] tracking-tight text-warm-800 leading-none">
              BellaSync
            </div>
            <span className="hidden sm:inline-block text-[10.5px] tracking-[0.18em] uppercase text-gold-600 font-medium ml-3 pl-3 border-l border-warm-200">
              Crear cuenta
            </span>
          </div>
          <Link
            to="/login"
            className="text-[12.5px] text-warm-600 hover:text-warm-900"
          >
            ¿Ya tienes cuenta?{' '}
            <span className="font-medium text-brand-700 underline-offset-2 hover:underline">
              Iniciar sesión
            </span>
          </Link>
        </div>
      </header>

      {/* Layout main + sidebar */}
      <main className="max-w-6xl mx-auto px-6 lg:px-10 py-8 lg:py-12 grid lg:grid-cols-[1fr_320px] gap-8">
        <section className="rounded-2xl bg-white border border-warm-150 p-6 lg:p-10 shadow-soft">
          <Stepper step={step} max={maxReached} onGoTo={goTo} />

          <div className="mt-8 mb-7">
            <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
              Paso {step} de {STEPS.length}
            </div>
            <h1 className="font-serif text-[34px] lg:text-[42px] leading-[1.05] tracking-tight text-warm-800 mt-1">
              {cur.title}
            </h1>
            <p className="text-[14px] text-warm-500 mt-1.5">{cur.sub}</p>
          </div>

          {submitError && (
            <div
              role="alert"
              className="mb-5 rounded-lg bg-terra-100 border border-terra-300 px-3 py-2 text-[12.5px] text-terra-500"
            >
              {submitError}
            </div>
          )}

          {/* Render del paso actual con animación */}
          <div className="anim-step" key={step}>
            {step === 1 && <Step1Account data={data} set={set} onNext={next} />}
            {step === 2 && <Step2SalonInfo data={data} set={set} onNext={next} onBack={back} />}
            {step === 3 && <Step3Hours data={data} set={set} onNext={next} onBack={back} />}
            {step === 4 && <Step4Services data={data} set={set} onNext={next} onBack={back} />}
            {step === 5 && (
              <Step5Plan
                data={data}
                set={set}
                onSubmit={onSubmit}
                onBack={back}
                loading={submitting}
              />
            )}
          </div>
        </section>

        <Summary data={data} />
      </main>
    </div>
  )
}
