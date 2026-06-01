import { useState } from 'react'
import Layout from '../../components/Layout'
import SeccionEmpresas from './SeccionEmpresas'

const SECCIONES = [
  { id: 'empresas', label: 'Empresas', icono: '🏢' },
]

export default function DashboardSuperAdmin() {
  const [seccion, setSeccion] = useState('empresas')

  return (
    <Layout
      titulo="QualityDoc — SuperAdmin"
      secciones={SECCIONES}
      seccionActiva={seccion}
      setSeccion={setSeccion}
    >
      <SeccionEmpresas />
    </Layout>
  )
}