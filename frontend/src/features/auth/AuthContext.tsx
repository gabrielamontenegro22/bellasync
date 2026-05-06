import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authStorage } from './storage'
import { AUTH_LOGOUT_EVENT } from '@/api/axios'
import { login as apiLogin, registerSalon as apiRegister } from '@/api/auth'
import type {
  AuthResponse,
  AuthenticatedUser,
  LoginRequest,
  RegisterSalonRequest,
} from '@/types/auth'

interface AuthContextValue {
  user: AuthenticatedUser | null
  token: string | null
  isAuthenticated: boolean
  login(credentials: LoginRequest): Promise<AuthResponse>
  register(data: RegisterSalonRequest): Promise<AuthResponse>
  logout(): void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

/**
 * Provider que mantiene el estado de sesión.
 * Hidrata desde localStorage al montar y se sincroniza con el evento global
 * `bellasync:auth:logout` que dispara el interceptor de Axios al recibir 401.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser]   = useState<AuthenticatedUser | null>(() => authStorage.getUser())
  const [token, setToken] = useState<string | null>(() => authStorage.getToken())

  const handleAuthSuccess = useCallback((response: AuthResponse) => {
    authStorage.save(response)
    setToken(response.token)
    setUser({
      userId:     response.userId,
      email:      response.email,
      fullName:   response.fullName,
      role:       response.role,
      tenantId:   response.tenantId,
      tenantName: response.tenantName,
      tenantSlug: response.tenantSlug,
    })
  }, [])

  const login = useCallback(
    async (credentials: LoginRequest) => {
      const response = await apiLogin(credentials)
      handleAuthSuccess(response)
      return response
    },
    [handleAuthSuccess],
  )

  const register = useCallback(
    async (data: RegisterSalonRequest) => {
      const response = await apiRegister(data)
      handleAuthSuccess(response)
      return response
    },
    [handleAuthSuccess],
  )

  const logout = useCallback(() => {
    authStorage.clear()
    setToken(null)
    setUser(null)
  }, [])

  // Logout automático cuando el interceptor detecta 401
  useEffect(() => {
    const onForceLogout = () => {
      setToken(null)
      setUser(null)
    }
    window.addEventListener(AUTH_LOGOUT_EVENT, onForceLogout)
    return () => window.removeEventListener(AUTH_LOGOUT_EVENT, onForceLogout)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isAuthenticated: Boolean(token && user),
      login,
      register,
      logout,
    }),
    [user, token, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
