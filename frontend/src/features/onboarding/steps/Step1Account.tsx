import { useState } from 'react'
import { ArrowRight, Eye, EyeOff } from 'lucide-react'
import type { WizardData } from '../types'
import { WizardField, WizardInput } from '../components/WizardField'
import { PasswordStrength } from '../components/PasswordStrength'
import { step1Schema } from '../schemas'
import { cls } from '@/lib/cls'

interface Step1Props {
  data: WizardData
  set: (patch: Partial<WizardData>) => void
  onNext: () => void
}

/**
 * Paso 1 del wizard — Crear cuenta.
 * Esta es la única información que se persiste al backend acá mismo
 * (en el OnboardingWizard, después de superar este paso, se llama a register-salon).
 */
export function Step1Account({ data, set, onNext }: Step1Props) {
  const [show, setShow] = useState(false)
  const [touched, setTouched] = useState(false)

  // Validación en vivo (el mockup la mostraba solo después de tocar)
  const result = step1Schema.safeParse({
    ownerName: data.ownerName,
    email: data.email,
    password: data.password,
  })
  const errors: Record<string, string> = {}
  if (!result.success && touched) {
    for (const issue of result.error.issues) {
      const key = issue.path[0]?.toString()
      if (key && !errors[key]) errors[key] = issue.message
    }
  }
  const valid = result.success

  const handleNext = () => {
    setTouched(true)
    if (valid) onNext()
  }

  return (
    <div className="space-y-5">
      <WizardField label="Tu nombre completo" error={errors.ownerName}>
        <WizardInput
          value={data.ownerName}
          onChange={(e) => set({ ownerName: e.target.value })}
          placeholder="Carolina Rodríguez"
          autoFocus
        />
      </WizardField>

      <WizardField
        label="Correo electrónico"
        error={errors.email}
        hint="Lo usarás para iniciar sesión"
      >
        <WizardInput
          type="email"
          value={data.email}
          onChange={(e) => set({ email: e.target.value })}
          placeholder="carolina@bellaaurora.co"
          autoComplete="email"
        />
      </WizardField>

      <WizardField label="Contraseña" error={errors.password}>
        <div className="relative">
          <WizardInput
            type={show ? 'text' : 'password'}
            value={data.password}
            onChange={(e) => set({ password: e.target.value })}
            placeholder="Mínimo 8 caracteres"
            autoComplete="new-password"
            className="pr-10"
          />
          <button
            type="button"
            onClick={() => setShow((s) => !s)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-warm-500 hover:text-warm-800"
            aria-label={show ? 'Ocultar contraseña' : 'Mostrar contraseña'}
          >
            {show ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        </div>
        <PasswordStrength password={data.password} />
      </WizardField>

      <div className="pt-2">
        <button
          type="button"
          onClick={handleNext}
          className={cls(
            'w-full px-5 py-3 rounded-xl text-[14px] font-medium flex items-center justify-center gap-2 transition',
            valid
              ? 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft'
              : 'bg-warm-150 text-warm-400 cursor-not-allowed',
          )}
        >
          Continuar <ArrowRight size={14} />
        </button>
        <p className="text-[11.5px] text-warm-500 text-center mt-3">
          Al continuar aceptas los{' '}
          <a href="#" className="underline hover:text-warm-800">Términos</a>{' '}
          y la{' '}
          <a href="#" className="underline hover:text-warm-800">Política de privacidad</a>.
        </p>
      </div>
    </div>
  )
}
