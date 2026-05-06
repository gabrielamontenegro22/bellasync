import { QueryClient } from '@tanstack/react-query'

/**
 * QueryClient global con defaults razonables para BellaSync.
 *
 *  - staleTime 30s: las queries se consideran frescas durante 30s,
 *    así no hacemos refetch al volver a montar un componente.
 *  - refetchOnWindowFocus: false (ruido innecesario en una app interna).
 *  - retry: NO reintentamos 4xx (son errores del cliente, no de red).
 *  - mutations: sin retry (las escrituras pueden no ser idempotentes).
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error: unknown) => {
        const status = (error as { response?: { status?: number } })?.response?.status
        if (status && status >= 400 && status < 500) return false
        return failureCount < 1
      },
    },
    mutations: { retry: false },
  },
})
