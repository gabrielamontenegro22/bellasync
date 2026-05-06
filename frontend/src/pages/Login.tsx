import { forwardRef, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ArrowRight, ArrowLeft, Eye, EyeOff, AlertCircle, Mail } from 'lucide-react'

import { cls } from '@/lib/cls'
import {
  loginSchema,
  forgotPasswordSchema,
  type LoginFormData,
  type ForgotPasswordFormData,
} from '@/features/auth/schemas'
import { useAuth } from '@/features/auth/useAuth'
import { forgotPassword as apiForgotPassword } from '@/api/auth'
import { extractApiError } from '@/lib/extractApiError'

/**
 * Página de Login con flujo de recuperación de contraseña.
 *
 * Replica la variación VarCard del mockup login.jsx con 3 stages:
 *  - login   → formulario email + password (POST /api/Auth/login)
 *  - forgot  → pedir email (POST /api/Auth/forgot-password)
 *  - sent    → confirmación de envío
 *
 * El stage "reset" (crear nueva contraseña) vive en /reset-password — se
 * accede desde el enlace que el usuario recibe por email.
 */
type Stage = 'login' | 'forgot' | 'sent'

export function Login() {
  const [stage, setStage] = useState<Stage>('login')
  const [resetEmail, setResetEmail] = useState('')

  return (
    <div className="relative min-h-screen overflow-hidden bg-cream flex items-center justify-center px-6 py-10">
      {/* Atmospheric background — 3 burbujas blur */}
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
          {stage === 'login' && (
            <LoginPanel onForgot={() => setStage('forgot')} />
          )}
          {stage === 'forgot' && (
            <ForgotPanel
              onBack={() => setStage('login')}
              onSent={(email) => {
                setResetEmail(email)
                setStage('sent')
              }}
            />
          )}
          {stage === 'sent' && (
            <SentPanel
              email={resetEmail}
              onBack={() => setStage('login')}
              onResend={() => setStage('forgot')}
            />
          )}
        </div>

        {/* Footer */}
        <div className="text-center mt-6 text-[11.5px] text-warm-500">
          ¿Necesitas ayuda?{' '}
          <a
            href="https://wa.me/573001234567"
            target="_blank"
            rel="noopener noreferrer"
            className="text-brand-700 underline-offset-2 hover:underline"
          >
            Contáctanos por WhatsApp
          </a>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Stage: login                                                              */
/* -------------------------------------------------------------------------- */

function LoginPanel({ onForgot }: { onForgot: () => void }) {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [serverError, setServerError] = useState<string | null>(null)
  const [showPwd, setShowPwd] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const fromState = (location.state as { from?: { pathname: string } } | null)?.from
  const redirectTo = fromState?.pathname ?? '/dashboard'

  const onSubmit = async (data: LoginFormData) => {
    setServerError(null)
    try {
      await login(data)
      navigate(redirectTo, { replace: true })
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo iniciar sesión.'))
    }
  }

  return (
    <>
      <div className="text-center mb-6 mt-1">
        <h1 className="font-serif text-[34px] leading-tight text-warm-800">
          Iniciar sesión
        </h1>
        <p className="text-[13px] text-warm-500 mt-1">Bienvenida de vuelta</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        {serverError && (
          <div
            role="alert"
            className="rounded-lg bg-terra-100/50 border border-terra-300/60 px-3.5 py-2.5 text-[12.5px] text-terra-500 flex items-start gap-2 anim-fade"
          >
            <AlertCircle size={13} className="flex-shrink-0 mt-0.5" />
            <span>{serverError}</span>
          </div>
        )}

        <FormField label="Correo electrónico" error={errors.email?.message}>
          <BareInput
            type="email"
            autoComplete="email"
            autoFocus
            placeholder="tu@salon.co"
            error={!!errors.email}
            {...register('email')}
          />
        </FormField>

        <FormField
          label="Contraseña"
          error={errors.password?.message}
          right={
            <button
              type="button"
              onClick={onForgot}
              className="text-[11.5px] text-brand-700 hover:text-brand-800 hover:underline"
            >
              ¿Olvidaste tu contraseña?
            </button>
          }
        >
          <div className="relative">
            <BareInput
              type={showPwd ? 'text' : 'password'}
              autoComplete="current-password"
              placeholder="••••••••"
              error={!!errors.password}
              {...register('password')}
              className="pr-10"
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
        </FormField>

        <label className="flex items-center gap-2 text-[12.5px] text-warm-600 select-none cursor-pointer">
          <input
            type="checkbox"
            defaultChecked
            className="w-4 h-4 rounded border-warm-300 text-brand-700 focus:ring-brand-200"
          />
          Mantener sesión iniciada
        </label>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft transition disabled:opacity-60 disabled:cursor-not-allowed"
        >
          {isSubmitting ? 'Iniciando sesión…' : 'Iniciar sesión'}
          {!isSubmitting && <ArrowRight size={14} />}
        </button>

        <p className="text-center text-[12.5px] text-warm-600">
          ¿Aún no tienes cuenta?{' '}
          <Link to="/register" className="text-brand-700 font-medium hover:underline">
            Crear salón
          </Link>
        </p>
      </form>
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*  Stage: forgot                                                             */
/* -------------------------------------------------------------------------- */

function ForgotPanel({
  onBack,
  onSent,
}: {
  onBack: () => void
  onSent: (email: string) => void
}) {
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormData>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  })

  const onSubmit = async (data: ForgotPasswordFormData) => {
    setServerError(null)
    try {
      await apiForgotPassword({ email: data.email })
      onSent(data.email)
    } catch (e) {
      setServerError(extractApiError(e, 'No se pudo enviar el enlace.'))
    }
  }

  return (
    <div className="anim-fade">
      <button
        type="button"
        onClick={onBack}
        className="text-[12.5px] text-warm-600 hover:text-warm-900 flex items-center gap-1.5 mb-4"
      >
        <ArrowLeft size={14} />
        Volver al login
      </button>

      <div className="mb-6">
        <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Recuperar acceso
        </div>
        <h1 className="font-serif text-[32px] leading-tight text-warm-800 mt-2">
          ¿Olvidaste tu contraseña?
        </h1>
        <p className="text-[13.5px] text-warm-500 mt-2">
          Sin problema. Te enviamos un enlace al correo.
        </p>
      </div>

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

        <FormField
          label="Correo electrónico"
          error={errors.email?.message}
          hint="Te enviaremos un enlace para restablecer tu contraseña"
        >
          <BareInput
            type="email"
            autoComplete="email"
            autoFocus
            placeholder="tu@salon.co"
            error={!!errors.email}
            {...register('email')}
          />
        </FormField>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft transition disabled:opacity-60 disabled:cursor-not-allowed"
        >
          {isSubmitting ? 'Enviando…' : 'Enviar enlace'}
          {!isSubmitting && <ArrowRight size={14} />}
        </button>
      </form>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Stage: sent — confirmación de envío                                       */
/* -------------------------------------------------------------------------- */

function SentPanel({
  email,
  onBack,
  onResend,
}: {
  email: string
  onBack: () => void
  onResend: () => void
}) {
  return (
    <div className="text-center anim-fade">
      <div className="w-16 h-16 rounded-full bg-brand-50 text-brand-700 flex items-center justify-center mx-auto">
        <Mail size={32} strokeWidth={1.4} />
      </div>
      <h3 className="font-serif text-[28px] text-warm-800 mt-5 leading-tight">
        Revisa tu correo
      </h3>
      <p className="text-[13.5px] text-warm-600 mt-2 leading-relaxed">
        Enviamos un enlace a{' '}
        <span className="font-medium text-warm-800 break-all">
          {email || 'tu correo'}
        </span>
        . Sigue las instrucciones para restablecer tu contraseña.
      </p>

      <button
        type="button"
        onClick={onBack}
        className="mt-6 w-full px-5 py-3 rounded-xl bg-brand-700 hover:bg-brand-800 text-white text-[14px] font-medium flex items-center justify-center gap-2 shadow-soft"
      >
        <ArrowLeft size={14} />
        Volver al login
      </button>

      <div className="mt-6 pt-5 border-t border-warm-150 text-[11.5px] text-warm-500">
        ¿No te llegó?{' '}
        <button type="button" onClick={onResend} className="underline hover:text-warm-800">
          Reenviar
        </button>{' '}
        ·{' '}
        <button type="button" onClick={onResend} className="underline hover:text-warm-800">
          Cambiar correo
        </button>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Sub-primitives reutilizadas                                               */
/* -------------------------------------------------------------------------- */

function FormField({
  label,
  error,
  hint,
  right,
  children,
}: {
  label: string
  error?: string
  hint?: string
  right?: ReactNode
  children: ReactNode
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between mb-1.5">
        <label className="text-[12.5px] font-medium text-warm-700">{label}</label>
        {right}
      </div>
      {children}
      {error && (
        <div className="text-[11px] text-terra-500 mt-1 flex items-center gap-1">
          <AlertCircle size={11} />
          {error}
        </div>
      )}
      {hint && !error && (
        <div className="text-[11.5px] text-warm-500 mt-1">{hint}</div>
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
