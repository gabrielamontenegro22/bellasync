import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Login } from '@/pages/Login'
import { ResetPassword } from '@/pages/ResetPassword'
import { Dashboard } from '@/pages/Dashboard'
import { OnboardingWizard } from '@/features/onboarding/OnboardingWizard'
import { ProtectedRoute } from '@/components/layout/ProtectedRoute'
import { useAuth } from '@/features/auth/useAuth'

/**
 * Si la usuaria ya está autenticada y entra a /login o /register,
 * la mandamos al dashboard. Evita ver login cuando ya hay sesión.
 *
 * /reset-password NO se considera "publico-only" porque alguien con sesión
 * activa puede recibir un link de reset y querer usarlo igual.
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

      {/* Protegidas */}
      <Route
        path="/dashboard"
        element={<ProtectedRoute><Dashboard /></ProtectedRoute>}
      />

      {/* Defaults */}
      <Route path="/"  element={<Navigate to="/dashboard" replace />} />
      <Route path="*"  element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
