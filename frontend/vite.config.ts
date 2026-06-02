import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // Alias `@` apunta a /src.
      // Permite imports limpios:  import { Button } from '@/components/ui'
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      // En dev, todas las llamadas a /api/... se proxean al backend.
      // Mismo origen lógico → la cookie HttpOnly del refresh token va y
      // viene sin problemas de SameSite/CORS.
      '/api': {
        target: 'http://localhost:5059',
        changeOrigin: true,
      },
    },
  },
})
