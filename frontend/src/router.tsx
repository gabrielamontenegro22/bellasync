import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Login } from '@/pages/Login'
import { ResetPassword } from '@/pages/ResetPassword'
import { Dashboard } from '@/pages/Dashboard'
import { MyAccountPage } from '@/pages/MyAccount'
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
import { UsuariosPage } from '@/features/settings/UsuariosPage'
import { PermissionsPage } from '@/features/settings/PermissionsPage'
import { CommissionsPage } from '@/features/commissions/CommissionsPage'
import { CashClosingPage } from '@/features/cash/CashClosingPage'
import { ReportsPage } from '@/features/reports/ReportsPage'
import { SaasAdminSubscriptionsPage } from '@/features/saasAdmin/SaasAdminSubscriptionsPage'
import { AppShell } from '@/components/layout/AppShell'
import { ProtectedRoute } from '@/components/layout/ProtectedRoute'
import { RequireRole } from '@/components/layout/RequireRole'
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

      {/* Dashboard — home post-login para usuarias del salón (admin,
          recepción, stylist). SuperAdmin NO debería entrar acá: no tiene
          tenant asociado y el dashboard se quedaría cargando para siempre.
          RequireRole lo redirige a su panel propio /saas-admin/. */}
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <RequireRole roles={['SalonAdmin', 'Receptionist', 'Stylist']}>
              <Dashboard />
            </RequireRole>
          </ProtectedRoute>
        }
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
          política, plantillas, suscripción). Lo operativo vive arriba.
          SalonAdmin-only: recepción no debería ver info de suscripción,
          usuarios del salón, ni cambiar políticas de pago. */}
      <Route
        path="/configuracion"
        element={
          <ProtectedRoute>
            <RequireRole roles={['SalonAdmin']}>
              <ConfigLayout />
            </RequireRole>
          </ProtectedRoute>
        }
      >
        {/* /configuracion → redirige a /configuracion/pagos (única sección
            con pantalla real por ahora) */}
        <Route index element={<Navigate to="general" replace />} />
        <Route path="general"     element={<TenantInfoPage />} />
        <Route path="horario"     element={<HorarioPage />} />
        <Route path="pagos"       element={<PaymentPolicyPage />} />
        <Route path="comisiones"  element={<CommissionsSettingPage />} />
        <Route path="whatsapp"    element={<WhatsAppPage />} />
        <Route path="usuarios"    element={<UsuariosPage />} />
        <Route path="permisos"    element={<PermissionsPage />} />
        <Route path="suscripcion" element={<SuscripcionPage />} />
      </Route>

      {/* Comisiones de estilistas — opt-in. SalonAdmin-only: implica ver
          ingresos por estilista y liquidar pagos, no es info para recepción. */}
      <Route
        path="/comisiones"
        element={
          <ProtectedRoute>
            <RequireRole roles={['SalonAdmin']}>
              <AppShell><CommissionsPage /></AppShell>
            </RequireRole>
          </ProtectedRoute>
        }
      />

      {/* Reportes — KPIs financieros del salón. SalonAdmin-only. */}
      <Route
        path="/reportes"
        element={
          <ProtectedRoute>
            <RequireRole roles={['SalonAdmin']}>
              <AppShell><ReportsPage /></AppShell>
            </RequireRole>
          </ProtectedRoute>
        }
      />

      {/* Panel SaaS Admin — exclusivo del dueño de BellaSync (SuperAdmin).
          Si un SalonAdmin/Receptionist intenta entrar, lo mandamos a su
          /agenda. El backend igual rechaza con 403, esto es defensa UI. */}
      <Route
        path="/saas-admin/subscriptions"
        element={
          <ProtectedRoute>
            <RequireRole roles={['SuperAdmin']}>
              <AppShell><SaasAdminSubscriptionsPage /></AppShell>
            </RequireRole>
          </ProtectedRoute>
        }
      />

      {/* Mi cuenta — accesible para CUALQUIER user autenticado (todos los
          roles). Sin RequireRole: hasta el SuperAdmin debería poder cambiar
          su propio password. La page monta AppShell internamente. */}
      <Route
        path="/mi-cuenta"
        element={<ProtectedRoute><MyAccountPage /></ProtectedRoute>}
      />

      {/* Defaults */}
      <Route path="/"  element={<Navigate to="/dashboard" replace />} />
      <Route path="*"  element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
