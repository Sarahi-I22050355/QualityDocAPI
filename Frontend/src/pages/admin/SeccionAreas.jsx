import { useState, useEffect } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'

export default function SeccionAreas() {
  const [areas, setAreas]           = useState([])
  const [inactivas, setInactivas]   = useState([])
  const [vista, setVista]           = useState('activas') // 'activas' | 'inactivas'
  const [cargando, setCargando]     = useState(true)
  const [error, setError]           = useState('')
  const [ok, setOk]                 = useState('')
  const [modalAbierto, setModal]    = useState(false)
  const [modoEdicion, setModoEdicion] = useState(null) // null | area object
  const [form, setForm]             = useState({ Nombre: '', Descripcion: '', EsGeneral: false })
  const [enviando, setEnviando]     = useState(false)

  const mostrarOk    = (msg) => { setOk(msg);    setTimeout(() => setOk(''),    3000) }
  const mostrarError = (msg) => { setError(msg); setTimeout(() => setError(''), 4000) }

  const cargarDatos = async () => {
    setCargando(true)
    try {
      const [rA, rI] = await Promise.all([
        api.get('/Areas'),
        api.get('/Areas/inactivas'),
      ])
      setAreas(rA.data.areas      || [])
      setInactivas(rI.data.areas  || [])
    } catch {
      mostrarError('Error al cargar áreas.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => { cargarDatos() }, [])

  const abrirCrear = () => {
    setModoEdicion(null)
    setForm({ Nombre: '', Descripcion: '', EsGeneral: false })
    setModal(true)
  }

  const abrirEditar = (area) => {
    setModoEdicion(area)
    setForm({ Nombre: area.nombre, Descripcion: area.descripcion || '', EsGeneral: area.esGeneral })
    setModal(true)
  }

  const handleGuardar = async (e) => {
    e.preventDefault()
    setEnviando(true)
    setError('')
    try {
      if (modoEdicion) {
        await api.put(`/Areas/${modoEdicion.id}`, form)
        mostrarOk('Área actualizada correctamente.')
      } else {
        await api.post('/Areas', form)
        mostrarOk('Área creada exitosamente.')
      }
      setModal(false)
      cargarDatos()
    } catch (e) {
      setError(e.response?.data?.Mensaje || e.response?.data?.mensaje || 'Error al guardar.')
    } finally {
      setEnviando(false)
    }
  }

  const handleDesactivar = async (area) => {
    if (!confirm(`¿Desactivar el área "${area.nombre}"?`)) return
    try {
      await api.delete(`/Areas/${area.id}`)
      mostrarOk(`Área "${area.nombre}" desactivada.`)
      cargarDatos()
    } catch (e) {
      mostrarError(e.response?.data?.Mensaje || 'Error al desactivar.')
    }
  }

  const handleReactivar = async (area) => {
    try {
      await api.put(`/Areas/${area.id}/reactivar`)
      mostrarOk(`Área "${area.nombre}" reactivada.`)
      cargarDatos()
    } catch (e) {
      mostrarError(e.response?.data?.Mensaje || 'Error al reactivar.')
    }
  }

  const lista = vista === 'activas' ? areas : inactivas

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Áreas</h2>
        <button className="btn-primario" onClick={abrirCrear}>+ Nueva área</button>
      </div>

      {error && <div className="alerta-error">{error}</div>}
      {ok    && <div className="alerta-ok">{ok}</div>}

      {/* Tabs activas / inactivas */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '1rem' }}>
        <button
          className={vista === 'activas' ? 'btn-primario' : 'btn-secundario'}
          onClick={() => setVista('activas')}
        >
          Activas ({areas.length})
        </button>
        <button
          className={vista === 'inactivas' ? 'btn-primario' : 'btn-secundario'}
          onClick={() => setVista('inactivas')}
        >
          Inactivas ({inactivas.length})
        </button>
      </div>

      <div className="card">
        {cargando ? (
          <p className="cargando-txt">Cargando áreas...</p>
        ) : (
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Descripción</th>
                  <th>Tipo</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {lista.length === 0 ? (
                  <tr><td colSpan={4} className="sin-datos">No hay áreas en esta lista.</td></tr>
                ) : lista.map((a) => (
                  <tr key={a.id}>
                    <td><strong>{a.nombre}</strong></td>
                    <td>{a.descripcion || '—'}</td>
                    <td>
                      {a.esGeneral
                        ? <span className="badge badge-morado">General</span>
                        : <span className="badge badge-gris">Departamental</span>}
                    </td>
                    <td style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                      {vista === 'activas' ? (
                        <>
                          <button className="btn-secundario" onClick={() => abrirEditar(a)}>Editar</button>
                          {!a.esGeneral && (
                            <button className="btn-peligro" onClick={() => handleDesactivar(a)}>Desactivar</button>
                          )}
                        </>
                      ) : (
                        <button className="btn-exito" onClick={() => handleReactivar(a)}>Reactivar</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── Modal crear / editar ────────────────────────────────────── */}
      {modalAbierto && (
        <div className="modal-fondo" onClick={() => setModal(false)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">{modoEdicion ? 'Editar área' : 'Nueva área'}</h3>
            <form onSubmit={handleGuardar}>
              <div className="form-grid una-col">
                <div className="campo-form">
                  <label>Nombre del área</label>
                  <input
                    required
                    value={form.Nombre}
                    onChange={(e) => setForm({ ...form, Nombre: e.target.value })}
                    placeholder="Ej. Producción"
                  />
                </div>
                <div className="campo-form">
                  <label>Descripción (opcional)</label>
                  <textarea
                    value={form.Descripcion}
                    onChange={(e) => setForm({ ...form, Descripcion: e.target.value })}
                    placeholder="Descripción breve del área..."
                  />
                </div>
                <div className="campo-form" style={{ flexDirection: 'row', alignItems: 'center', gap: '10px' }}>
                  <input
                    type="checkbox"
                    id="esGeneral"
                    checked={form.EsGeneral}
                    onChange={(e) => setForm({ ...form, EsGeneral: e.target.checked })}
                    style={{ width: 'auto' }}
                  />
                  <label htmlFor="esGeneral" style={{ margin: 0 }}>
                    Área General (todos los usuarios pueden ver sus documentos)
                  </label>
                </div>
              </div>

              {error && <div className="alerta-error" style={{ marginTop: '1rem' }}>{error}</div>}

              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModal(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn-primario" disabled={enviando}>
                  {enviando ? 'Guardando...' : modoEdicion ? 'Guardar cambios' : 'Crear área'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
