import { useState } from 'react'
import Layout from '../components/Layout'
import SeccionDocumentos from './admin/SeccionDocumentos'
import SeccionUsuarios   from './admin/SeccionUsuarios'
import SeccionAreas      from './admin/SeccionAreas'
import SeccionLogs       from './admin/SeccionLogs'

const SECCIONES = [
  { id: 'documentos', label: 'Documentos', icono: '📄' },
  { id: 'usuarios',   label: 'Usuarios',   icono: '👥' },
  { id: 'areas',      label: 'Áreas',      icono: '🏢' },
  { id: 'logs',       label: 'Auditoría',  icono: '📊' },
]

export default function DashboardAdmin() {
  const [seccion, setSeccion] = useState('documentos')

  const renderSeccion = () => {
    if (seccion === 'documentos') return <SeccionDocumentos />
    if (seccion === 'usuarios')   return <SeccionUsuarios />
    if (seccion === 'areas')      return <SeccionAreas />
    if (seccion === 'logs')       return <SeccionLogs />
    return null
  }

  return (
    <Layout
      titulo="QualityDoc"
      secciones={SECCIONES}
      seccionActiva={seccion}
      setSeccion={setSeccion}
    >
      {renderSeccion()}
    </Layout>
  )
}
