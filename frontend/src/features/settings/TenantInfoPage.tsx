import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Building2, ImageIcon, AtSign, Phone, ExternalLink, Box, Upload, Loader2,
} from 'lucide-react'
import { cls } from '@/lib/cls'
import { extractApiError } from '@/lib/extractApiError'
import { absoluteUrl } from '@/lib/absoluteUrl'
import {
  getTenantInfo,
  updateTenantInfo,
  uploadTenantLogo,
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
          <LogoUploader
            currentUrl={form.logoUrl}
            onUploaded={(newUrl) => {
              // Refrescamos el cache para que el preview muestre el nuevo
              // logo, y reset del form para que NO quede dirty (el backend
              // ya persistió, no hay que esperar al SaveBar).
              setField('logoUrl', newUrl)
              qc.invalidateQueries({ queryKey: ['tenantInfo'] })
            }}
          />
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
      <div className="w-20 h-20 rounded-xl flex items-center justify-center bg-warm-100 text-warm-400 flex-shrink-0">
        <Box size={24} />
      </div>
    )
  }
  if (errored) {
    return (
      <div className="w-20 h-20 rounded-xl flex flex-col items-center justify-center bg-terra-100/40 text-terra-500 flex-shrink-0 text-center px-1">
        <span className="text-[9.5px] font-medium leading-tight">URL inválida</span>
      </div>
    )
  }
  return (
    <img
      src={absoluteUrl(url)}
      alt="Logo"
      onError={() => setErrored(true)}
      className="w-20 h-20 rounded-xl border border-warm-150 object-cover bg-white flex-shrink-0"
    />
  )
}

/**
 * Bloque de upload de logo con preview + input file oculto disparado por
 * botón. Sube apenas el usuario elige el archivo (no espera al SaveBar)
 * porque el upload es una mutación independiente del resto del form.
 */
function LogoUploader({
  currentUrl,
  onUploaded,
}: {
  currentUrl: string
  onUploaded: (newUrl: string) => void
}) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const [err, setErr] = useState<string | null>(null)

  const uploadMut = useMutation({
    mutationFn: (file: File) => uploadTenantLogo(file),
    onSuccess: (data) => {
      setErr(null)
      onUploaded(data.logoUrl)
    },
    onError: (e) => setErr(extractApiError(e, 'No se pudo subir el logo.')),
  })

  const handlePick = () => fileInputRef.current?.click()

  const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''  // reset para que el mismo archivo pueda re-seleccionarse
    if (!file) return
    if (file.size > 5 * 1024 * 1024) {
      setErr('El archivo supera 5 MB.')
      return
    }
    uploadMut.mutate(file)
  }

  return (
    <div className="flex items-start gap-5">
      <LogoPreview url={currentUrl} />
      <div className="flex-1 min-w-0">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp,image/heic,image/heif"
          onChange={handleFile}
          className="hidden"
        />
        <button
          type="button"
          onClick={handlePick}
          disabled={uploadMut.isPending}
          className={cls(
            'inline-flex items-center gap-2 px-3.5 py-2 rounded-lg',
            'border border-warm-200 bg-white text-[13px] text-warm-700 font-medium',
            'hover:border-warm-300 hover:text-warm-800 transition',
            'disabled:opacity-60 disabled:cursor-not-allowed',
          )}
        >
          {uploadMut.isPending
            ? <Loader2 size={14} className="animate-spin" />
            : <Upload size={14} />}
          {uploadMut.isPending ? 'Subiendo…' : currentUrl ? 'Cambiar logo' : 'Subir logo'}
        </button>
        <p className="text-[11.5px] text-warm-500 mt-2 leading-relaxed">
          JPG, PNG o WebP. Máximo 5 MB. Se sube de inmediato — no necesitás guardar.
        </p>
        {err && (
          <p className="text-[12px] text-terra-700 mt-2">{err}</p>
        )}
      </div>
    </div>
  )
}

function emptyToNull(v: string | null | undefined): string | null {
  if (v === undefined || v === null) return null
  const t = v.trim()
  return t === '' ? null : t
}
