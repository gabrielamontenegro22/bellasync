import { useState, type ChangeEvent } from 'react'
import { Upload } from 'lucide-react'
import type { WizardData } from '../types'
import { WizardField, WizardInput, WizardSelect } from '../components/WizardField'
import { NavButtons } from '../components/NavButtons'
import { COLOMBIAN_CITIES } from '../data/colombianCities'
import { step2Schema } from '../schemas'

interface Step2Props {
  data: WizardData
  set: (patch: Partial<WizardData>) => void
  onNext: () => void
  onBack: () => void
}

/** Paso 2 — datos comerciales del salón (NIT, dirección, ciudad, teléfono, logo). */
export function Step2SalonInfo({ data, set, onNext, onBack }: Step2Props) {
  const [touched, setTouched] = useState(false)

  const result = step2Schema.safeParse({
    salonName: data.salonName,
    nit: data.nit,
    address: data.address,
    city: data.city,
    phone: data.phone,
    logoData: data.logoData,
    logoName: data.logoName,
  })
  const errors: Record<string, string> = {}
  if (!result.success && touched) {
    for (const issue of result.error.issues) {
      const key = issue.path[0]?.toString()
      if (key && !errors[key]) errors[key] = issue.message
    }
  }
  const valid = result.success

  const handleNext = () => {
    setTouched(true)
    if (valid) onNext()
  }

  const onLogoFile = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => {
      set({ logoData: reader.result as string, logoName: file.name })
    }
    reader.readAsDataURL(file)
  }

  return (
    <div className="space-y-5">
      <WizardField label="Nombre comercial del salón" error={errors.salonName}>
        <WizardInput
          value={data.salonName}
          onChange={(e) => set({ salonName: e.target.value })}
          placeholder="Bella Aurora Studio"
          autoFocus
        />
      </WizardField>

      <div className="grid sm:grid-cols-2 gap-4">
        <WizardField label="NIT" error={errors.nit} hint="Sin dígito de verificación">
          <WizardInput
            value={data.nit}
            onChange={(e) => set({ nit: e.target.value })}
            placeholder="901234567"
            inputMode="numeric"
          />
        </WizardField>

        <WizardField label="Teléfono / WhatsApp" error={errors.phone}>
          <WizardInput
            value={data.phone}
            onChange={(e) => set({ phone: e.target.value })}
            placeholder="+57 300 123 4567"
            inputMode="tel"
          />
        </WizardField>
      </div>

      <WizardField label="Dirección" error={errors.address}>
        <WizardInput
          value={data.address}
          onChange={(e) => set({ address: e.target.value })}
          placeholder="Calle 85 #15-30, Local 102"
        />
      </WizardField>

      <WizardField label="Ciudad" error={errors.city}>
        <WizardSelect value={data.city} onChange={(e) => set({ city: e.target.value })}>
          <option value="">Selecciona…</option>
          {COLOMBIAN_CITIES.map((c) => (
            <option key={c} value={c}>{c}</option>
          ))}
        </WizardSelect>
      </WizardField>

      <WizardField label="Logo del salón" optional hint="PNG o JPG, máx. 2 MB">
        <label className="flex items-center gap-4 px-4 py-3 rounded-lg border border-dashed border-warm-300 bg-warm-50/40 hover:bg-warm-50 cursor-pointer transition">
          {data.logoData ? (
            <img
              src={data.logoData}
              alt=""
              className="w-14 h-14 rounded-lg object-cover border border-warm-150"
            />
          ) : (
            <div className="w-14 h-14 rounded-lg bg-warm-100 flex items-center justify-center text-warm-500">
              <Upload size={20} />
            </div>
          )}
          <div className="flex-1 min-w-0">
            <div className="text-[13px] text-warm-800 font-medium truncate">
              {data.logoName || 'Subir imagen'}
            </div>
            <div className="text-[11.5px] text-warm-500">
              {data.logoData ? 'Click para cambiar' : 'Arrastra o haz click para seleccionar'}
            </div>
          </div>
          <input type="file" accept="image/*" className="hidden" onChange={onLogoFile} />
        </label>
      </WizardField>

      <NavButtons onBack={onBack} onNext={handleNext} valid={valid || !touched} />
    </div>
  )
}
