import { useState, useEffect } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'
import { swalExito, swalError } from '../../utils/swal'

export default function SeccionUsuarios() {
  const [usuarios, setUsuarios]   = useState([])
  const [areas, setAreas]         = useState([])
  const [cargando, setCargando]   = useState(true)
  const [error, setError]         = useState('')
  const [ok, setOk]               = useState('')
  const [modalAbierto, setModal]  = useState(false)
  const [form, setForm]           = useState({
    NombreCompleto: '', Email: '', Password: '', IdRol: 2, IdArea: ''
  })
  const [enviando, setEnviando]   = useState(false)

  const cargarDatos = async () => {
    setCargando(true)
    try {
      const [rU, rA] = await Promise.all([
        api.get('/Usuarios'),
        api.get('/Areas'),
      ])
      setUsuarios(rU.data.usuarios || [])
      setAreas(rA.data.areas || [])
    } catch {
      setError('Error al cargar usuarios.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => { cargarDatos() }, [])

  const handleEstado = async (id, activoActual) => {
    try {
      await api.put(`/Usuarios/${id}/estado`, { Activo: !activoActual })
      swalExito(`Usuario ${activoActual ? 'desactivado' : 'activado'} correctamente.`)
      cargarDatos()
    } catch (e) {
      swalError(e.response?.data?.mensaje || 'Error al cambiar estado.')
    }
  }

  const handleCrear = async (e) => {
    e.preventDefault()
    setEnviando(true)
    setError('')
    try {
      await api.post('/Usuarios/crear', {
        ...form,
        IdRol:  Number(form.IdRol),
        IdArea: Number(form.IdArea),
      })
      swalExito('Usuario creado exitosamente.')
      setModal(false)
      setForm({ NombreCompleto: '', Email: '', Password: '', IdRol: 2, IdArea: '' })
      cargarDatos()
      setTimeout(() => { }, 0) // cleanup noop
    } catch (e) {
      setError(e.response?.data?.mensaje || 'Error al crear usuario.')
    } finally {
      setEnviando(false)
    }
  }

  const labelRol = (rol) => {
    const r = typeof rol === 'string' ? rol.toLowerCase() : ''
    if (r === 'admin' || r === 'administrador' || rol === 1) return <span className="badge badge-morado">Admin</span>
    if (r === 'supervisor'    || rol === 2) return <span className="badge badge-azul">Supervisor</span>
    if (r === 'revisor'       || rol === 4) return <span className="badge badge-naranja">Revisor</span>
    return <span className="badge badge-gris">Operario</span>
  }

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Usuarios</h2>
        <button className="btn-primario" onClick={() => setModal(true)}>
          <i className="bi bi-person-plus-fill"></i> Nuevo usuario
        </button>
      </div>

      {error && <div className="alerta-error">{error}</div>}
      {ok    && <div className="alerta-ok">{ok}</div>}

      <div className="card">
        {cargando ? (
          <p className="cargando-txt">Cargando usuarios...</p>
        ) : (
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Email</th>
                  <th>Rol</th>
                  <th>Área</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {usuarios.length === 0 ? (
                  <tr><td colSpan={6} className="sin-datos">No hay usuarios registrados.</td></tr>
                ) : usuarios.map((u) => (
                  <tr key={u.idUsuario}>
                    <td>{u.nombreCompleto}</td>
                    <td>{u.email}</td>
                    <td>{labelRol(u.rol)}</td>
                    <td>{u.area}</td>
                    <td>
                      {u.activo
                        ? <span className="badge badge-verde">Activo</span>
                        : <span className="badge badge-rojo">Inactivo</span>}
                    </td>
                    <td>
                        <button
                          className={u.activo ? 'btn-peligro' : 'btn-exito'}
                          onClick={() => handleEstado(u.idUsuario, u.activo)}
                        >
                          {u.activo
                            ? <><i className="bi bi-person-x"></i> Desactivar</>
                            : <><i className="bi bi-person-check"></i> Activar</>}
                        </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── Modal crear usuario ─────────────────────────────────────── */}
      {modalAbierto && (
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModal(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-person-plus-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Nuevo usuario
                </h5>
                <button type="button" className="btn-close" onClick={() => setModal(false)} aria-label="Cerrar"></button>
              </div>
              <form onSubmit={handleCrear}>
                <div className="modal-body">
                  <div className="form-grid">
                    <div className="campo-form">
                      <label>Nombre completo</label>
                      <input
                        required
                        value={form.NombreCompleto}
                        onChange={(e) => setForm({ ...form, NombreCompleto: e.target.value })}
                        placeholder="Ej. Ana García"
                      />
                    </div>
                    <div className="campo-form">
                      <label>Correo electrónico</label>
                      <input
                        type="email"
                        required
                        value={form.Email}
                        onChange={(e) => setForm({ ...form, Email: e.target.value })}
                        placeholder="ana@empresa.com"
                      />
                    </div>
                    <div className="campo-form">
                      <label>Contraseña</label>
                      <input
                        type="password"
                        required
                        value={form.Password}
                        onChange={(e) => setForm({ ...form, Password: e.target.value })}
                        placeholder="Mín. 12 caracteres, 1 mayúscula, 1 especial"
                      />
                    </div>
                    <div className="campo-form">
                      <label>Rol</label>
                      <select
                        value={form.IdRol}
                        onChange={(e) => setForm({ ...form, IdRol: e.target.value })}
                      >
                        <option value={2}>Supervisor</option>
                        <option value={3}>Operario</option>
                        <option value={4}>Revisor</option>
                        <option value={1}>Administrador</option>
                      </select>
                    </div>
                    <div className="campo-form">
                      <label>Área</label>
                      <select
                        required
                        value={form.IdArea}
                        onChange={(e) => setForm({ ...form, IdArea: e.target.value })}
                      >
                        <option value="">-- Selecciona un área --</option>
                        {areas.map((a) => (
                          <option key={a.id} value={a.id}>{a.nombre}</option>
                        ))}
                      </select>
                    </div>
                  </div>

                  {error && <div className="alerta-error" style={{ marginTop: '1rem' }}>{error}</div>}
                </div>

                <div className="modal-footer">
                  <button type="button" className="btn-secundario" onClick={() => setModal(false)}>
                    Cancelar
                  </button>
                  <button type="submit" className="btn-primario" disabled={enviando}>
                    {enviando ? 'Creando...' : 'Crear usuario'}
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