import { forwardRef, useMemo, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { ArrowRight, AlertCircle, Eye, EyeOff, Check } from 'lucide-react'

import { cls } from '@/lib/cls'
import {
  resetPasswordSchema,
  type ResetPasswordFormData,
} from '@/features/auth/schemas'
import { resetPassword as apiResetPassword } from '@/api/auth'
import { extractApiError } from '@/lib/extractApiError'
import { passwordStrength } from '@/features/onboarding/schemas'

/**
 * Página /reset-password?token=xxx
 *
 * Se accede desde el enlace que el backend envía por email tras un
 * forgot-password. El token llega como query param.
 *
 * Stages:
 *  - reset       → formulario de nueva contraseña + confirmación
 *  - reset-done  → confirmación de éxito + CTA al login
 *
 * Si llega sin token, mostramos un mensaje de error inmediato.
 */
type Stage = 'reset' | 'reset-done'

export function ResetPassword() {
  const [params] = useSearchParams()
  const token = params.get('token')

  const [stage, setStage] = useState<Stage>('reset')

  return (
    <div className="relative min-h-screen overflow-hidden bg-cream flex items-center justify-center px-6 py-10">
      {/* Atmospheric background — mismo del Login */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="absolute -top-40 -left-40 w-[500px] h-[500px] rounded-full bg-brand-100/60 blur-3xl" />
        <div className="absolute -bottom-40 -right-32 w-[480px] h-[480px] rounded-full bg-gold-100/70 blur-3xl" />
        <div className="absolute top-1/3 right-1/4 w-[300px] h-[300px] rounded-full bg-[#f5dfd8]/40 blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        {/* Logo header */}
        <div className="text-center mb-6">
          <div className="inline-flex items-center gap-2.5">
            <div className="w-10 h-10 rounded-xl bg-brand-700 flex items-center justify-center text-white shadow-pop">
              <span className="font-serif text-[22px] leading-none translate-y-[1px]">B</span>
            </div>
            <div className="font-serif text-[26px] tracking-tight text-warm-800 leading-none">
              BellaSync
            </div>
          </div>
        </div>

        {/* Card */}
        <div className="rounded-3xl bg-white border border-warm-150 shadow-pop p-8 lg:p-10">
          {!token ? (
            <NoTokenPanel />
          ) : stage === 'reset' ? (
            <ResetPanel token={token} onDone={() => setStage('reset-done')} />
          ) : (
            <ResetDonePanel />
          )}
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Sin token en la URL — caso borde                                          */
/* -------------------------------------------------------------------------- */

function NoTokenPanel() {
  return (
    <div className="text-center">
      <div className="w-16 h-16 rounded-full bg-terra-100 text-terra-500 flex items-center justify-center mx-auto">
        <AlertCircle size={32} strokeWidth={1.5} />
      </div>
      <h3 className="font-serif text-[28px] text-warm-800 mt-5 leading-tight">
        Enlace inválido
      </h3>
      <p className="text-[13.5px] text-warm-600 mt-2 leading-relaxed">
        El enlace de recuperación no incluye el token requerido. Solicitá uno nuevo desde el login.
      </p>
      <Link
        to="/login"
        className="mt-6 inline-block w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium shadow-soft"
      >
        Volver al login
      </Link>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Stage: reset — formulario de nueva contraseña                             */
/* -------------------------------------------------------------------------- */

function ResetPanel({ token, onDone }: { token: string; onDone: () => void }) {
  const [serverError, setServerError] = useState<string | null>(null)
  const [showPwd, setShowPwd] = useState(false)

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormData>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: '', confirmPassword: '' },
  })

  const passwordValue = watch('password') ?? ''

  const onSubmit = async (data: ResetPasswordFormData) => {
    setServerError(null)
    try {
      await apiResetPassword({ token, newPassword: data.password })
      onDone()
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo cambiar la contraseña.'))
    }
  }

  return (
    <div className="anim-fade">
      <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
        Restablecer
      </div>
      <h1 className="font-serif text-[28px] text-warm-800 leading-tight mt-2">
        Crea una nueva contraseña
      </h1>
      <p className="text-[13px] text-warm-600 leading-relaxed mt-2 mb-5">
        Después podrás iniciar sesión con la contraseña nueva.
      </p>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        {serverError && (
          <div
            role="alert"
            className="rounded-lg bg-terra-100/50 border border-terra-300/60 px-3.5 py-2.5 text-[12.5px] text-terra-500 flex items-start gap-2"
          >
            <AlertCircle size={13} className="flex-shrink-0 mt-0.5" />
            <span>{serverError}</span>
          </div>
        )}

        <FormField label="Nueva contraseña" error={errors.password?.message}>
          <div className="relative">
            <BareInput
              type={showPwd ? 'text' : 'password'}
              autoComplete="new-password"
              autoFocus
              placeholder="Mínimo 8 caracteres"
              error={!!errors.password}
              className="pr-10"
              {...register('password')}
            />
            <button
              type="button"
              onClick={() => setShowPwd((v) => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-warm-500 hover:text-warm-800"
              aria-label={showPwd ? 'Ocultar contraseña' : 'Mostrar contraseña'}
            >
              {showPwd ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          <Strength password={passwordValue} />
        </FormField>

        <FormField label="Confirma la contraseña" error={errors.confirmPassword?.message}>
          <BareInput
            type={showPwd ? 'text' : 'password'}
            autoComplete="new-password"
            placeholder="Repite la contraseña"
            error={!!errors.confirmPassword}
            {...register('confirmPassword')}
          />
        </FormField>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft transition disabled:opacity-60 disabled:cursor-not-allowed"
        >
          {isSubmitting ? 'Guardando…' : 'Guardar contraseña'}
          {!isSubmitting && <ArrowRight size={14} />}
        </button>
      </form>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Stage: reset-done — éxito                                                 */
/* -------------------------------------------------------------------------- */

function ResetDonePanel() {
  const navigate = useNavigate()

  return (
    <div className="text-center anim-fade">
      <div className="relative w-20 h-20 mx-auto mb-5">
        <div className="absolute inset-0 rounded-full bg-brand-100 animate-ping-slow" />
        <div className="relative w-full h-full rounded-full bg-brand-700 text-white flex items-center justify-center shadow-pop">
          <Check size={44} strokeWidth={2.5} />
        </div>
      </div>
      <h3 className="font-serif text-[28px] text-warm-800 leading-tight">
        Contraseña actualizada
      </h3>
      <p className="text-[13.5px] text-warm-600 mt-2">
        Ya puedes iniciar sesión con tu nueva contraseña.
      </p>
      <button
        type="button"
        onClick={() => navigate('/login', { replace: true })}
        className="mt-6 w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft"
      >
        Ir al login <ArrowRight size={14} />
      </button>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Indicador de fuerza de contraseña                                         */
/* -------------------------------------------------------------------------- */

const STRENGTH_LABELS = ['', 'Débil', 'Aceptable', 'Buena', 'Muy fuerte'] as const

function Strength({ password }: { password: string }) {
  const score = useMemo(() => passwordStrength(password), [password])
  if (!password) return null

  return (
    <>
      <div className="flex gap-1 mt-2">
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className={cls(
              'h-1 flex-1 rounded-full',
              i <= score
                ? score <= 1
                  ? 'bg-terra-300'
                  : score === 2
                    ? 'bg-gold-300'
                    : score === 3
                      ? 'bg-gold-500'
                      : 'bg-brand-500'
                : 'bg-warm-200',
            )}
          />
        ))}
      </div>
      {STRENGTH_LABELS[score] && (
        <div className="text-[11.5px] text-warm-500 mt-1">{STRENGTH_LABELS[score]}</div>
      )}
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*  Primitives reutilizadas                                                   */
/* -------------------------------------------------------------------------- */

function FormField({
  label,
  error,
  children,
}: {
  label: string
  error?: string
  children: ReactNode
}) {
  return (
    <div>
      <label className="block text-[12.5px] font-medium text-warm-700 mb-1.5">
        {label}
      </label>
      {children}
      {error && (
        <div className="text-[11px] text-terra-500 mt-1 flex items-center gap-1">
          <AlertCircle size={11} />
          {error}
        </div>
      )}
    </div>
  )
}

interface BareInputProps extends InputHTMLAttributes<HTMLInputElement> {
  error?: boolean
}

const BareInput = forwardRef<HTMLInputElement, BareInputProps>(
  function BareInput({ error, className = '', ...rest }, ref) {
    return (
      <input
        ref={ref}
        {...rest}
        className={cls(
          'w-full px-3.5 py-2.5 rounded-lg bg-white border text-[14px] text-warm-800 placeholder:text-warm-400 focus:ring-2 focus:ring-brand-100 outline-none transition',
          error
            ? 'border-terra-300 focus:border-terra-500'
            : 'border-warm-200 focus:border-brand-500',
          className,
        )}
      />
    )
  },
)
