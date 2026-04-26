import { useState, useEffect } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'

export default function SeccionLogs() {
  const [logs, setLogs]         = useState([])
  const [resumen, setResumen]   = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError]       = useState('')
  const [total, setTotal]       = useState(0)
  const [pagina, setPagina]     = useState(1)

  const [filtros, setFiltros] = useState({
    idUsuario: '',
    accion:    '',
    desde:     '',
    hasta:     '',
  })

  const cargarLogs = async (pag = 1) => {
    setCargando(true)
    setError('')
    try {
      const params = new URLSearchParams()
      params.append('pagina', pag)
      params.append('tamano', 20)
      if (filtros.idUsuario) params.append('idUsuario', filtros.idUsuario)
      if (filtros.accion)    params.append('accion',    filtros.accion)
      if (filtros.desde)     params.append('desde',     filtros.desde)
      if (filtros.hasta)     params.append('hasta',     filtros.hasta)

      const [rL, rR] = await Promise.all([
        api.get(`/Logs?${params.toString()}`),
        api.get('/Logs/resumen'),
      ])
      setLogs(rL.data.logs      || [])
      setTotal(rL.data.totalRegistros || 0)
      setResumen(rR.data.porAccion    || [])
      setPagina(pag)
    } catch {
      setError('Error al cargar los logs.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => { cargarLogs(1) }, [])

  const handleBuscar = (e) => {
    e.preventDefault()
    cargarLogs(1)
  }

  const formatFecha = (fecha) => {
    if (!fecha) return '—'
    return new Date(fecha).toLocaleString('es-MX', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    })
  }

  const badgeAccion = (accion) => {
    const a = accion?.toUpperCase()
    if (a === 'SUBIO')              return <span className="badge badge-azul">{accion}</span>
    if (a === 'APROBÓ')             return <span className="badge badge-verde">{accion}</span>
    if (a === 'RECHAZÓ')            return <span className="badge badge-rojo">{accion}</span>
    if (a === 'DESCARGÓ')           return <span className="badge badge-gris">{accion}</span>
    if (a === 'SOLICITÓ REVISIÓN')  return <span className="badge badge-naranja">{accion}</span>
    if (a === 'NUEVA VERSIÓN')      return <span className="badge badge-morado">{accion}</span>
    return <span className="badge badge-gris">{accion}</span>
  }

  const totalPaginas = Math.ceil(total / 20)

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Logs de auditoría</h2>
        <span style={{ fontSize: '0.875rem', color: '#6b7280' }}>
          {total} registros totales
        </span>
      </div>

      {/* Tarjetas resumen */}
      {resumen.length > 0 && (
        <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
          {resumen.map((r) => (
            <div key={r.accion} className="card" style={{ padding: '12px 16px', minWidth: '120px', textAlign: 'center', marginBottom: 0 }}>
              <div style={{ fontSize: '1.5rem', fontWeight: 600, color: '#4f46e5' }}>{r.total}</div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '2px' }}>{r.accion}</div>
            </div>
          ))}
        </div>
      )}

      {/* Filtros */}
      <form onSubmit={handleBuscar}>
        <div className="filtros-row">
          <input
            type="number"
            placeholder="ID de usuario"
            value={filtros.idUsuario}
            onChange={(e) => setFiltros({ ...filtros, idUsuario: e.target.value })}
            style={{ width: '130px' }}
          />
          <select
            value={filtros.accion}
            onChange={(e) => setFiltros({ ...filtros, accion: e.target.value })}
          >
            <option value="">Todas las acciones</option>
            <option value="SUBIO">SUBIO</option>
            <option value="APROBÓ">APROBÓ</option>
            <option value="RECHAZÓ">RECHAZÓ</option>
            <option value="DESCARGÓ">DESCARGÓ</option>
            <option value="SOLICITÓ REVISIÓN">SOLICITÓ REVISIÓN</option>
            <option value="NUEVA VERSIÓN">NUEVA VERSIÓN</option>
          </select>
          <input
            type="date"
            value={filtros.desde}
            onChange={(e) => setFiltros({ ...filtros, desde: e.target.value })}
          />
          <input
            type="date"
            value={filtros.hasta}
            onChange={(e) => setFiltros({ ...filtros, hasta: e.target.value })}
          />
          <button type="submit" className="btn-primario">Buscar</button>
          <button
            type="button"
            className="btn-secundario"
            onClick={() => { setFiltros({ idUsuario: '', accion: '', desde: '', hasta: '' }); cargarLogs(1) }}
          >
            Limpiar
          </button>
        </div>
      </form>

      {error && <div className="alerta-error">{error}</div>}

      <div className="card">
        {cargando ? (
          <p className="cargando-txt">Cargando logs...</p>
        ) : (
          <>
            <div className="tabla-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Usuario</th>
                    <th>Área</th>
                    <th>Acción</th>
                    <th>Detalle</th>
                    <th>Fecha</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.length === 0 ? (
                    <tr><td colSpan={6} className="sin-datos">No hay registros con esos filtros.</td></tr>
                  ) : logs.map((log) => (
                    <tr key={log.idLog}>
                      <td style={{ color: '#9ca3af' }}>{log.idLog}</td>
                      <td>{log.nombreUsuario || `ID ${log.idUsuario}`}</td>
                      <td>{log.areaUsuario || '—'}</td>
                      <td>{badgeAccion(log.accion)}</td>
                      <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {log.detalle || '—'}
                      </td>
                      <td style={{ whiteSpace: 'nowrap' }}>{formatFecha(log.fechaAccion)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Paginación */}
            {totalPaginas > 1 && (
              <div style={{ display: 'flex', justifyContent: 'center', gap: '8px', marginTop: '1rem' }}>
                <button
                  className="btn-secundario"
                  disabled={pagina === 1}
                  onClick={() => cargarLogs(pagina - 1)}
                >
                  ← Anterior
                </button>
                <span style={{ padding: '6px 12px', fontSize: '0.875rem', color: '#6b7280' }}>
                  Página {pagina} de {totalPaginas}
                </span>
                <button
                  className="btn-secundario"
                  disabled={pagina === totalPaginas}
                  onClick={() => cargarLogs(pagina + 1)}
                >
                  Siguiente →
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
