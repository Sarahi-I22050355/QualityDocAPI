import { useState } from 'react'
import Layout from '../components/Layout'
import SeccionDocumentosSupervisor from './supervisor/SeccionDocumentosSupervisor'

// Versiones ya están integradas dentro de SeccionDocumentosSupervisor
// como un modal que se abre desde los resultados de búsqueda.
// No se necesita sección separada.
const SECCIONES = [
  { id: 'documentos', label: 'Documentos', icono: '📄' },
]

export default function DashboardSupervisor() {
  const [seccion, setSeccion] = useState('documentos')

  return (
    <Layout
      titulo="QualityDoc"
      secciones={SECCIONES}
      seccionActiva={seccion}
      setSeccion={setSeccion}
    >
      <SeccionDocumentosSupervisor />
    </Layout>
  )
}
