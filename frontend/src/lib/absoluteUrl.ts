/**
 * Convierte una URL relativa al backend (ej "/uploads/logos/abc.jpg",
 * que viene de IFileStorage) en URL absoluta usando VITE_API_BASE_URL.
 *
 * Si la URL ya es absoluta (http://, https://) o es null/empty, la
 * devuelve tal cual.
 *
 * Por qué: los <img src=...> del frontend corren en localhost:5173 pero
 * los archivos subidos están servidos por la API en localhost:5059. Sin
 * resolver, el browser intentaría buscar la imagen en el origin del
 * frontend y daría 404.
 */
export function absoluteUrl(url: string | null | undefined): string {
  if (!url) return ''
  if (/^https?:\/\//i.test(url)) return url
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5059'
  const path = url.startsWith('/') ? url : '/' + url
  return base.replace(/\/$/, '') + path
}
