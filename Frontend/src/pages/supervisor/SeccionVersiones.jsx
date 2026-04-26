import { useState } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'

// El Supervisor puede:
// - Consultar el historial de versiones de cualquier doc de su área
// - Subir una nueva versión (el doc regresa a Borrador automáticamente)
// No puede hacer esto con docs del área General.

export default function SeccionVersiones() {
  const [idDocBuscar, setIdDocBuscar]   = useState('')
  const [historial, setHistorial]       = useState(null)
  const [cargando, setCargando]         = useState(false)
  const [error, setError]               = useState('')

  // Nueva versión
  const [modalNuevaVer, setModalNuevaVer] = useState(false)
  const [formVer, setFormVer]             = useState({ ComentarioCambio: '', ContenidoTexto: '' })
  const [archivoVer, setArchivoVer]       = useState(null)
  const [subiendoVer, setSubiendoVer]     = useState(false)
  const [errorVer, setErrorVer]           = useState('')
  const [okVer, setOkVer]                 = useState('')

  const buscarVersiones = async (e) => {
    e.preventDefault()
    if (!idDocBuscar.trim()) return
    setCargando(true)
    setError('')
    setHistorial(null)
    try {
      const r = await api.get(`/Documentos/${idDocBuscar}/versiones`)
      setHistorial(r.data)
    } catch (e) {
      setError(e.response?.data?.Mensaje || 'No se pudo obtener el historial. Verifica que el ID sea de un documento de tu área.')
    } finally {
      setCargando(false)
    }
  }

  const handleSubirVersion = async (e) => {
    e.preventDefault()
    setSubiendoVer(true)
    setErrorVer('')
    try {
      const fd = new FormData()
      fd.append('ComentarioCambio', formVer.ComentarioCambio)
      if (archivoVer)                   fd.append('Archivo', archivoVer)
      else if (formVer.ContenidoTexto)  fd.append('ContenidoTexto', formVer.ContenidoTexto)

      await api.post(`/Documentos/${idDocBuscar}/nueva-version`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      setOkVer('Nueva versión subida correctamente. El documento regresa a Borrador para revisión.')
      setModalNuevaVer(false)
      // Recargar historial
      const r = await api.get(`/Documentos/${idDocBuscar}/versiones`)
      setHistorial(r.data)
      setTimeout(() => setOkVer(''), 4000)
    } catch (e) {
      setErrorVer(e.response?.data?.Mensaje || e.response?.data?.Error || 'Error al subir la versión.')
    } finally {
      setSubiendoVer(false)
    }
  }

  const formatFecha = (fecha) => {
    if (!fecha) return '—'
    return new Date(fecha).toLocaleString('es-MX', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    })
  }

  const formatBytes = (bytes) => {
    if (!bytes) return '—'
    if (bytes < 1024)        return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Versiones de documentos</h2>
      </div>

      <div className="card" style={{ marginBottom: '1rem' }}>
        <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '1rem' }}>
          Ingresa el ID del documento para ver su historial de versiones. El ID lo obtienes desde el buscador de documentos.
        </p>
        <form onSubmit={buscarVersiones}>
          <div className="filtros-row">
            <input
              type="number"
              placeholder="ID del documento"
              value={idDocBuscar}
              onChange={(e) => setIdDocBuscar(e.target.value)}
              style={{ width: '160px' }}
            />
            <button type="submit" className="btn-primario" disabled={cargando}>
              {cargando ? 'Buscando...' : 'Ver historial'}
            </button>
          </div>
        </form>
      </div>

      {error && <div className="alerta-error">{error}</div>}
      {okVer  && <div className="alerta-ok">{okVer}</div>}

      {historial && (
        <>
          <div className="card">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '1rem' }}>
              <div>
                <strong>{historial.tituloDocumento}</strong>
                <div style={{ fontSize: '0.875rem', color: '#6b7280', marginTop: '2px' }}>
                  Versión actual: <strong>v{historial.versionActual}</strong>
                  {' · '}
                  Estado: {historial.estadoActual === 'Aprobado'
                    ? <span className="badge badge-verde">Aprobado</span>
                    : historial.estadoActual === 'Borrador'
                    ? <span className="badge badge-naranja">Borrador</span>
                    : <span className="badge badge-gris">{historial.estadoActual}</span>}
                </div>
              </div>
              <button
                className="btn-primario"
                onClick={() => {
                  setFormVer({ ComentarioCambio: '', ContenidoTexto: '' })
                  setArchivoVer(null)
                  setErrorVer('')
                  setModalNuevaVer(true)
                }}
              >
                + Subir nueva versión
              </button>
            </div>

            <div className="tabla-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Versión</th>
                    <th>Subido por</th>
                    <th>Fecha</th>
                    <th>Tamaño</th>
                    <th>Comentario de cambio</th>
                  </tr>
                </thead>
                <tbody>
                  {historial.versiones?.length === 0 ? (
                    <tr><td colSpan={5} className="sin-datos">Sin versiones registradas.</td></tr>
                  ) : historial.versiones?.map((v) => (
                    <tr key={v.idVersion}>
                      <td>
                        <span className="badge badge-morado">v{v.numeroVersion}</span>
                        {v.numeroVersion === historial.versionActual && (
                          <span style={{ marginLeft: '6px', fontSize: '0.75rem', color: '#6b7280' }}>actual</span>
                        )}
                      </td>
                      <td>{v.subidoPor}</td>
                      <td style={{ whiteSpace: 'nowrap' }}>{formatFecha(v.fechaVersion)}</td>
                      <td>{formatBytes(v.tamanoBytes)}</td>
                      <td style={{ fontSize: '0.8125rem', color: '#6b7280' }}>{v.comentarioCambio || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {/* ── Modal nueva versión ─────────────────────────────────────── */}
      {modalNuevaVer && (
        <div className="modal-fondo" onClick={() => setModalNuevaVer(false)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Subir nueva versión — Doc #{idDocBuscar}</h3>
            <p style={{ fontSize: '0.875rem', color: '#854F0B', background: '#fef3c7', padding: '8px 12px', borderRadius: '8px', marginBottom: '1rem' }}>
              El documento regresará a estado <strong>Borrador</strong> y necesitará una nueva aprobación.
            </p>
            <form onSubmit={handleSubirVersion}>
              <div className="form-grid una-col">
                <div className="campo-form">
                  <label>¿Qué cambió en esta versión? *</label>
                  <textarea
                    required
                    value={formVer.ComentarioCambio}
                    onChange={(e) => setFormVer({ ...formVer, ComentarioCambio: e.target.value })}
                    placeholder="Ej: Se actualizó la sección 3.2 por cambio en el procedimiento de proveedor X"
                  />
                </div>
                <div className="campo-form">
                  <label>Nuevo archivo PDF</label>
                  <input type="file" accept=".pdf"
                    onChange={(e) => setArchivoVer(e.target.files[0])} />
                </div>
                {!archivoVer && (
                  <div className="campo-form">
                    <label>O escribe el nuevo contenido</label>
                    <textarea
                      value={formVer.ContenidoTexto}
                      onChange={(e) => setFormVer({ ...formVer, ContenidoTexto: e.target.value })}
                      placeholder="Nuevo contenido del documento..."
                    />
                  </div>
                )}
              </div>

              {errorVer && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorVer}</div>}

              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalNuevaVer(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn-primario" disabled={subiendoVer}>
                  {subiendoVer ? 'Subiendo...' : 'Subir versión'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
