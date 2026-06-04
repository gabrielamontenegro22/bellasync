import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, ImageIcon, AtSign, Phone, Globe } from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  getTenantInfo,
  updateTenantInfo,
  type UpdateTenantInfoRequest,
} from '@/api/admin'

/**
 * Configuración → Información general.
 *
 * La admin edita los datos públicos del salón: nombre, dirección,
 * teléfono, email, logo, Instagram, descripción. Esto se usa en:
 *  - Portal público de booking `/booking/:slug` (logo, descripción)
 *  - Plantillas de WhatsApp (nombre, teléfono) — futuro
 *  - Factura simple — futuro
 *
 * Slug y datos de auth/seguridad se manejan aparte; acá solo lo
 * "público + contacto".
 */
export function TenantInfoPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: ['tenantInfo'],
    queryFn: getTenantInfo,
  })

  // Form state local — se hidrata cuando llega la query.
  const [form, setForm] = useState<UpdateTenantInfoRequest | null>(null)
  useEffect(() => {
    if (data) {
      setForm({
        name: data.name,
        address: data.address ?? '',
        phone: data.phone ?? '',
        contactEmail: data.contactEmail ?? '',
        logoUrl: data.logoUrl ?? '',
        instagramHandle: data.instagramHandle ?? '',
        description: data.description ?? '',
      })
    }
  }, [data])

  const [error, setError] = useState<string | null>(null)
  const [savedRecently, setSavedRecently] = useState(false)

  const mut = useMutation({
    mutationFn: updateTenantInfo,
    onSuccess: (r) => {
      qc.setQueryData(['tenantInfo'], r)
      // Si el nombre cambió, invalidar otros lugares que lo muestran
      // (sidebar footer, agenda topbar leen del user — quedan stale
      // hasta logout; no es crítico, se ajustará en otro sprint).
      setError(null)
      setSavedRecently(true)
    },
    onError: (e) => setError(extractApiError(e, 'No se pudo guardar.')),
  })

  useEffect(() => {
    if (!savedRecently) return
    const t = setTimeout(() => setSavedRecently(false), 3000)
    return () => clearTimeout(t)
  }, [savedRecently])

  if (isLoading || !form) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">
        Cargando…
      </div>
    )
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.name.trim()) {
      setError('El nombre del salón es obligatorio.')
      return
    }
    // Normalizamos strings vacíos a null para que el backend los
    // interprete como "borrar campo" en lugar de "string vacío".
    mut.mutate({
      name: form.name.trim(),
      address: emptyToNull(form.address),
      phone: emptyToNull(form.phone),
      contactEmail: emptyToNull(form.contactEmail),
      logoUrl: emptyToNull(form.logoUrl),
      instagramHandle: emptyToNull(form.instagramHandle),
      description: emptyToNull(form.description),
    })
  }

  return (
    <form onSubmit={handleSubmit} className="px-6 lg:px-10 py-8 max-w-3xl">
      <div className="mb-6">
        <div className="text-[10.5px] tracking-[0.2em] uppercase text-gold-600 font-medium">
          Ajustes del salón
        </div>
        <h1 className="font-serif text-[32px] lg:text-[38px] leading-[1.1] text-warm-800 mt-1">
          Información general
        </h1>
        <p className="text-[13.5px] text-warm-500 mt-2 max-w-2xl leading-relaxed">
          Datos públicos y de contacto. Se usan en el portal de reservas, las
          notificaciones por WhatsApp y donde sea que tu salón aparezca con su
          marca.
        </p>
      </div>

      {/* Identidad */}
      <Section title="Identidad" icon={<Building2 size={15} strokeWidth={1.8} />}>
        <Field label="Nombre del salón" required>
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            placeholder="Ej: Bella Spa Neiva"
            className={inputClass}
          />
        </Field>
        <Field label="URL del booking" hint="Lo cambian desde soporte por ahora">
          <div className="flex items-center gap-1 px-3 py-2.5 rounded-lg border border-warm-200 bg-warm-50 text-warm-500 text-[13px]">
            <Globe size={13} className="text-warm-400" />
            bellasync.app/booking/
            <span className="text-warm-700 font-medium">{data?.slug}</span>
          </div>
        </Field>
        <Field label="Descripción corta" hint="Aparece en el portal de booking (máx 500)">
          <textarea
            value={form.description ?? ''}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            rows={3}
            maxLength={500}
            placeholder="Ej: Spa boutique especializado en cabello y manos. Atendemos con cita."
            className={cls(inputClass, 'resize-none')}
          />
        </Field>
      </Section>

      {/* Logo */}
      <Section title="Logo" icon={<ImageIcon size={15} strokeWidth={1.8} />}>
        <div className="flex gap-4 items-start">
          <div className="flex-1 min-w-0">
            <Field label="URL del logo" hint="Imagen pública (Imgur, Cloudinary, Instagram CDN…)">
              <input
                value={form.logoUrl ?? ''}
                onChange={(e) => setForm({ ...form, logoUrl: e.target.value })}
                placeholder="https://…"
                className={inputClass}
              />
            </Field>
          </div>
          <LogoPreview url={form.logoUrl} />
        </div>
      </Section>

      {/* Contacto */}
      <Section title="Contacto" icon={<Phone size={15} strokeWidth={1.8} />}>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Teléfono del salón">
            <input
              value={form.phone ?? ''}
              onChange={(e) => setForm({ ...form, phone: e.target.value })}
              placeholder="+57 320 555 1234"
              className={inputClass}
            />
          </Field>
          <Field label="Email de contacto">
            <input
              value={form.contactEmail ?? ''}
              onChange={(e) => setForm({ ...form, contactEmail: e.target.value })}
              placeholder="contacto@misalon.com"
              type="email"
              className={inputClass}
            />
          </Field>
        </div>
        <Field label="Dirección">
          <input
            value={form.address ?? ''}
            onChange={(e) => setForm({ ...form, address: e.target.value })}
            placeholder="Cra 25 #18-32, Neiva"
            className={inputClass}
          />
        </Field>
      </Section>

      {/* Redes */}
      <Section title="Redes sociales" icon={<AtSign size={15} strokeWidth={1.8} />}>
        <Field label="Instagram" hint="Sin @ (lo agregamos automáticamente)">
          <div className="flex items-center rounded-lg border border-warm-200 bg-white overflow-hidden focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
            <span className="pl-3 pr-1 text-warm-400 text-[13px]">@</span>
            <input
              value={form.instagramHandle ?? ''}
              onChange={(e) => setForm({ ...form, instagramHandle: e.target.value.replace(/^@/, '') })}
              placeholder="bella.spa.neiva"
              className="flex-1 pr-3 py-2.5 text-[13px] text-warm-800 outline-none"
            />
          </div>
        </Field>
      </Section>

      {/* Feedback */}
      {error && (
        <div className="mt-4 rounded-lg bg-terra-100/60 ring-1 ring-terra-300 px-3 py-2 text-[12.5px] text-terra-500">
          {error}
        </div>
      )}
      {savedRecently && !error && (
        <div className="mt-4 rounded-lg bg-brand-50 ring-1 ring-brand-200 px-3 py-2 text-[12.5px] text-brand-800">
          ✓ Cambios guardados.
        </div>
      )}

      <div className="mt-6 flex items-center justify-end gap-2">
        <button
          type="submit"
          disabled={mut.isPending}
          className={cls(
            'px-5 py-2.5 rounded-lg text-[13px] font-medium transition',
            mut.isPending
              ? 'bg-warm-300 text-white cursor-wait'
              : 'bg-brand-700 hover:bg-brand-800 text-white shadow-soft',
          )}
        >
          {mut.isPending ? 'Guardando…' : 'Guardar cambios'}
        </button>
      </div>
    </form>
  )
}

