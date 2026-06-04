import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Building2, ImageIcon, AtSign, Phone, ExternalLink, Box,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import {
  getTenantInfo,
  updateTenantInfo,
  type UpdateTenantInfoRequest,
} from '@/api/admin'
import {
  SettingsHeader, SettingsBlock, SettingsField, SaveBar, inputCls,
} from './_primitives'

/**
 * `/configuracion/general` — info pública y de contacto del salón.
 * Diseño basado en config-servicios.jsx (InfoGeneralView). Lógica:
 * GET/PUT /api/Admin/tenant-info.
 *
 * Nota: el logo se guarda como URL (no upload de archivo) porque no
 * tenemos storage de imágenes todavía. Visualmente queda igual al
 * mockup con la diferencia de que en vez de "Examinar archivo" hay
 * un input de URL.
 */
export function TenantInfoPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: ['tenantInfo'],
    queryFn: getTenantInfo,
  })

  // Form local hidratado cuando llega la query. Lo manejamos como
  // dict de strings (vacío en lugar de null) para que los inputs no
  // tengan que lidiar con null.
  type FormShape = {
    name: string
    address: string
    phone: string
    contactEmail: string
    logoUrl: string
    instagramHandle: string
    description: string
  }
  const initial: FormShape = useMemo(
    () => ({
      name: data?.name ?? '',
      address: data?.address ?? '',
      phone: data?.phone ?? '',
      contactEmail: data?.contactEmail ?? '',
      logoUrl: data?.logoUrl ?? '',
      instagramHandle: data?.instagramHandle ?? '',
      description: data?.description ?? '',
    }),
    [data],
  )

  const [form, setForm] = useState<FormShape>(initial)
  useEffect(() => { setForm(initial) }, [initial])

  const [saved, setSaved] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const mut = useMutation({
    mutationFn: (req: UpdateTenantInfoRequest) => updateTenantInfo(req),
    onSuccess: (r) => {
      qc.setQueryData(['tenantInfo'], r)
      setSubmitError(null)
      setSaved(true)
    },
    onError: (e) => setSubmitError(extractApiError(e, 'No se pudo guardar.')),
  })

  // SaveBar verde se auto-oculta a los 3s.
  useEffect(() => {
    if (!saved) return
    const t = setTimeout(() => setSaved(false), 3000)
    return () => clearTimeout(t)
  }, [saved])

  const isDirty = JSON.stringify(form) !== JSON.stringify(initial)

  const setField = <K extends keyof FormShape>(key: K, value: FormShape[K]) => {
    setForm(f => ({ ...f, [key]: value }))
    setSaved(false)
    setSubmitError(null)
  }

  const handleSave = () => {
    if (!form.name.trim()) {
      setSubmitError('El nombre del salón es obligatorio.')
      return
    }
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

  const handleDiscard = () => {
    setForm(initial)
    setSubmitError(null)
    setSaved(false)
  }

  if (isLoading) {
    return (
      <div className="px-6 lg:px-10 py-8 text-[13px] text-warm-500">Cargando…</div>
    )
  }

  return (
    <div className="flex flex-col min-h-full">
      <div className="flex-1 px-6 lg:px-10 py-8 max-w-3xl">
        <SettingsHeader
          eyebrow="Ajustes del salón"
          title="Información general"
          desc="Datos públicos y de contacto. Se usan en el portal de reservas, las notificaciones por WhatsApp y donde sea que tu salón aparezca con su marca."
        />

        {/* IDENTIDAD */}
        <SettingsBlock icon={<Building2 size={16} />} title="Identidad">
          <SettingsField label="Nombre del salón" required>
            <input
              value={form.name}
              onChange={(e) => setField('name', e.target.value)}
              className={inputCls}
              placeholder="Bella Aurora"
            />
          </SettingsField>

          <SettingsField label="URL del booking" hint="Lo cambian desde soporte por ahora">
            <div className="flex items-center rounded-lg border border-warm-200 bg-warm-50 overflow-hidden">
              <span className="pl-3.5 pr-1 text-[13.5px] text-warm-400 flex items-center gap-1.5 whitespace-nowrap">
                <ExternalLink size={13} /> bellasync.app/booking/
              </span>
              <div className="flex-1 py-2.5 pr-3.5 bg-white text-[14px] text-warm-800 border-l border-warm-200 pl-3 select-text">
                {data?.slug}
              </div>
            </div>
          </SettingsField>

          <SettingsField label="Descripción corta" hint="Aparece en el portal de booking (máx 500)">
            <textarea
              value={form.description}
              onChange={(e) => setField('description', e.target.value.slice(0, 500))}
              rows={3}
              className={cls(inputCls, 'resize-none')}
              placeholder="Ej: Spa boutique especializado en cabello y manos. Atendemos con cita."
            />
          </SettingsField>
        </SettingsBlock>

        {/* LOGO */}
        <SettingsBlock icon={<ImageIcon size={16} />} title="Logo">
          <SettingsField
            label="URL del logo"
            hint="Pegá un link público (Imgur, Cloudinary, Instagram CDN…)"
          >
            <div className="flex items-center gap-4">
              <LogoPreview url={form.logoUrl} />
              <input
                value={form.logoUrl}
                onChange={(e) => setField('logoUrl', e.target.value)}
                className={inputCls}
                placeholder="https://…"
              />
            </div>
            <p className="text-[11.5px] text-warm-500 mt-2">
              Cuando hagamos upload de archivos lo vas a poder subir desde acá. Por
              ahora solo URL pública.
            </p>
          </SettingsField>
        </SettingsBlock>

        {/* CONTACTO */}
        <SettingsBlock icon={<Phone size={16} />} title="Contacto">
          <div className="grid sm:grid-cols-2 gap-4">
            <SettingsField label="Teléfono del salón">
              <input
                value={form.phone}
                onChange={(e) => setField('phone', e.target.value)}
                className={inputCls}
                placeholder="+57 320 555 1234"
              />
            </SettingsField>
            <SettingsField label="Email de contacto">
              <input
                value={form.contactEmail}
                onChange={(e) => setField('contactEmail', e.target.value)}
                className={inputCls}
                placeholder="contacto@misalon.com"
                type="email"
              />
            </SettingsField>
          </div>
          <SettingsField label="Dirección">
            <input
              value={form.address}
              onChange={(e) => setField('address', e.target.value)}
              className={inputCls}
              placeholder="Cra 25 #18-32, Neiva"
            />
          </SettingsField>
        </SettingsBlock>

        {/* REDES */}
        <SettingsBlock icon={<AtSign size={16} />} title="Redes sociales" last>
          <SettingsField label="Instagram" hint="Sin @ (lo agregamos automáticamente)">
            <div className="flex items-center rounded-lg border border-warm-200 bg-white overflow-hidden focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
              <span className="pl-3.5 pr-1 text-warm-400 text-[14px]">@</span>
              <input
                value={form.instagramHandle}
                onChange={(e) => setField('instagramHandle', e.target.value.replace(/^@/, ''))}
                placeholder="bella.spa.neiva"
                className="flex-1 py-2.5 pr-3.5 text-[14px] text-warm-800 outline-none bg-white"
              />
            </div>
          </SettingsField>
        </SettingsBlock>
      </div>

      <SaveBar
        show={isDirty}
        saved={saved}
        saving={mut.isPending}
        error={submitError}
        onSave={handleSave}
        onDiscard={handleDiscard}
      />
    </div>
  )
}

// ───────────────────────────────────────────────────────────────────────

function LogoPreview({ url }: { url: string }) {
  const [errored, setErrored] = useState(false)
  useEffect(() => { setErrored(false) }, [url])

  if (!url) {
    return (
      <div className="w-16 h-16 rounded-lg flex items-center justify-center bg-warm-100 text-warm-400 flex-shrink-0">
        <Box size={20} />
      </div>
    )
  }
  if (errored) {
    return (
      <div className="w-16 h-16 rounded-lg flex flex-col items-center justify-center bg-terra-100/40 text-terra-500 flex-shrink-0 text-center px-1">
        <span className="text-[9.5px] font-medium leading-tight">URL inválida</span>
      </div>
    )
  }
  return (
    <img
      src={url}
      alt="Logo"
      onError={() => setErrored(true)}
      className="w-16 h-16 rounded-lg border border-warm-150 object-cover bg-white flex-shrink-0"
    />
  )
}

function emptyToNull(v: string | null | undefined): string | null {
  if (v === undefined || v === null) return null
  const t = v.trim()
  return t === '' ? null : t
}
