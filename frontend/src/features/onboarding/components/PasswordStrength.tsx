import { cls } from '@/lib/cls'
import { passwordStrength } from '../schemas'

const LABELS = ['', 'Débil', 'Aceptable', 'Buena', 'Muy fuerte'] as const

interface PasswordStrengthProps {
  password: string
}

/**
 * Indicador visual de fuerza de contraseña — replica el componente
 * inline del Step1 del mockup. 4 barras coloreadas según fuerza.
 */
export function PasswordStrength({ password }: PasswordStrengthProps) {
  if (!password) return null

  const strength = passwordStrength(password)
  const label = LABELS[strength]

  return (
    <>
      <div className="flex gap-1 mt-2">
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className={cls(
              'h-1 flex-1 rounded-full',
              i <= strength
                ? strength <= 1
                  ? 'bg-terra-300'
                  : strength === 2
                    ? 'bg-gold-300'
                    : strength === 3
                      ? 'bg-gold-500'
                      : 'bg-brand-500'
                : 'bg-warm-200',
            )}
          />
        ))}
      </div>
      {label && <div className="text-[11.5px] text-warm-500 mt-1">{label}</div>}
    </>
  )
}
