import { useState } from 'react'
import { Users } from 'lucide-react'
import type { CustomerResponse } from '@/api/customers'
import { NewAppointmentModal } from '@/features/appointments/components/NewAppointmentModal'
import { ClientList } from './components/ClientList'
import { ClientDetail } from './components/ClientDetail'
import { CustomerModal } from './components/CustomerModal'

/**
 * CRM de clientes — layout split-panel:
 *  - Izquierda (~40%): lista con búsqueda + tabs por tag.
 *  - Derecha (~60%): ficha completa del cliente seleccionado (4 tabs).
 *
 * Cuando no hay ninguno seleccionado, la derecha muestra un empty state
 * invitando a elegir uno o crear el primero.
 *
 * Modales:
 *  - CustomerModal: crear (creating) o editar (editing)
 *  - NewAppointmentModal: agendar cita para el cliente seleccionado
 *    (apptForCustomer guarda el cliente pre-seleccionado).
 */
export function CustomersPage() {
  const [selected, setSelected] = useState<CustomerResponse | null>(null)
  const [editing, setEditing] = useState<CustomerResponse | null>(null)
  const [creating, setCreating] = useState(false)
  const [apptForCustomer, setApptForCustomer] = useState<CustomerResponse | null>(null)

  const today = new Date().toISOString().slice(0, 10)

  return (
    <div className="flex h-full min-h-0 bg-warm-50">
      <ClientList
        selectedId={selected?.id ?? null}
        onSelect={setSelected}
        onNew={() => setCreating(true)}
      />

      {selected ? (
        <ClientDetail
          key={selected.id}
          fallback={selected}
          onEdit={() => setEditing(selected)}
          onNewAppointment={() => setApptForCustomer(selected)}
        />
      ) : (
        // Empty state: visible desde md (768px+) para que iPad y desktop
        // vean qué va a aparecer al elegir un cliente. En mobile (<md) el
        // detalle vive en página aparte (futuro Sprint B).
        <main className="hidden md:flex flex-1 min-w-0 flex-col items-center justify-center bg-warm-50 p-10 text-center">
          <div className="w-14 h-14 rounded-full bg-white border border-warm-200 flex items-center justify-center text-warm-400">
            <Users size={24} />
          </div>
          <div className="font-serif text-[26px] text-warm-700 mt-4">
            Selecciona una clienta
          </div>
          <div className="text-[13.5px] text-warm-500 mt-1 max-w-sm">
            Su ficha completa aparecerá aquí: stats, próxima cita, historial
            y ficha técnica.
          </div>
        </main>
      )}

      {creating && (
        <CustomerModal customer={null} onClose={() => setCreating(false)} />
      )}
      {editing && (
        <CustomerModal
          customer={editing}
          onClose={() => {
            const justEdited = editing
            setEditing(null)
            // Si archivamos el seleccionado, limpiar selección
            if (selected?.id === justEdited.id && !justEdited.isActive) {
              setSelected(null)
            }
          }}
        />
      )}
      {apptForCustomer && (
        <NewAppointmentModal
          defaultDate={today}
          defaultCustomer={apptForCustomer}
          onClose={() => setApptForCustomer(null)}
        />
      )}
    </div>
  )
}
