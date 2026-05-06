import type { ReactNode } from 'react'
import { LogOut } from 'lucide-react'
import { Button, Card, Badge } from '@/components/ui'
import { useAuth } from '@/features/auth/useAuth'

/**
 * Dashboard placeholder — confirma que el flujo de auth cierra end-to-end.
 * Se reemplaza en F4+ por el dashboard real (Agenda de Hoy / Configuración).
 */
export function Dashboard() {
  const { user, logout } = useAuth()
  if (!user) return null

  const firstName = user.fullName.split(' ')[0]

  return (
    <div className="min-h-screen bg-cream px-6 py-10">
      <div className="max-w-3xl mx-auto space-y-6">

        {/* Top bar */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-brand-700 text-white flex items-center justify-center font-serif text-[22px] leading-none translate-y-[1px]">
              B
            </div>
            <span className="font-serif text-[28px] tracking-tight text-warm-800 leading-none">
              BellaSync
            </span>
          </div>
          <Button variant="secondary" leftIcon={<LogOut size={16} />} onClick={logout}>
            Cerrar sesión
          </Button>
        </div>

        {/* Welcome card */}
        <Card variant="elevated" padding="lg">
          <p className="text-[12.5px] uppercase tracking-[0.18em] text-gold-600 font-medium mb-2">
            Sesión iniciada
          </p>
          <h1 className="font-serif text-[40px] leading-tight text-warm-800">
            Bienvenida, {firstName}
          </h1>
          <p className="text-[15px] text-warm-600 mt-2">
            Estás operando como administradora de{' '}
            <strong className="text-warm-800">{user.tenantName}</strong>.
          </p>

          <div className="mt-6 grid sm:grid-cols-2 gap-4 text-[13px]">
            <Field label="Email" value={user.email} />
            <Field label="Rol"   value={<Badge tone="brand">{user.role}</Badge>} />
            <Field
              label="Tenant ID"
              value={<code className="font-mono text-[11px] text-warm-700 break-all">{user.tenantId}</code>}
            />
            <Field
              label="Tenant slug"
              value={<code className="font-mono text-[12px] text-warm-700">{user.tenantSlug}</code>}
            />
          </div>
        </Card>

        {/* Roadmap card */}
        <Card variant="default">
          <p className="text-[12.5px] text-warm-500">
            <span className="text-warm-700 font-medium">Próximos pasos (Bloques F4+):</span>{' '}
            Configuración → Servicios · Configuración → Estilistas · Agenda de Hoy.
          </p>
        </Card>

      </div>
    </div>
  )
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <div className="text-[10.5px] uppercase tracking-[0.14em] text-warm-400 font-medium mb-1">
        {label}
      </div>
      <div className="text-warm-800">{value}</div>
    </div>
  )
}
