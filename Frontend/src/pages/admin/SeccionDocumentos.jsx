import { useState, useEffect } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'
import EditorTexto from '../../components/EditorTexto'
import { useAuth } from '../../context/AuthContext'
import { useCategorias } from '../../hooks/useCategorias'
import { swalError, swalInfo } from '../../utils/swal'

// ── Barra de progreso de firmas ───────────────────────────────────
function BarraFirmas({ requeridas, obtenidas }) {
  if (!requeridas || requeridas === 0) return null
  const pct = Math.round((obtenidas / requeridas) * 100)
  return (
    <div style={{ marginTop: '4px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.72rem', color: '#6b7280', marginBottom: '3px' }}>
        <span>Firmas</span>
        <span style={{ fontWeight: 600, color: obtenidas === requeridas ? '#4ade80' : '#fbbf24' }}>
          {obtenidas} / {requeridas}
        </span>
      </div>
      <div style={{ height: '4px', background: '#2a3347', borderRadius: '99px', overflow: 'hidden' }}>
        <div style={{ height: '4px', width: `${pct}%`, background: obtenidas === requeridas ? '#4ade80' : '#fbbf24', borderRadius: '99px', transition: 'width 0.3s' }} />
      </div>
    </div>
  )
}

export default function SeccionDocumentos() {
  const { usuario } = useAuth()

  const [busqueda, setBusqueda]     = useState('')
  const [resultados, setResultados] = useState([])
  const [buscando, setBuscando]     = useState(false)
  const [errorBusq, setErrorBusq]   = useState('')
  const [buscadoYa, setBuscadoYa]   = useState(false)
  // versiones por documento: { [sqlId]: { cargando, data } }
  const [versionesMap, setVersionesMap] = useState({})

  const [modalSubir, setModalSubir] = useState(false)
  const [areas, setAreas]           = useState([])
  const [formDoc, setFormDoc]       = useState({ Titulo: '', IdCategoria: 1, IdArea: '', ContenidoTexto: '' })
  const [archivo, setArchivo]       = useState(null)
  const [etiquetasInput, setEtiquetasInput] = useState('')
  const [subiendoDoc, setSubiendoDoc] = useState(false)
  const [errorSubir, setErrorSubir] = useState('')
  const [okSubir, setOkSubir]       = useState('')

  const [modalFlujo, setModalFlujo] = useState(false)
  const [docSel, setDocSel]         = useState(null)
  const [flujoData, setFlujoData]   = useState(null)
  const [cargFlujo, setCargFlujo]   = useState(false)
  const [errorFlujo, setErrorFlujo] = useState('')
  const [okFlujo, setOkFlujo]       = useState('')
  const [modalRes, setModalRes]     = useState(false)
  const [idFlujoRes, setIdFlujoRes] = useState(null)
  const [formRes, setFormRes]       = useState({ Decision: 'Aprobado', Comentarios: '' })
  const [resolviendoF, setResF]     = useState(false)

  // Nueva versión — modal inline
  const [modalNuevaVer, setModalNV]   = useState(false)
  const [docSelVer, setDocSelVer]     = useState(null)
  const [formVer, setFormVer]         = useState({ ComentarioCambio: '', ContenidoTexto: '' })
  const [archivoVer, setArchivoVer]   = useState(null)
  const [subiendoVer, setSubVer]      = useState(false)
  const [errorNV, setErrorNV]         = useState('')
  const [okNV, setOkNV]               = useState('')

  const categorias = useCategorias()

  // ── Buscar + cargar versiones de cada resultado ───────────────
  const handleBuscar = async (e) => {
    e.preventDefault()
    if (!busqueda.trim()) return
    setBuscando(true)
    setErrorBusq('')
    setResultados([])
    setVersionesMap({})
    setBuscadoYa(true)
    try {
      const r = await api.get(`/Documentos/buscar/${encodeURIComponent(busqueda)}`)
      const docs = r.data.resultados || []
      setResultados(docs)
      // Cargar versiones de cada documento en paralelo
      docs.forEach(async (item) => {
        const idDoc = item.documento?.sqlId ?? item.sqlId
        if (!idDoc) return
        setVersionesMap(prev => ({ ...prev, [idDoc]: { cargando: true, data: null } }))
        try {
          const vr = await api.get(`/Documentos/${idDoc}/versiones`)
          setVersionesMap(prev => ({ ...prev, [idDoc]: { cargando: false, data: vr.data } }))
        } catch {
          setVersionesMap(prev => ({ ...prev, [idDoc]: { cargando: false, data: null } }))
        }
      })
    } catch (e) {
      setErrorBusq(e.response?.data?.Mensaje || 'No se encontraron resultados.')
    } finally {
      setBuscando(false)
    }
  }

  const refrescarSilencioso = async () => {
    if (!busqueda.trim()) return
    try {
      const r = await api.get(`/Documentos/buscar/${encodeURIComponent(busqueda)}`)
      const docs = r.data.resultados || []
      setResultados(docs)
      docs.forEach(async (item) => {
        const idDoc = item.documento?.sqlId ?? item.sqlId
        if (!idDoc) return
        try {
          const vr = await api.get(`/Documentos/${idDoc}/versiones`)
          setVersionesMap(prev => ({ ...prev, [idDoc]: { cargando: false, data: vr.data } }))
        } catch {
          // ignore
        }
      })
    } catch {
      // ignore
    }
  }

  useEffect(() => {
    if (!buscadoYa) return
    const interval = setInterval(refrescarSilencioso, 60000)
    return () => clearInterval(interval)
  }, [buscadoYa, busqueda])

  const handleDescargar = async (idDoc, titulo, version) => {
    try {
      const url = version
        ? `/Documentos/${idDoc}/versiones/${version}/descargar`
        : `/Documentos/descargar/${idDoc}`
      const r = await api.get(url, { responseType: 'blob' })
      const blobUrl = window.URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
      const link    = document.createElement('a')
      link.href     = blobUrl
      link.download = version ? `v${version}_${titulo || 'documento'}.pdf` : `${titulo || 'documento'}.pdf`
      link.click()
      window.URL.revokeObjectURL(blobUrl)
    } catch {
      swalError('Error al descargar el documento.')
    }
  }

  const abrirModalSubir = async () => {
    try {
      const r = await api.get('/Areas')
      setAreas(r.data.areas || [])
    } catch { setAreas([]) }
    setFormDoc({ Titulo: '', IdCategoria: 1, IdArea: '', ContenidoTexto: '' })
    setArchivo(null)
    setEtiquetasInput('')
    setErrorSubir('')
    setOkSubir('')
    setModalSubir(true)
  }

  const handleSubir = async (e) => {
    e.preventDefault()
    setSubiendoDoc(true)
    setErrorSubir('')
    const tieneArchivo   = !!archivo
    const tieneContenido = formDoc.ContenidoTexto && formDoc.ContenidoTexto.replace(/<[^>]*>/g, '').trim() !== ''
    if (!tieneArchivo && !tieneContenido) {
      setErrorSubir('Debes adjuntar un archivo PDF o escribir el contenido del documento.')
      setSubiendoDoc(false)
      return
    }
    try {
      const fd = new FormData()
      fd.append('Titulo',      formDoc.Titulo)
      fd.append('IdCategoria', formDoc.IdCategoria)
      if (formDoc.IdArea)              fd.append('IdArea',         formDoc.IdArea)
      if (archivo)                     fd.append('Archivo',        archivo)
      else if (formDoc.ContenidoTexto) fd.append('ContenidoTexto', formDoc.ContenidoTexto)
      const etiquetas = etiquetasInput.split(',').map(e => e.trim()).filter(Boolean)
      etiquetas.forEach(tag => fd.append('Etiquetas', tag))
      await api.post('/Documentos', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
      setOkSubir('Documento subido correctamente.')
      setTimeout(() => { setModalSubir(false); setOkSubir('') }, 2000)
    } catch (e) {
      setErrorSubir(e.response?.data?.Mensaje || e.response?.data?.Error || 'Error al subir.')
    } finally {
      setSubiendoDoc(false)
    }
  }

  const handleSolicitar = async (idDoc) => {
    try {
      const res = await api.put(`/Documentos/solicitar-aprobacion/${idDoc}`)
      swalInfo(res.data?.Mensaje || 'Solicitud de revisión creada correctamente.')
    } catch (e) {
      swalError(e.response?.data?.Mensaje || 'Error al solicitar aprobación.')
    }
  }

  const verFlujo = async (r) => {
    const idDoc = r.documento?.sqlId ?? r.sqlId
    setDocSel(r)
    setFlujoData(null)
    setErrorFlujo('')
    setOkFlujo('')
    setModalFlujo(true)
    setCargFlujo(true)
    try {
      const res = await api.get(`/Documentos/${idDoc}/flujo`)
      setFlujoData(res.data)
    } catch {
      setErrorFlujo('Error al cargar el flujo.')
    } finally {
      setCargFlujo(false)
    }
  }

  const abrirResolver = (idFlujo) => {
    setIdFlujoRes(idFlujo)
    setFormRes({ Decision: 'Aprobado', Comentarios: '' })
    setModalRes(true)
  }

  const handleResolver = async (e) => {
    e.preventDefault()
    setResF(true)
    setErrorFlujo('')
    try {
      const res = await api.put(`/Documentos/resolver-aprobacion/${idFlujoRes}`, formRes)
      setOkFlujo(res.data?.Mensaje || 'Decisión registrada correctamente.')
      setModalRes(false)
      const idDoc = docSel?.documento?.sqlId ?? docSel?.sqlId
      const r2 = await api.get(`/Documentos/${idDoc}/flujo`)
      setFlujoData(r2.data)
    } catch (e) {
      setErrorFlujo(e.response?.data?.Mensaje || 'Error al resolver.')
    } finally {
      setResF(false)
    }
  }

  const abrirNuevaVersion = (r) => {
    const idDoc  = r.documento?.sqlId ?? r.sqlId
    const titulo = r.documento?.titulo ?? r.titulo ?? `Documento #${idDoc}`
    setDocSelVer({ idDoc, titulo })
    setFormVer({ ComentarioCambio: '', ContenidoTexto: '' })
    setArchivoVer(null)
    setErrorNV('')
    setOkNV('')
    setModalNV(true)
  }

  const handleSubirVersion = async (e) => {
    e.preventDefault()
    setSubVer(true)
    setErrorNV('')
    try {
      const fd = new FormData()
      fd.append('ComentarioCambio', formVer.ComentarioCambio)
      if (archivoVer)                  fd.append('Archivo',        archivoVer)
      else if (formVer.ContenidoTexto) fd.append('ContenidoTexto', formVer.ContenidoTexto)
      await api.post(`/Documentos/${docSelVer.idDoc}/nueva-version`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      setOkNV('Nueva versión subida. El documento regresa a Borrador para revisión.')
      // Refrescar versiones del documento
      const vr = await api.get(`/Documentos/${docSelVer.idDoc}/versiones`)
      setVersionesMap(prev => ({ ...prev, [docSelVer.idDoc]: { cargando: false, data: vr.data } }))
      setTimeout(() => { setModalNV(false); setOkNV('') }, 2000)
    } catch (e) {
      setErrorNV(e.response?.data?.Mensaje || e.response?.data?.Error || 'Error al subir.')
    } finally {
      setSubVer(false)
    }
  }

  const badgeEstado = (estado) => {
    if (estado === 'Aprobado') return <span className="badge badge-verde">Aprobado</span>
    if (estado === 'Borrador') return <span className="badge badge-naranja">Borrador</span>
    if (estado === 'Obsoleto') return <span className="badge badge-gris">Obsoleto</span>
    return <span className="badge badge-gris">{estado ?? '—'}</span>
  }

  const formatFecha = (f) => f ? new Date(f).toLocaleString('es-MX', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
  }) : '—'

  const formatBytes = (b) => {
    if (!b) return '—'
    if (b < 1024)        return `${b} B`
    if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`
    return `${(b / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Documentos</h2>
        <button className="btn-primario" onClick={abrirModalSubir}><i className="bi bi-upload"></i> Subir documento</button>
      </div>

      {okSubir && <div className="alerta-ok">{okSubir}</div>}

      <form onSubmit={handleBuscar}>
        <div className="filtros-row">
          <input type="text" placeholder="Buscar por título, descripción, etiqueta o autor..."
            value={busqueda} onChange={(e) => setBusqueda(e.target.value)}
            style={{ flex: 1, minWidth: '200px' }} />
          <button type="submit" className="btn-primario" disabled={buscando}>
            {buscando ? 'Buscando...' : 'Buscar'}
          </button>
        </div>
      </form>

      {errorBusq && <div className="alerta-error">{errorBusq}</div>}

      {buscadoYa && !buscando && (
        <div className="card">
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Título / Versión</th>
                  <th>Área</th>
                  <th>Estado</th>
                  <th>Fecha</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {resultados.length === 0 ? (
                  <tr><td colSpan={5} className="sin-datos">Sin resultados.</td></tr>
                ) : resultados.map((r, i) => {
                  const doc     = r.documento ?? r
                  const estado  = r.estado    ?? '—'
                  const verActual = r.version ?? '—'
                  const idDoc   = doc.sqlId ?? i
                  const verInfo = versionesMap[idDoc]
                  const versiones = verInfo?.data?.versiones ?? []
                  const versionesAnteriores = versiones.filter(v => v.numeroVersion !== verActual)

                  return [
                    // ── Fila principal (versión actual, activa) ─────────────
                    <tr key={`doc-${idDoc}`}>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <span className="badge badge-morado">v{verActual}</span>
                          <span style={{ fontWeight: 600 }}>{doc.titulo}</span>
                          <span style={{ fontSize: '0.72rem', color: '#4ade80', fontWeight: 500 }}>● actual</span>
                        </div>
                        <div style={{ fontSize: '0.8rem', color: '#9ca3af', marginTop: '2px' }}>{doc.categoria}</div>
                        <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '3px' }}>
                          {doc.subidoPor && <span>Subido por: <strong>{doc.subidoPor}</strong></span>}
                        </div>
                        {doc.ultimoFlujo && (
                          <div style={{ fontSize: '0.75rem', marginTop: '2px' }}>
                            <span style={{
                              padding: '1px 6px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600,
                              background: doc.ultimoFlujo.decision === 'Aprobado' ? '#dcfce7' :
                                          doc.ultimoFlujo.decision === 'Rechazado' ? '#fee2e2' : '#fef9c3',
                              color: doc.ultimoFlujo.decision === 'Aprobado' ? '#166534' :
                                     doc.ultimoFlujo.decision === 'Rechazado' ? '#991b1b' : '#854d0e'
                            }}>{doc.ultimoFlujo.decision}</span>
                            {doc.ultimoFlujo.revisadoPor && (
                              <span style={{ color: '#6b7280' }}> · {doc.ultimoFlujo.revisadoPor}</span>
                            )}
                          </div>
                        )}
                        {r.firmasReq > 0 && (
                          <BarraFirmas requeridas={r.firmasReq} obtenidas={r.firmasOk} />
                        )}
                        {doc.etiquetas?.length > 0 && (
                          <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap', marginTop: '4px' }}>
                            {doc.etiquetas.map((e, j) => (
                              <span key={j} style={{
                                background: '#eff6ff', color: '#1d4ed8', fontSize: '0.68rem',
                                padding: '1px 6px', borderRadius: '4px', border: '1px solid #bfdbfe'
                              }}>{e}</span>
                            ))}
                          </div>
                        )}
                        {verInfo?.cargando && (
                          <div style={{ fontSize: '0.72rem', color: '#6b7280', marginTop: '4px' }}>
                            Cargando versiones...
                          </div>
                        )}
                      </td>
                      <td>{doc.area}</td>
                      <td>{badgeEstado(estado)}</td>
                      <td style={{ fontSize: '0.8rem', color: '#6b7280', whiteSpace: 'nowrap' }}>
                        {formatFecha(doc.fechaSubida)}
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                          <button className="btn-secundario" onClick={() => handleDescargar(idDoc, doc.titulo)}>
                            Descargar
                          </button>
                          {estado === 'Borrador' && (
                            <button className="btn-exito" onClick={() => handleSolicitar(idDoc)}>
                              Solicitar aprobación
                            </button>
                          )}
                          <button className="btn-secundario" onClick={() => verFlujo(r)}>Flujo</button>
                          <button className="btn-secundario" onClick={() => abrirNuevaVersion(r)}>
                            + Versión
                          </button>
                        </div>
                      </td>
                    </tr>,

                    // ── Filas de versiones anteriores (inactivas) ───────────
                    ...versionesAnteriores.map(v => (
                      <tr key={`ver-${idDoc}-${v.numeroVersion}`}
                        style={{ opacity: 0.45, background: 'rgba(0,0,0,0.12)' }}>
                        <td>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', paddingLeft: '12px' }}>
                            <span className="badge badge-gris">v{v.numeroVersion}</span>
                            <span style={{ fontSize: '0.875rem', color: '#9ca3af' }}>{doc.titulo}</span>
                            <span style={{ fontSize: '0.72rem', color: '#6b7280' }}>● inactiva</span>
                          </div>
                          <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '2px', paddingLeft: '12px' }}>
                            {v.subidoPor && <span>Subido por: {v.subidoPor}</span>}
                            {v.comentarioCambio && <span> · {v.comentarioCambio}</span>}
                          </div>
                        </td>
                        <td style={{ color: '#6b7280', fontSize: '0.875rem' }}>{doc.area}</td>
                        <td><span className="badge badge-gris">Inactiva</span></td>
                        <td style={{ fontSize: '0.8rem', color: '#6b7280', whiteSpace: 'nowrap' }}>
                          {formatFecha(v.fechaVersion)}
                        </td>
                        <td>
                          <button className="btn-secundario"
                            style={{ opacity: 0.7 }}
                            onClick={() => handleDescargar(idDoc, doc.titulo, v.numeroVersion)}>
                            ⬇ v{v.numeroVersion}
                          </button>
                        </td>
                      </tr>
                    ))
                  ]
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {!buscadoYa && (
        <div className="card"><p className="sin-datos">Usa el buscador para encontrar documentos.</p></div>
      )}

      {/* ── Modal subir documento ── */}
      {modalSubir && (
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModalSubir(false)}>
          <div className="modal-dialog modal-lg modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-file-earmark-plus-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Subir documento
                </h5>
                <button type="button" className="btn-close" onClick={() => setModalSubir(false)} aria-label="Cerrar"></button>
              </div>
              <form onSubmit={handleSubir}>
                <div className="modal-body">
                  <div className="form-grid">
                    <div className="campo-form">
                      <label>Título *</label>
                      <input required value={formDoc.Titulo}
                        onChange={(e) => setFormDoc({ ...formDoc, Titulo: e.target.value })}
                        placeholder="Nombre del documento" />
                    </div>
                    <div className="campo-form">
                      <label>Categoría *</label>
                      <select value={formDoc.IdCategoria}
                        onChange={(e) => setFormDoc({ ...formDoc, IdCategoria: e.target.value })}>
                        {categorias.map((c) => (
                          <option key={c.id} value={c.id}>{c.nombre}</option>
                        ))}
                      </select>
                    </div>
                    {(usuario?.es_area_general === true || usuario?.es_area_general === 'true' || usuario?.es_area_general === 'true') && (
                      <div className="campo-form">
                        <label>Área de destino</label>
                        <select value={formDoc.IdArea}
                          onChange={(e) => setFormDoc({ ...formDoc, IdArea: e.target.value })}>
                          <option value="">— General (visible para todas las áreas) —</option>
                          {areas.map((a) => (
                            <option key={a.id} value={a.id}>{a.nombre}</option>
                          ))}
                        </select>
                        <span style={{ fontSize: '0.75rem', fontStyle: 'italic',
                          color: formDoc.IdArea ? '#fbbf24' : '#4ade80' }}>
                          {formDoc.IdArea
                            ? `📌 Solo visible para: ${areas.find(a => String(a.id) === String(formDoc.IdArea))?.nombre || 'área seleccionada'}`
                            : '🌐 Visible para TODAS las áreas (área General)'}
                        </span>
                      </div>
                    )}
                    <div className="campo-form">
                      <label>Etiquetas <span style={{ fontWeight: 400, color: '#9ca3af' }}>(separa con comas)</span></label>
                      <input value={etiquetasInput}
                        onChange={(e) => setEtiquetasInput(e.target.value)}
                        placeholder="calidad, iso, manual…" />
                    </div>
                    <div className="campo-form" style={{ gridColumn: '1 / -1' }}>
                      <label>Archivo PDF</label>
                      <input type="file" accept=".pdf" onChange={(e) => setArchivo(e.target.files[0])} />
                    </div>
                    {!archivo && (
                      <div className="campo-form" style={{ gridColumn: '1 / -1' }}>
                        <label>O escribe el contenido con formato</label>
                        <EditorTexto value={formDoc.ContenidoTexto}
                          onChange={(html) => setFormDoc({ ...formDoc, ContenidoTexto: html })}
                          placeholder="Escribe el contenido del documento aquí..." />
                      </div>
                    )}
                  </div>
                  {errorSubir && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorSubir}</div>}
                  {okSubir    && <div className="alerta-ok"    style={{ marginTop: '1rem' }}>{okSubir}</div>}
                </div>
                <div className="modal-footer">
                  <button type="button" className="btn-secundario" onClick={() => setModalSubir(false)}>Cancelar</button>
                  <button type="submit" className="btn-primario" disabled={subiendoDoc}>
                    {subiendoDoc ? 'Subiendo...' : 'Subir documento'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal flujo ── */}
      {modalFlujo && (
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModalFlujo(false)}>
          <div className="modal-dialog modal-dialog-centered modal-lg" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-diagram-3-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Flujo — {flujoData?.tituloDocumento ?? '...'}
                </h5>
                <button type="button" className="btn-close" onClick={() => setModalFlujo(false)} aria-label="Cerrar"></button>
              </div>
              <div className="modal-body">
                {errorFlujo && <div className="alerta-error">{errorFlujo}</div>}
                {okFlujo    && <div className="alerta-ok">{okFlujo}</div>}
                {cargFlujo ? <p className="cargando-txt">Cargando...</p> : flujoData ? (
                  <>
                    <div style={{ display: 'flex', gap: '10px', marginBottom: '0.75rem', flexWrap: 'wrap', alignItems: 'center' }}>
                      <span className="badge badge-gris">v{flujoData.version}</span>
                      {flujoData.estadoActual === 'Aprobado' && <span className="badge badge-verde">Aprobado</span>}
                      {flujoData.estadoActual === 'Borrador'  && <span className="badge badge-naranja">Borrador</span>}
                      {flujoData.haySolicitudActiva           && <span className="badge badge-azul">Revisión pendiente</span>}
                    </div>
                    {flujoData.firmasRequeridas > 0 && (
                      <div style={{ marginBottom: '1rem', padding: '10px 12px', background: 'rgba(79,142,247,0.08)', border: '1px solid rgba(79,142,247,0.2)', borderRadius: '8px' }}>
                        <BarraFirmas requeridas={flujoData.firmasRequeridas} obtenidas={flujoData.firmasObtenidas} />
                      </div>
                    )}
                    {flujoData.historial?.length === 0 ? (
                      <p className="sin-datos">Sin historial de aprobación aún.</p>
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '1rem' }}>
                        {flujoData.historial?.map((h) => (
                          <div key={h.idFlujo} className="card" style={{ marginBottom: 0 }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '6px' }}>
                              <div>
                                {h.areaRequerida && (
                                  <span style={{ fontSize: '0.8rem', fontWeight: 600 }}>Área: {h.areaRequerida}</span>
                                )}
                                <span style={{ marginLeft: h.areaRequerida ? '8px' : 0, color: '#6b7280', fontSize: '0.8rem' }}>
                                  · Solicitado por: {h.nombreSolicitante}
                                </span>
                              </div>
                              {h.decision === 'Pendiente'  && <span className="badge badge-naranja">Pendiente</span>}
                              {h.decision === 'Aprobado'   && <span className="badge badge-verde">Aprobado</span>}
                              {h.decision === 'Rechazado'  && <span className="badge badge-rojo">Rechazado</span>}
                              {h.decision === 'Cancelado'  && <span className="badge badge-gris">Cancelado</span>}
                            </div>
                            {h.nombreRevisor !== 'Pendiente de revisión' && (
                              <div style={{ fontSize: '0.875rem', color: '#6b7280', marginTop: '4px' }}>
                                Revisado por: {h.nombreRevisor}
                              </div>
                            )}
                            {h.comentarios && (
                              <div style={{ marginTop: '6px', fontSize: '0.875rem', background: 'var(--bg-base)', border: '1px solid var(--border-light)', padding: '8px', borderRadius: '6px' }}>
                                "{h.comentarios}"
                              </div>
                            )}
                            {h.decision === 'Pendiente' && (
                              <div style={{ marginTop: '10px' }}>
                                <button className="btn-primario" onClick={() => abrirResolver(h.idFlujo)}>
                                  Resolver esta solicitud
                                </button>
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                    {flujoData.estadoActual === 'Borrador' && !flujoData.haySolicitudActiva && (
                      <button className="btn-primario" onClick={async () => {
                        await handleSolicitar(flujoData.idDocumento)
                        const res = await api.get(`/Documentos/${flujoData.idDocumento}/flujo`)
                        setFlujoData(res.data)
                      }}>
                        Solicitar aprobación
                      </button>
                    )}
                  </>
                ) : null}
              </div>
              <div className="modal-footer">
                <button className="btn-secundario" onClick={() => setModalFlujo(false)}>Cerrar</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal resolver aprobación ── */}
      {modalRes && (
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModalRes(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-check-circle-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Resolver solicitud #{idFlujoRes}
                </h5>
                <button type="button" className="btn-close" onClick={() => setModalRes(false)} aria-label="Cerrar"></button>
              </div>
              <form onSubmit={handleResolver}>
                <div className="modal-body">
                  <div className="form-grid una-col">
                    <div className="campo-form">
                      <label>Decisión</label>
                      <select value={formRes.Decision}
                        onChange={(e) => setFormRes({ ...formRes, Decision: e.target.value })}>
                        <option value="Aprobado">Aprobado</option>
                        <option value="Rechazado">Rechazado</option>
                      </select>
                    </div>
                    <div className="campo-form">
                      <label>Comentarios {formRes.Decision === 'Rechazado' ? '*' : '(opcional)'}</label>
                      <textarea required={formRes.Decision === 'Rechazado'}
                        value={formRes.Comentarios}
                        onChange={(e) => setFormRes({ ...formRes, Comentarios: e.target.value })}
                        placeholder="Describe el motivo de tu decisión..." />
                    </div>
                  </div>
                </div>
                <div className="modal-footer">
                  <button type="button" className="btn-secundario" onClick={() => setModalRes(false)}>Cancelar</button>
                  <button type="submit"
                    className={formRes.Decision === 'Aprobado' ? 'btn-primario' : 'btn-peligro'}
                    disabled={resolviendoF}>
                    {resolviendoF ? 'Guardando...' : `Confirmar ${formRes.Decision}`}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal nueva versión ── */}
      {modalNuevaVer && (
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModalNV(false)}>
          <div className="modal-dialog modal-dialog-centered modal-lg" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-file-earmark-arrow-up-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Nueva versión — {docSelVer?.titulo}
                </h5>
                <button type="button" className="btn-close" onClick={() => setModalNV(false)} aria-label="Cerrar"></button>
              </div>
              <form onSubmit={handleSubirVersion}>
                <div className="modal-body">
                  <div className="alerta-error" style={{ marginBottom: '1rem', borderLeftColor: '#f59e0b', color: '#fbbf24', background: 'rgba(245,158,11,0.08)', border: '1px solid rgba(245,158,11,0.25)', borderLeftWidth: '3px' }}>
                    <i className="bi bi-exclamation-triangle-fill" style={{ fontSize: '1.1rem', marginRight: '8px' }}></i>
                    <span>El documento regresará a <strong>Borrador</strong> y necesitará una nueva aprobación.</span>
                  </div>
                  <div className="form-grid una-col">
                    <div className="campo-form">
                      <label>¿Qué cambió en esta versión? *</label>
                      <textarea required value={formVer.ComentarioCambio}
                        onChange={(e) => setFormVer({ ...formVer, ComentarioCambio: e.target.value })}
                        placeholder="Ej: Se actualizó la sección 3.2..." />
                    </div>
                    <div className="campo-form">
                      <label>Nuevo archivo PDF</label>
                      <input type="file" accept=".pdf" onChange={(e) => setArchivoVer(e.target.files[0])} />
                    </div>
                    {!archivoVer && (
                      <div className="campo-form">
                        <label>O escribe el nuevo contenido con formato</label>
                        <EditorTexto value={formVer.ContenidoTexto}
                          onChange={(html) => setFormVer({ ...formVer, ContenidoTexto: html })}
                          placeholder="Nuevo contenido del documento..." />
                      </div>
                    )}
                  </div>
                  {errorNV && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorNV}</div>}
                  {okNV    && <div className="alerta-ok"    style={{ marginTop: '1rem' }}>{okNV}</div>}
                </div>
                <div className="modal-footer">
                  <button type="button" className="btn-secundario" onClick={() => setModalNV(false)}>Cancelar</button>
                  <button type="submit" className="btn-primario" disabled={subiendoVer}>
                    {subiendoVer ? 'Subiendo...' : 'Subir versión'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}