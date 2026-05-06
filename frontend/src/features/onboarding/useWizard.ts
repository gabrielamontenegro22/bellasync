import { useCallback, useEffect, useState } from 'react'
import type { WizardData } from './types'
import { SUGGESTED_SERVICES } from './data/suggestedServices'
import { HOURS_PRESETS } from './data/hoursPresets'
import { wizardStorage } from './storage'

/**
 * Crea el estado por defecto del wizard.
 * Coincide 1:1 con defaultState() de onboarding-shell.jsx del mockup.
 */
export function makeDefaultWizardData(): WizardData {
  const servicesData: Record<string, { price: number; dur: number }> = {}
  const servicesOn:   Record<string, boolean> = {}
  for (const s of SUGGESTED_SERVICES) {
    servicesData[s.id] = { price: s.price, dur: s.dur }
    servicesOn[s.id]   = s.defOn
  }

  return {
    ownerName: '',
    email: '',
    password: '',

    salonName: '',
    nit: '',
    address: '',
    city: '',
    phone: '',
    logoData: null,
    logoName: '',

    hoursPreset: 'classic',
    hours: HOURS_PRESETS.classic.days,

    servicesOn,
    servicesData,
    customServices: [],

    plan: 'pro',
  }
}

/**
 * Hook con el estado del wizard.
 * - Hidrata desde localStorage si hay draft
 * - Persiste cada cambio
 * - Helpers para avanzar/retroceder/setear campos
 */
export function useWizard() {
  const [step,        setStep]        = useState(1)
  const [maxReached,  setMaxReached]  = useState(1)
  const [done,        setDone]        = useState(false)
  const [data,        setData]        = useState<WizardData>(() => {
    const saved = wizardStorage.loadDraft()
    return saved ?? makeDefaultWizardData()
  })

  // Guardar draft en cada cambio
  useEffect(() => {
    wizardStorage.saveDraft(data)
  }, [data])

  // Scroll al top al cambiar de paso
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [step])

  const set = useCallback((patch: Partial<WizardData>) => {
    setData((d) => ({ ...d, ...patch }))
  }, [])

  const next = useCallback(() => {
    setStep((s) => {
      const n = s + 1
      setMaxReached((m) => Math.max(m, n))
      return n
    })
  }, [])

  const back = useCallback(() => {
    setStep((s) => Math.max(1, s - 1))
  }, [])

  const goTo = useCallback((n: number) => {
    setStep((s) => (n <= maxReached ? n : s))
  }, [maxReached])

  const finish = useCallback(() => {
    setDone(true)
    wizardStorage.clearDraft()
  }, [])

  const reset = useCallback(() => {
    setData(makeDefaultWizardData())
    setStep(1)
    setMaxReached(1)
    setDone(false)
    wizardStorage.clearDraft()
  }, [])

  return { step, maxReached, done, data, set, next, back, goTo, finish, reset }
}
