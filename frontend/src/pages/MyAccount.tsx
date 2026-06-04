import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { User as UserIcon, Lock, Save, Eye, EyeOff, CheckCircle2 } from 'lucide-react'
import { AppShell } from '@/components/layout/AppShell'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { extractApiError } from '@/lib/extractApiError'
import { getMyProfile, updateMyProfile, changeMyPassword } from '@/api/auth'

/**
 * Página /mi-cuenta. La ven todos los roles (admin, recepción, stylist
 * si llega a tener UI). Permite al user:
 *  - Ver su perfil (rol, email, nombre del salón) — read-only
 *  - Editar su nombre completo
 *  - Cambiar su contraseña verificando la actual
 *
 * El email no se puede editar acá: cambiar email requiere flujo de
 * verificación con doble confirmación que está fuera del scope v1.
 */
export function MyAccountPage() {
  return (
    <AppShell>
      <div className="px-6 lg:px-10 py-8 max-w-3xl mx-auto">
        <div className="mb-7">
          <div className="text-[11px] tracking-[0.2em] uppercase text-gold-600 font-medium">
            Tu sesión
          </div>
          <h1 className="font-serif text-[34px] sm:text-[40px] leading-[1.02] tracking-tight text-warm-800 mt-1">
            Mi cuenta
          </h1>
          <p className="text-[13.5px] text-warm-600 mt-2.5">
            Tu información personal y la contraseña con la que entrás a BellaSync.
          </p>
        </div>

        <ProfileSection />
        <PasswordSection />
      </div>
    </AppShell>
  )
}

// ───────────────────────────────────────────────────────────────────────

function ProfileSection() {
  const qc = useQueryClient()
  const profileQ = useQuery({
    queryKey: ['myProfile'],
    queryFn: getMyProfile,
  })

  const [fullName, setFullName] = useState('')
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const [err, setErr] = useState<string | null>(null)

  // Hidratamos el form cuando llegan los datos.
  useEffect(() => {
    if (profileQ.data) setFullName(profileQ.data.fullName)
  }, [profileQ.data])

  const mut = useMutation({
    mutationFn: (name: string) => updateMyProfile({ fullName: name }),
    onSuccess: (data) => {
      qc.setQueryData(['myProfile'], data)
      // También invalidamos el AuthContext / sidebar para que actualice
      // el nombre mostrado en el footer. La forma simple: forzar refetch
      // del dashboard que comparte muchos consumidores.
      setSavedAt(Date.now())
      setErr(null)
    },
    onError: (e) => setErr(extractApiError(e, 'No se pudo guardar el nombre.')),
  })

  if (profileQ.isLoading) {
    return <div className="h-44 rounded-2xl bg-warm-100 animate-pulse mb-6" />
  }
  if (!profileQ.data) return null

  const isDirty = fullName.trim() !== profileQ.data.fullName && fullName.trim().length >= 2

  return (
    <section className="rounded-2xl border border-warm-150 bg-white p-6 mb-6">
      <div className="flex items-center gap-2 text-[13px] font-semibold text-warm-800 mb-1">
        <UserIcon size={15} className="text-brand-700" />
        Información personal
      </div>
      <p className="text-[12.5px] text-warm-500 mb-5">
        Estos datos identifican tu cuenta dentro del salón.
      </p>

      <div className="grid sm:grid-cols-2 gap-4">
        <Input
          label="Nombre completo"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          placeholder="Ej. María González"
          maxLength={120}
        />
        <Input
          label="Email"
          value={profileQ.data.email}
          disabled
          hint="Para cambiarlo, pedile a la administradora del salón."
        />
        <Input
          label="Rol"
          value={roleLabel(profileQ.data.role)}
          disabled
        />
        <Input
          label="Salón"
          value={profileQ.data.tenantName ?? '—'}
          disabled
        />
      </div>

      {err && (
        <div className="mt-4 rounded-md bg-terra-100 px-3 py-2 text-[12.5px] text-terra-700">
          {err}
        </div>
      )}

      <div className="mt-5 flex items-center justify-between gap-3">
        <div className="text-[11.5px] text-warm-500">
          {savedAt && Date.now() - savedAt < 4000 ? (
            <span className="inline-flex items-center gap-1.5 text-brand-700">
              <CheckCircle2 size={13} /> Cambios guardados
            </span>
          ) : (
            <>Último acceso:{' '}
              {profileQ.data.lastLoginAt
                ? new Date(profileQ.data.lastLoginAt).toLocaleString('es-CO', {
                    dateStyle: 'medium', timeStyle: 'short',
                  })
                : 'sin registro'}
            </>
          )}
        </div>
        <Button
          size="sm"
          leftIcon={<Save size={14} />}
          onClick={() => { setErr(null); mut.mutate(fullName.trim()) }}
          disabled={!isDirty}
          loading={mut.isPending}
        >
          Guardar cambios
        </Button>
      </div>
    </section>
  )
}

