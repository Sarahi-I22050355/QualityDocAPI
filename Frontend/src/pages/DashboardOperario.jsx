import { useState } from 'react'
import Layout from '../components/Layout'
import api from '../api/axios'
import '../components/Seccion.css'

// El Operario solo puede:
// - Buscar documentos Aprobados de su área + área General
// - Descargar esos documentos
// No puede subir, aprobar, ni ver logs.

function SeccionDocumentosOperario() {
  const [busqueda, setBusqueda]     = useState('')
  const [resultados, setResultados] = useState([])
  const [buscando, setBuscando]     = useState(false)
  const [error, setError]           = useState('')
  const [buscadoYa, setBuscadoYa]   = useState(false)

  const handleBuscar = async (e) => {
    e.preventDefault()
    if (!busqueda.trim()) return
    setBuscando(true)
    setError('')
    setResultados([])
    setBuscadoYa(true)
    try {
      const r = await api.get(`/Documentos/buscar/${encodeURIComponent(busqueda)}`)
      setResultados(r.data.resultados || [])
    } catch (e) {
      setError(e.response?.data?.Mensaje || 'No se encontraron documentos aprobados con ese término.')
    } finally {
      setBuscando(false)
    }
  }

  const handleDescargar = async (idDoc, titulo) => {
    try {
      const r = await api.get(`/Documentos/descargar/${idDoc}`, { responseType: 'blob' })
      const url  = window.URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
      const link = document.createElement('a')
      link.href     = url
      link.download = `${titulo || 'documento'}.pdf`
      link.click()
      window.URL.revokeObjectURL(url)
    } catch (e) {
      alert(e.response?.data?.Mensaje || 'Error al descargar el documento.')
    }
  }

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Documentos aprobados</h2>
      </div>

      <div className="card" style={{ marginBottom: '1rem', background: '#f0fdf4', border: '1px solid #bbf7d0' }}>
        <p style={{ fontSize: '0.875rem', color: '#166534' }}>
          Aquí puedes buscar y descargar los documentos aprobados de tu área, así como documentos del área General disponibles para todos.
        </p>
      </div>

      <form onSubmit={handleBuscar}>
        <div className="filtros-row">
          <input
            type="text"
            placeholder="Buscar por título, descripción, etiqueta o autor..."
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            style={{ flex: 1, minWidth: '200px' }}
          />
          <button type="submit" className="btn-primario" disabled={buscando}>
            {buscando ? 'Buscando...' : 'Buscar'}
          </button>
        </div>
      </form>

      {error && <div className="alerta-error">{error}</div>}

      {buscadoYa && !buscando && (
        <div className="card">
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Título</th>
                  <th>Categoría</th>
                  <th>Área</th>
                  <th>Autor</th>
                  <th>Descargar</th>
                </tr>
              </thead>
              <tbody>
                {resultados.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="sin-datos">
                      No hay documentos aprobados con ese término de búsqueda.
                    </td>
                  </tr>
                ) : resultados.map((r, i) => {
                  const doc = r.documento ?? r
                  return (
                    <tr key={doc.sqlId ?? i}>
                      <td><strong>{doc.titulo}</strong></td>
                      <td>{doc.categoria}</td>
                      <td>
                        {doc.area}
                        {doc.area?.toLowerCase() === 'general' && (
                          <span className="badge badge-morado" style={{ marginLeft: '6px', fontSize: '0.7rem' }}>
                            General
                          </span>
                        )}
                      </td>
                      <td>{doc.autor}</td>
                      <td>
                        <button
                          className="btn-primario"
                          onClick={() => handleDescargar(doc.sqlId, doc.titulo)}
                        >
                          ⬇ Descargar
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {!buscadoYa && (
        <div className="card">
          <p className="sin-datos">
            Escribe una palabra clave para buscar documentos disponibles para ti.
          </p>
        </div>
      )}
    </div>
  )
}

// El dashboard del Operario solo tiene una sección
const SECCIONES = [
  { id: 'documentos', label: 'Documentos', icono: '📄' },
]

export default function DashboardOperario() {
  const [seccion, setSeccion] = useState('documentos')

  return (
    <Layout
      titulo="QualityDoc"
      secciones={SECCIONES}
      seccionActiva={seccion}
      setSeccion={setSeccion}
    >
      <SeccionDocumentosOperario />
    </Layout>
  )
}
