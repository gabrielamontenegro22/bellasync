import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate } from 'react-router-dom'
import { Building2, User, Mail, Lock } from 'lucide-react'

import { Button, Input, Card } from '@/components/ui'
import { PublicLayout } from '@/components/layout/PublicLayout'
import { registerSchema, type RegisterFormData } from '@/features/auth/schemas'
import { useAuth } from '@/features/auth/useAuth'
import { extractApiError, extractFieldErrors } from '@/lib/extractApiError'

export function Register() {
  const { register: registerSalon } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: { salonName: '', adminFullName: '', adminEmail: '', adminPassword: '' },
  })

  const onSubmit = async (data: RegisterFormData) => {
    setServerError(null)
    try {
      await registerSalon(data)
      navigate('/dashboard', { replace: true })
    } catch (e) {
      // Si el backend devolvió 400 con errores POR campo, los pintamos en cada Input
      const fieldErrors = extractFieldErrors(e)
      let mappedAny = false
      for (const [field, msg] of Object.entries(fieldErrors)) {
        if (field in data) {
          setError(field as keyof RegisterFormData, { message: msg })
          mappedAny = true
        }
      }
      // 409 (email duplicado), 500, error de red, etc.
      if (!mappedAny) {
        setServerError(extractApiError(e, 'No se pudo registrar el salón.'))
      }
    }
  }

  return (
    <PublicLayout>
      <Card variant="elevated" padding="lg">
        <header className="text-center mb-6">
          <h1 className="font-serif text-[32px] text-warm-800 leading-tight">Crear cuenta</h1>
          <p className="text-[13px] text-warm-500 mt-1">Registrá tu salón en BellaSync</p>
        </header>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <Input
            label="Nombre del salón"
            placeholder="Ej. Bella Aurora Neiva"
            leftIcon={<Building2 size={16} />}
            error={errors.salonName?.message}
            {...register('salonName')}
          />
          <Input
            label="Tu nombre completo"
            placeholder="Como administradora del salón"
            leftIcon={<User size={16} />}
            autoComplete="name"
            error={errors.adminFullName?.message}
            {...register('adminFullName')}
          />
          <Input
            label="Correo electrónico"
            type="email"
            autoComplete="email"
            placeholder="tu@correo.com"
            leftIcon={<Mail size={16} />}
            error={errors.adminEmail?.message}
            {...register('adminEmail')}
          />
          <Input
            label="Contraseña"
            type="password"
            autoComplete="new-password"
            placeholder="Mínimo 8 caracteres"
            leftIcon={<Lock size={16} />}
            hint="Al menos: 8 caracteres, una mayúscula, una minúscula y un número."
            error={errors.adminPassword?.message}
            {...register('adminPassword')}
          />

          {serverError && (
            <div
              role="alert"
              className="rounded-lg bg-terra-100 border border-terra-300 px-3 py-2 text-[12.5px] text-terra-500"
            >
              {serverError}
            </div>
          )}

          <Button type="submit" fullWidth loading={isSubmitting}>
            Crear cuenta
          </Button>
        </form>

        <p className="text-center text-[13px] text-warm-600 mt-6">
          ¿Ya tenés cuenta?{' '}
          <Link to="/login" className="text-brand-700 hover:text-brand-800 font-medium">
            Iniciá sesión
          </Link>
        </p>
      </Card>
    </PublicLayout>
  )
}
