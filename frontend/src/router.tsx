import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Login } from '@/pages/Login'
import { ResetPassword } from '@/pages/ResetPassword'
import { Dashboard } from '@/pages/Dashboard'
import { OnboardingWizard } from '@/features/onboarding/OnboardingWizard'
import { ConfigLayout } from '@/pages/Settings/ConfigLayout'
import { ServicesPage } from '@/features/services/ServicesPage'
import { StylistsPage } from '@/features/stylists/StylistsPage'
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
      <Route path="/login"          element={<PublicOnly><Login /></PublicOnly>} />
      <Route path="/register"       element={<PublicOnly><OnboardingWizard /></PublicOnly>} />
      <Route path="/reset-password" element={<ResetPassword />} />

      {/* Dashboard placeholder */}
      <Route
        path="/dashboard"
        element={<ProtectedRoute><Dashboard /></ProtectedRoute>}
      />

      {/* Configuración (layout con outlet) */}
      <Route
        path="/configuracion"
        element={<ProtectedRoute><ConfigLayout /></ProtectedRoute>}
      >
        {/* /configuracion → redirige a /configuracion/servicios */}
        <Route index element={<Navigate to="servicios" replace />} />
        <Route path="servicios"  element={<ServicesPage />} />
        <Route path="estilistas" element={<StylistsPage />} />
        {/* Aquí se agregarán: general, horario, pagos, whatsapp, suscripcion */}
      </Route>

      {/* Defaults */}
      <Route path="/"  element={<Navigate to="/dashboard" replace />} />
      <Route path="*"  element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
