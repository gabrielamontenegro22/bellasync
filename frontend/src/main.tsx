import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'

import './index.css'
import App from './App.tsx'
import { queryClient } from '@/lib/queryClient'
import { AuthProvider } from '@/features/auth/AuthContext'

/**
 * Orden de los providers (afuera hacia adentro):
 *
 *   QueryClientProvider  →  caché HTTP global de TanStack Query
 *   BrowserRouter        →  para que useNavigate / useLocation funcionen
 *   AuthProvider         →  estado de sesión
 *
 * AuthProvider va DESPUÉS de BrowserRouter porque internamente puede
 * disparar navegación (logout, redirect tras login).
 */
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </StrictMode>,
)
