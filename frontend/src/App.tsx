import { AppRouter } from './router'

/**
 * Entry point de la aplicación.
 * Toda la lógica de rutas vive en src/router.tsx.
 *
 * Los providers (QueryClient, BrowserRouter, Auth) están en src/main.tsx.
 */
function App() {
  return <AppRouter />
}

export default App