// ───────────────────────────────────────────────────────────────────────

function Section({
  title, icon, children,
}: {
  title: string
  icon: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <section className="mb-7 pb-6 border-b border-warm-150 last:border-0 last:pb-0 last:mb-0">
      <h2 className="font-medium text-[13.5px] text-warm-800 mb-3.5 flex items-center gap-2">
        <span className="text-warm-400">{icon}</span>
        {title}
      </h2>
      <div className="space-y-3.5">{children}</div>
    </section>
  )
}

function Field({
  label, hint, required, children,
}: {
  label: string
  hint?: string
  required?: boolean
  children: React.ReactNode
}) {
  return (
    <label className="block">
      <div className="flex items-baseline justify-between mb-1.5">
        <span className="text-[12px] font-medium text-warm-700">
          {label}
          {required && <span className="text-terra-500 ml-0.5">*</span>}
        </span>
        {hint && <span className="text-[11px] text-warm-400">{hint}</span>}
      </div>
      {children}
    </label>
  )
}

function LogoPreview({ url }: { url: string | null | undefined }) {
  const [errored, setErrored] = useState(false)
  // Reset error si la URL cambia
  useEffect(() => { setErrored(false) }, [url])

  if (!url) {
    return (
      <div className="w-20 h-20 rounded-xl border border-dashed border-warm-300 bg-warm-50 flex flex-col items-center justify-center text-warm-400 flex-shrink-0">
        <ImageIcon size={20} strokeWidth={1.5} />
        <span className="text-[10px] mt-1">Sin logo</span>
      </div>
    )
  }
  if (errored) {
    return (
      <div className="w-20 h-20 rounded-xl border border-terra-300 bg-terra-100/40 flex flex-col items-center justify-center text-terra-500 flex-shrink-0 text-center px-2">
        <span className="text-[10.5px] font-medium">URL inválida</span>
        <span className="text-[9.5px] mt-0.5">o sin acceso</span>
      </div>
    )
  }
  return (
    <img
      src={url}
      alt="Logo"
      onError={() => setErrored(true)}
      className="w-20 h-20 rounded-xl border border-warm-200 object-cover bg-white flex-shrink-0"
    />
  )
}

const inputClass = 'w-full px-3 py-2.5 rounded-lg bg-white border border-warm-200 text-[13px] text-warm-800 placeholder:text-warm-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none'

function emptyToNull(v: string | null | undefined): string | null {
  if (v === undefined || v === null) return null
  const t = v.trim()
  return t === '' ? null : t
}
