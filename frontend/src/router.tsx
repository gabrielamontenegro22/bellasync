import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Login } from '@/pages/Login'
import { ResetPassword } from '@/pages/ResetPassword'
import { Dashboard } from '@/pages/Dashboard'
import { OnboardingWizard } from '@/features/onboarding/OnboardingWizard'
import { ConfigLayout } from '@/pages/Settings/ConfigLayout'
import { ServicesPage } from '@/features/services/ServicesPage'
import { StylistsPage } from '@/features/stylists/StylistsPage'
import { AgendaPage } from '@/features/appointments/AgendaPage'
import { ValidationQueuePage } from '@/features/vouchers/ValidationQueuePage'
import { BookingPage } from '@/features/booking/BookingPage'
import { CustomersPage } from '@/features/customers/CustomersPage'
import { PaymentPolicyPage } from '@/features/settings/PaymentPolicyPage'
import { CommissionsSettingPage } from '@/features/settings/CommissionsSettingPage'
import { TenantInfoPage } from '@/features/settings/TenantInfoPage'
import { HorarioPage } from '@/features/settings/HorarioPage'
import { WhatsAppPage } from '@/features/settings/WhatsAppPage'
import { SuscripcionPage } from '@/features/settings/SuscripcionPage'
import { CommissionsPage } from '@/features/commissions/CommissionsPage'
import { CashClosingPage } from '@/features/cash/CashClosingPage'
import { ReportsPage } from '@/features/reports/ReportsPage'
import { AppShell } from '@/components/layout/AppShell'
import { ProtectedRoute } from '@/components/layout/ProtectedRoute'
import { useAuth } from '@/features/auth/useAuth'

/**
 * Si la usuaria ya está autenticada y entra a /login o /register,
 * la mandamos al dashboard. /reset-password queda accesible siempre
 * (puede llegar un link al email mientras tiene sesión).
 */
function PublicOnly({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <Navigate to="/dashboard" replace /> : <>{children}</>
}

export function AppRouter() {
  return (
    <Routes>
      {/* Públicas */}
      <Route path="/login"               element={<PublicOnly><Login /></PublicOnly>} />
      <Route path="/register"            element={<PublicOnly><OnboardingWizard /></PublicOnly>} />
      <Route path="/reset-password"      element={<ResetPassword />} />

      {/* Portal público de booking — anónimo, sin AppShell */}
      <Route path="/booking/:tenantSlug" element={<BookingPage />} />

      {/* Dashboard placeholder */}
      <Route
        path="/dashboard"
        element={<ProtectedRoute><Dashboard /></ProtectedRoute>}
      />

      {/* Agenda (la nueva home tras login para recepción) */}
      <Route
        path="/agenda"
        element={
          <ProtectedRoute>
            <AppShell><AgendaPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* CRM de clientes */}
      <Route
        path="/clientes"
        element={
          <ProtectedRoute>
            <AppShell><CustomersPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Cierre de caja del día — lo que la admin abre cada noche */}
      <Route
        path="/caja"
        element={
          <ProtectedRoute>
            <AppShell><CashClosingPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Servicios — catálogo del salón (top-level: uso diario/semanal) */}
      <Route
        path="/servicios"
        element={
          <ProtectedRoute>
            <AppShell><ServicesPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Estilistas — equipo del salón (top-level) */}
      <Route
        path="/estilistas"
        element={
          <ProtectedRoute>
            <AppShell><StylistsPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Validación de pagos — cola operativa, uso diario */}
      <Route
        path="/validacion"
        element={
          <ProtectedRoute>
            <AppShell><ValidationQueuePage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Redirects: URLs viejas /configuracion/{servicios,estilistas,validacion}
          siguen funcionando (bookmarks, links viejos) pero apuntan a las
          nuevas top-level. Las dejamos un par de versiones y después se
          quitan. */}
      <Route path="/configuracion/servicios"  element={<Navigate to="/servicios"  replace />} />
      <Route path="/configuracion/estilistas" element={<Navigate to="/estilistas" replace />} />
      <Route path="/configuracion/validacion" element={<Navigate to="/validacion" replace />} />

      {/* Configuración — sólo lo que es ajuste real (info, horario,
          política, plantillas, suscripción). Lo operativo vive arriba. */}
      <Route
        path="/configuracion"
        element={<ProtectedRoute><ConfigLayout /></ProtectedRoute>}
      >
        {/* /configuracion → redirige a /configuracion/pagos (única sección
            con pantalla real por ahora) */}
        <Route index element={<Navigate to="general" replace />} />
        <Route path="general"     element={<TenantInfoPage />} />
        <Route path="horario"     element={<HorarioPage />} />
        <Route path="pagos"       element={<PaymentPolicyPage />} />
        <Route path="comisiones"  element={<CommissionsSettingPage />} />
        <Route path="whatsapp"    element={<WhatsAppPage />} />
        <Route path="suscripcion" element={<SuscripcionPage />} />
      </Route>

      {/* Comisiones de estilistas — opt-in. Si el tenant no la activó,
          el sidebar oculta el item, pero la URL sigue accesible y la
          propia pantalla muestra un empty state explicando cómo activarla. */}
      <Route
        path="/comisiones"
        element={
          <ProtectedRoute>
            <AppShell><CommissionsPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Reportes — dashboard de KPIs (solo SalonAdmin lo ve útil, pero
          el endpoint también lo restringe). */}
      <Route
        path="/reportes"
        element={
          <ProtectedRoute>
            <AppShell><ReportsPage /></AppShell>
          </ProtectedRoute>
        }
      />

      {/* Defaults */}
      <Route path="/"  element={<Navigate to="/dashboard" replace />} />
      <Route path="*"  element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
