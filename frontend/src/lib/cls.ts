/**
 * cls — combina clases CSS de forma condicional.
 *
 * Filtra valores falsy (false, null, undefined, '') y une el resto con espacio.
 * Es el patrón que usan los mockups de BellaSync para componer clases Tailwind.
 *
 * @example
 *   cls('px-4 py-2', isActive && 'bg-brand-50', disabled && 'opacity-50')
 *   // → 'px-4 py-2 bg-brand-50'  (si isActive=true y disabled=false)
 */
export function cls(...args: Array<string | false | null | undefined>): string {
  return args.filter(Boolean).join(' ')
}
