import { useState } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'
import EditorTexto from '../../components/EditorTexto'

export default function SeccionDocumentos() {
  const [busqueda, setBusqueda]       = useState('')
  const [resultados, setResultados]   = useState([])
  const [buscando, setBuscando]       = useState(false)
  const [errorBusq, setErrorBusq]     = useState('')
  const [buscadoYa, setBuscadoYa]     = useState(false)

  // Subir documento
  const [modalSubir, setModalSubir]   = useState(false)
  const [areas, setAreas]             = useState([])
  const [formDoc, setFormDoc]         = useState({
    Titulo: '', Autor: '', IdCategoria: 1, IdArea: '', ContenidoTexto: ''
  })
  const [archivo, setArchivo]         = useState(null)
  const [subiendoDoc, setSubiendoDoc] = useState(false)
  const [errorSubir, setErrorSubir]   = useState('')
  const [okSubir, setOkSubir]         = useState('')

  // Flujo de aprobación
  const [modalFlujo, setModalFlujo]   = useState(false)
  const [docSel, setDocSel]           = useState(null)
  const [flujoData, setFlujoData]     = useState(null)
  const [cargFlujo, setCargFlujo]     = useState(false)
  const [errorFlujo, setErrorFlujo]   = useState('')
  const [okFlujo, setOkFlujo]         = useState('')
  const [modalRes, setModalRes]       = useState(false)
  const [idFlujoRes, setIdFlujoRes]   = useState(null)
  const [formRes, setFormRes]         = useState({ Decision: 'Aprobado', Comentarios: '' })
  const [resolviendoF, setResF]       = useState(false)

  // Versiones
  const [modalVersiones, setModalVer] = useState(false)
  const [docSelVer, setDocSelVer]     = useState(null)
  const [versionesData, setVerData]   = useState(null)
  const [cargVer, setCargVer]         = useState(false)
  const [errorVer, setErrorVer]       = useState('')
  const [okVer, setOkVer]             = useState('')

  // Nueva versión
  const [modalNuevaVer, setModalNV]   = useState(false)
  const [formVer, setFormVer]         = useState({ ComentarioCambio: '', ContenidoTexto: '' })
  const [archivoVer, setArchivoVer]   = useState(null)
  const [subiendoVer, setSubVer]      = useState(false)
  const [errorNV, setErrorNV]         = useState('')

  const categorias = [
    { id: 1, nombre: 'Manual de Calidad' },
    { id: 2, nombre: 'Procedimiento' },
    { id: 3, nombre: 'Instrucción de Trabajo' },
    { id: 4, nombre: 'Registro de Calidad' },
    { id: 5, nombre: 'Plan de Control' },
    { id: 6, nombre: 'Auditoría' },
  ]

  // ── Buscar ────────────────────────────────────────────────────────
  const handleBuscar = async (e) => {
    e.preventDefault()
    if (!busqueda.trim()) return
    setBuscando(true)
    setErrorBusq('')
    setResultados([])
    setBuscadoYa(true)
    try {
      const r = await api.get(`/Documentos/buscar/${encodeURIComponent(busqueda)}`)
      setResultados(r.data.resultados || [])
    } catch (e) {
      setErrorBusq(e.response?.data?.Mensaje || 'No se encontraron resultados.')
    } finally {
      setBuscando(false)
    }
  }

  // ── Descargar versión actual ──────────────────────────────────────
  const handleDescargar = async (idDoc, titulo) => {
    try {
      const r = await api.get(`/Documentos/descargar/${idDoc}`, { responseType: 'blob' })
      const url  = window.URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
      const link = document.createElement('a')
      link.href = url
      link.download = `${titulo || 'documento'}.pdf`
      link.click()
      window.URL.revokeObjectURL(url)
    } catch {
      alert('Error al descargar el documento.')
    }
  }

  // ── Descargar versión específica del historial ────────────────────
  const handleDescargarVersion = async (idDoc, numeroVersion, tituloDoc) => {
    try {
      const r = await api.get(
        `/Documentos/${idDoc}/versiones/${numeroVersion}/descargar`,
        { responseType: 'blob' }
      )
      const url  = window.URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
      const link = document.createElement('a')
      link.href = url
      link.download = `v${numeroVersion}_${tituloDoc || 'documento'}.pdf`
      link.click()
      window.URL.revokeObjectURL(url)
    } catch (e) {
      alert(e.response?.data?.Mensaje || `Error al descargar la versión ${numeroVersion}.`)
    }
  }

  // ── Subir documento ───────────────────────────────────────────────
  const abrirModalSubir = async () => {
    try {
      const r = await api.get('/Areas')
      setAreas(r.data.areas || [])
    } catch { setAreas([]) }
    setFormDoc({ Titulo: '', Autor: '', IdCategoria: 1, IdArea: '', ContenidoTexto: '' })
    setArchivo(null)
    setErrorSubir('')
    setOkSubir('')
    setModalSubir(true)
  }

  const handleSubir = async (e) => {
    e.preventDefault()
    setSubiendoDoc(true)
    setErrorSubir('')

    // Validación en el frontend antes de llamar al backend:
    // el usuario debe adjuntar un PDF o escribir algo en el editor
    const tieneArchivo  = !!archivo
    const tieneContenido = formDoc.ContenidoTexto &&
      formDoc.ContenidoTexto.replace(/<[^>]*>/g, '').trim() !== ''

    if (!tieneArchivo && !tieneContenido) {
      setErrorSubir('Debes adjuntar un archivo PDF o escribir el contenido del documento.')
      setSubiendoDoc(false)
      return
    }

    try {
      const fd = new FormData()
      fd.append('Titulo',      formDoc.Titulo)
      fd.append('Autor',       formDoc.Autor)
      fd.append('IdCategoria', formDoc.IdCategoria)
      if (formDoc.IdArea)              fd.append('IdArea',        formDoc.IdArea)
      if (archivo)                     fd.append('Archivo',       archivo)
      else if (formDoc.ContenidoTexto) fd.append('ContenidoTexto', formDoc.ContenidoTexto)
      await api.post('/Documentos', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
      setOkSubir('Documento subido correctamente.')
      setTimeout(() => { setModalSubir(false); setOkSubir('') }, 2000)
    } catch (e) {
      setErrorSubir(e.response?.data?.Mensaje || e.response?.data?.Error || 'Error al subir.')
    } finally {
      setSubiendoDoc(false)
    }
  }

  // ── Flujo de aprobación ───────────────────────────────────────────
  const handleSolicitar = async (idDoc) => {
    try {
      await api.put(`/Documentos/solicitar-aprobacion/${idDoc}`)
      alert('Solicitud de revisión creada correctamente.')
    } catch (e) {
      alert(e.response?.data?.Mensaje || 'Error al solicitar aprobación.')
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
      await api.put(`/Documentos/resolver-aprobacion/${idFlujoRes}`, formRes)
      setOkFlujo(`Documento ${formRes.Decision.toLowerCase()} correctamente.`)
      setModalRes(false)
      const idDoc = docSel?.documento?.sqlId ?? docSel?.sqlId
      const res = await api.get(`/Documentos/${idDoc}/flujo`)
      setFlujoData(res.data)
    } catch (e) {
      setErrorFlujo(e.response?.data?.Mensaje || 'Error al resolver.')
    } finally {
      setResF(false)
    }
  }

  // ── Versiones ─────────────────────────────────────────────────────
  const verVersiones = async (r) => {
    const idDoc  = r.documento?.sqlId ?? r.sqlId
    const titulo = r.documento?.titulo ?? r.titulo ?? `Documento #${idDoc}`
    setDocSelVer({ idDoc, titulo })
    setVerData(null)
    setErrorVer('')
    setOkVer('')
    setModalVer(true)
    setCargVer(true)
    try {
      const res = await api.get(`/Documentos/${idDoc}/versiones`)
      setVerData(res.data)
    } catch (e) {
      setErrorVer(e.response?.data?.Mensaje || 'No se pudo cargar el historial de versiones.')
    } finally {
      setCargVer(false)
    }
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
      setOkVer('Nueva versión subida. El documento regresa a Borrador para revisión.')
      setModalNV(false)
      const res = await api.get(`/Documentos/${docSelVer.idDoc}/versiones`)
      setVerData(res.data)
    } catch (e) {
      setErrorNV(e.response?.data?.Mensaje || e.response?.data?.Error || 'Error al subir.')
    } finally {
      setSubVer(false)
    }
  }

  // ── Helpers visuales ─────────────────────────────────────────────
  const badgeEstado = (estado) => {
    if (estado === 'Aprobado') return <span className="badge badge-verde">Aprobado</span>
    if (estado === 'Borrador') return <span className="badge badge-naranja">Borrador</span>
    if (estado === 'Obsoleto') return <span className="badge badge-gris">Obsoleto</span>
    return <span className="badge badge-gris">{estado ?? '—'}</span>
  }

  const formatFecha = (f) => f ? new Date(f).toLocaleString('es-MX', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
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
        <button className="btn-primario" onClick={abrirModalSubir}>+ Subir documento</button>
      </div>

      {okSubir && <div className="alerta-ok">{okSubir}</div>}

      {/* Buscador */}
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

      {errorBusq && <div className="alerta-error">{errorBusq}</div>}

      {buscadoYa && !buscando && (
        <div className="card">
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Título</th>
                  <th>Área</th>
                  <th>Estado</th>
                  <th>Ver.</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {resultados.length === 0 ? (
                  <tr><td colSpan={5} className="sin-datos">Sin resultados.</td></tr>
                ) : resultados.map((r, i) => {
                  const doc    = r.documento ?? r
                  const estado = r.estado    ?? '—'
                  const ver    = r.version   ?? '—'
                  return (
                    <tr key={doc.sqlId ?? i}>
                      <td>
                        <div style={{ fontWeight: 500 }}>{doc.titulo}</div>
                        <div style={{ fontSize: '0.8rem', color: '#9ca3af' }}>{doc.categoria}</div>
                      </td>
                      <td>{doc.area}</td>
                      <td>{badgeEstado(estado)}</td>
                      <td>v{ver}</td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                          <button className="btn-secundario" onClick={() => handleDescargar(doc.sqlId, doc.titulo)}>
                            Descargar
                          </button>
                          {estado === 'Borrador' && (
                            <button className="btn-exito" onClick={() => handleSolicitar(doc.sqlId)}>
                              Solicitar aprobación
                            </button>
                          )}
                          <button className="btn-secundario" onClick={() => verFlujo(r)}>
                            Flujo
                          </button>
                          {/* Admin puede ver versiones de cualquier documento */}
                          <button className="btn-secundario" onClick={() => verVersiones(r)}>
                            Versiones
                          </button>
                        </div>
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
          <p className="sin-datos">Usa el buscador para encontrar documentos.</p>
        </div>
      )}

      {/* ── Modal subir documento ───────────────────────────────────── */}
      {modalSubir && (
        <div className="modal-fondo">
          <div className="modal-card">
            <h3 className="modal-titulo">Subir documento</h3>
            <form onSubmit={handleSubir}>
              <div className="form-grid">
                <div className="campo-form">
                  <label>Título *</label>
                  <input required value={formDoc.Titulo}
                    onChange={(e) => setFormDoc({ ...formDoc, Titulo: e.target.value })}
                    placeholder="Nombre del documento" />
                </div>
                <div className="campo-form">
                  <label>Autor *</label>
                  <input required value={formDoc.Autor}
                    onChange={(e) => setFormDoc({ ...formDoc, Autor: e.target.value })}
                    placeholder="Nombre del autor" />
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
                <div className="campo-form">
                  <label>Área (opcional)</label>
                  <select value={formDoc.IdArea}
                    onChange={(e) => setFormDoc({ ...formDoc, IdArea: e.target.value })}>
                    <option value="">-- Sin área específica (usará General) --</option>
                    {areas.map((a) => (
                      <option key={a.id} value={a.id}>{a.nombre}</option>
                    ))}
                  </select>
                </div>
                <div className="campo-form" style={{ gridColumn: '1 / -1' }}>
                  <label>Archivo PDF</label>
                  <input type="file" accept=".pdf"
                    onChange={(e) => setArchivo(e.target.files[0])} />
                </div>
                {!archivo && (
                  <div className="campo-form" style={{ gridColumn: '1 / -1' }}>
                    <label>O escribe el contenido con formato</label>
                    <EditorTexto
                      value={formDoc.ContenidoTexto}
                      onChange={(html) => setFormDoc({ ...formDoc, ContenidoTexto: html })}
                      placeholder="Escribe el contenido del documento aquí. Puedes usar títulos, listas, negritas..."
                    />
                  </div>
                )}
              </div>
              {errorSubir && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorSubir}</div>}
              {okSubir    && <div className="alerta-ok"    style={{ marginTop: '1rem' }}>{okSubir}</div>}
              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalSubir(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn-primario" disabled={subiendoDoc}>
                  {subiendoDoc ? 'Subiendo...' : 'Subir documento'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal flujo de aprobación ───────────────────────────────── */}
      {modalFlujo && (
        <div className="modal-fondo" onClick={() => setModalFlujo(false)}>
          <div className="modal-card" style={{ maxWidth: '640px' }} onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Flujo — {flujoData?.tituloDocumento ?? '...'}</h3>
            {errorFlujo && <div className="alerta-error">{errorFlujo}</div>}
            {okFlujo    && <div className="alerta-ok">{okFlujo}</div>}
            {cargFlujo ? <p className="cargando-txt">Cargando...</p> : flujoData ? (
              <>
                <div style={{ display: 'flex', gap: '10px', marginBottom: '1rem', flexWrap: 'wrap' }}>
                  <span className="badge badge-gris">v{flujoData.version}</span>
                  {flujoData.estadoActual === 'Aprobado' && <span className="badge badge-verde">Aprobado</span>}
                  {flujoData.estadoActual === 'Borrador'  && <span className="badge badge-naranja">Borrador</span>}
                  {flujoData.haySolicitudActiva           && <span className="badge badge-azul">Revisión pendiente</span>}
                </div>
                {flujoData.historial?.length === 0 ? (
                  <p className="sin-datos">Sin historial de aprobación aún.</p>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '1rem' }}>
                    {flujoData.historial?.map((h) => (
                      <div key={h.idFlujo} className="card" style={{ marginBottom: 0 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '6px' }}>
                          <span><strong>Solicitado por:</strong> {h.nombreSolicitante}</span>
                          {h.decision === 'Pendiente'  && <span className="badge badge-naranja">Pendiente</span>}
                          {h.decision === 'Aprobado'   && <span className="badge badge-verde">Aprobado</span>}
                          {h.decision === 'Rechazado'  && <span className="badge badge-rojo">Rechazado</span>}
                        </div>
                        {h.nombreRevisor !== 'Pendiente de revisión' && (
                          <div style={{ fontSize: '0.875rem', color: '#6b7280', marginTop: '4px' }}>
                            Revisado por: {h.nombreRevisor}
                          </div>
                        )}
                        {h.comentarios && (
                          <div style={{ marginTop: '6px', fontSize: '0.875rem', background: '#f9fafb', padding: '8px', borderRadius: '6px' }}>
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
            <div className="modal-acciones">
              <button className="btn-secundario" onClick={() => setModalFlujo(false)}>Cerrar</button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal resolver aprobación ───────────────────────────────── */}
      {modalRes && (
        <div className="modal-fondo" onClick={() => setModalRes(false)}>
          <div className="modal-card" style={{ maxWidth: '440px' }} onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Resolver solicitud #{idFlujoRes}</h3>
            <form onSubmit={handleResolver}>
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
              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalRes(false)}>
                  Cancelar
                </button>
                <button type="submit"
                  className={formRes.Decision === 'Aprobado' ? 'btn-primario' : 'btn-peligro'}
                  disabled={resolviendoF}>
                  {resolviendoF ? 'Guardando...' : `Confirmar ${formRes.Decision}`}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal versiones ─────────────────────────────────────────── */}
      {modalVersiones && (
        <div className="modal-fondo" onClick={() => setModalVer(false)}>
          <div className="modal-card" style={{ maxWidth: '700px' }} onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Versiones — {docSelVer?.titulo}</h3>

            {errorVer && <div className="alerta-error">{errorVer}</div>}
            {okVer    && <div className="alerta-ok">{okVer}</div>}

            {cargVer ? <p className="cargando-txt">Cargando...</p> : versionesData ? (
              <>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '8px' }}>
                  <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>
                    Versión actual: <strong>v{versionesData.versionActual}</strong>
                    {' · '}
                    {versionesData.estadoActual === 'Aprobado'
                      ? <span className="badge badge-verde">Aprobado</span>
                      : <span className="badge badge-naranja">Borrador</span>}
                  </div>
                  <button className="btn-primario" onClick={() => {
                    setFormVer({ ComentarioCambio: '', ContenidoTexto: '' })
                    setArchivoVer(null)
                    setErrorNV('')
                    setModalNV(true)
                  }}>
                    + Nueva versión
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
                        <th>Comentario</th>
                        <th>Descargar</th>
                      </tr>
                    </thead>
                    <tbody>
                      {versionesData.versiones?.length === 0 ? (
                        <tr><td colSpan={6} className="sin-datos">Sin versiones.</td></tr>
                      ) : versionesData.versiones?.map((v) => (
                        <tr key={v.idVersion}>
                          <td>
                            <span className="badge badge-morado">v{v.numeroVersion}</span>
                            {v.numeroVersion === versionesData.versionActual && (
                              <span style={{ marginLeft: '5px', fontSize: '0.75rem', color: '#6b7280' }}>actual</span>
                            )}
                          </td>
                          <td>{v.subidoPor}</td>
                          <td style={{ whiteSpace: 'nowrap' }}>{formatFecha(v.fechaVersion)}</td>
                          <td>{formatBytes(v.tamanoBytes)}</td>
                          <td style={{ fontSize: '0.8125rem', color: '#6b7280', maxWidth: '180px' }}>
                            {v.comentarioCambio || '—'}
                          </td>
                          <td>
                            {/* Botón de descarga por versión — Admin puede descargar cualquiera */}
                            <button
                              className="btn-secundario"
                              onClick={() => handleDescargarVersion(
                                docSelVer.idDoc,
                                v.numeroVersion,
                                docSelVer.titulo
                              )}
                            >
                              ⬇ v{v.numeroVersion}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            ) : null}

            <div className="modal-acciones">
              <button className="btn-secundario" onClick={() => setModalVer(false)}>Cerrar</button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal nueva versión ─────────────────────────────────────── */}
      {modalNuevaVer && (
        <div className="modal-fondo" onClick={() => setModalNV(false)}>
          <div className="modal-card" style={{ maxWidth: '480px' }} onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Nueva versión — {docSelVer?.titulo}</h3>
            <p style={{ fontSize: '0.875rem', color: '#854F0B', background: '#fef3c7', padding: '8px 12px', borderRadius: '8px', marginBottom: '1rem' }}>
              El documento regresará a <strong>Borrador</strong> y necesitará una nueva aprobación.
            </p>
            <form onSubmit={handleSubirVersion}>
              <div className="form-grid una-col">
                <div className="campo-form">
                  <label>¿Qué cambió en esta versión? *</label>
                  <textarea required value={formVer.ComentarioCambio}
                    onChange={(e) => setFormVer({ ...formVer, ComentarioCambio: e.target.value })}
                    placeholder="Ej: Se actualizó la sección 3.2 por cambio en el procedimiento..." />
                </div>
                <div className="campo-form">
                  <label>Nuevo archivo PDF</label>
                  <input type="file" accept=".pdf"
                    onChange={(e) => setArchivoVer(e.target.files[0])} />
                </div>
                {!archivoVer && (
                  <div className="campo-form">
                    <label>O escribe el nuevo contenido con formato</label>
                    <EditorTexto
                      value={formVer.ContenidoTexto}
                      onChange={(html) => setFormVer({ ...formVer, ContenidoTexto: html })}
                      placeholder="Nuevo contenido del documento con formato..."
                    />
                  </div>
                )}
              </div>
              {errorNV && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorNV}</div>}
              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalNV(false)}>
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