// ───────────────────────────────────────────────────────────────────────

function PasswordSection() {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [showCurrent, setShowCurrent] = useState(false)
  const [showNext, setShowNext] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const [okAt, setOkAt] = useState<number | null>(null)

  const mut = useMutation({
    mutationFn: () => changeMyPassword({ currentPassword: current, newPassword: next }),
    onSuccess: () => {
      // Limpiar inputs + mostrar confirmación. La sesión actual sigue
      // viva (el backend solo revoca refresh tokens en otros devices).
      setCurrent('')
      setNext('')
      setConfirm('')
      setOkAt(Date.now())
      setErr(null)
    },
    onError: (e) => setErr(extractApiError(e, 'No se pudo cambiar la contraseña.')),
  })

  // Validación local mínima antes de mandar (la dura va en el backend).
  const localError = (() => {
    if (!current || !next || !confirm) return null
    if (next !== confirm) return 'La confirmación no coincide con la contraseña nueva.'
    if (next.length < 8) return 'La contraseña nueva debe tener al menos 8 caracteres.'
    if (!/[A-Z]/.test(next)) return 'Tiene que incluir al menos una letra mayúscula.'
    if (!/[a-z]/.test(next)) return 'Tiene que incluir al menos una letra minúscula.'
    if (!/[0-9]/.test(next)) return 'Tiene que incluir al menos un número.'
    if (next === current) return 'La nueva debe ser distinta a la actual.'
    return null
  })()

  const canSubmit = current && next && confirm && !localError && !mut.isPending

  return (
    <section className="rounded-2xl border border-warm-150 bg-white p-6">
      <div className="flex items-center gap-2 text-[13px] font-semibold text-warm-800 mb-1">
        <Lock size={15} className="text-brand-700" />
        Cambiar contraseña
      </div>
      <p className="text-[12.5px] text-warm-500 mb-5">
        Al cambiarla, se cerrarán las sesiones que tengas abiertas en otros
        dispositivos (acá vas a seguir logueada).
      </p>

      <div className="space-y-4 max-w-md">
        <Input
          label="Contraseña actual"
          type={showCurrent ? 'text' : 'password'}
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
          autoComplete="current-password"
          rightIcon={
            <button
              type="button"
              onClick={() => setShowCurrent((v) => !v)}
              className="text-warm-400 hover:text-warm-600"
              aria-label={showCurrent ? 'Ocultar' : 'Mostrar'}
            >
              {showCurrent ? <EyeOff size={15} /> : <Eye size={15} />}
            </button>
          }
        />
        <Input
          label="Contraseña nueva"
          type={showNext ? 'text' : 'password'}
          value={next}
          onChange={(e) => setNext(e.target.value)}
          autoComplete="new-password"
          hint="Mínimo 8 caracteres, con mayúscula, minúscula y número."
          rightIcon={
            <button
              type="button"
              onClick={() => setShowNext((v) => !v)}
              className="text-warm-400 hover:text-warm-600"
              aria-label={showNext ? 'Ocultar' : 'Mostrar'}
            >
              {showNext ? <EyeOff size={15} /> : <Eye size={15} />}
            </button>
          }
        />
        <Input
          label="Repetir contraseña nueva"
          type={showNext ? 'text' : 'password'}
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          autoComplete="new-password"
          error={confirm && next && confirm !== next ? 'No coincide.' : undefined}
        />
      </div>

      {localError && current && next && confirm && (
        <div className="mt-4 rounded-md bg-gold-50 border border-gold-200 px-3 py-2 text-[12.5px] text-gold-700">
          {localError}
        </div>
      )}

      {err && (
        <div className="mt-4 rounded-md bg-terra-100 px-3 py-2 text-[12.5px] text-terra-700">
          {err}
        </div>
      )}

      {okAt && Date.now() - okAt < 6000 && (
        <div className="mt-4 rounded-md bg-brand-50 border border-brand-200 px-3 py-2 text-[12.5px] text-brand-800 flex items-center gap-1.5">
          <CheckCircle2 size={14} /> Contraseña actualizada. Usá la nueva la próxima vez que inicies sesión.
        </div>
      )}

      <div className="mt-5">
        <Button
          size="sm"
          leftIcon={<Lock size={14} />}
          onClick={() => { setErr(null); mut.mutate() }}
          disabled={!canSubmit}
          loading={mut.isPending}
        >
          Cambiar contraseña
        </Button>
      </div>
    </section>
  )
}

function roleLabel(role: string): string {
  switch (role) {
    case 'SalonAdmin':   return 'Administradora'
    case 'Receptionist': return 'Recepción'
    case 'Stylist':      return 'Estilista'
    case 'SuperAdmin':   return 'SaaS Admin'
    default:             return role
  }
}
