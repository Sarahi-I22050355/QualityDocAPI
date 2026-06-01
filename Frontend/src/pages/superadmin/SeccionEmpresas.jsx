import { useState, useEffect } from 'react'
import api from '../../api/axios'
import '../../components/Seccion.css'

export default function SeccionEmpresas() {
  const [empresas, setEmpresas]         = useState([])
  const [cargando, setCargando]         = useState(true)
  const [error, setError]               = useState('')
  const [ok, setOk]                     = useState('')

  // Modal crear empresa
  const [modalEmpresa, setModalEmpresa] = useState(false)
  const [formEmpresa, setFormEmpresa]   = useState({ Nombre: '', Rfc: '' })
  const [enviandoEmp, setEnviandoEmp]   = useState(false)
  const [errorEmp, setErrorEmp]         = useState('')

  // Modal crear admin de empresa
  const [modalAdmin, setModalAdmin]     = useState(false)
  const [empresaSel, setEmpresaSel]     = useState(null)
  const [formAdmin, setFormAdmin]       = useState({ NombreCompleto: '', Email: '', Password: '' })
  const [enviandoAdm, setEnviandoAdm]   = useState(false)
  const [errorAdm, setErrorAdm]         = useState('')

  const mostrarOk    = (msg) => { setOk(msg);    setTimeout(() => setOk(''),    3500) }
  const mostrarError = (msg) => { setError(msg); setTimeout(() => setError(''), 4500) }

  // ── Cargar empresas ────────────────────────────────────────────
  const cargarEmpresas = async () => {
    setCargando(true)
    try {
      const r = await api.get('/Empresas')
      setEmpresas(r.data.empresas || [])
    } catch (e) {
      mostrarError(e.response?.data?.Error || 'Error al cargar empresas.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => { cargarEmpresas() }, [])

  // ── Crear empresa ──────────────────────────────────────────────
  const handleCrearEmpresa = async (e) => {
    e.preventDefault()
    setEnviandoEmp(true)
    setErrorEmp('')
    try {
      await api.post('/Empresas', formEmpresa)
      mostrarOk(`Empresa "${formEmpresa.Nombre}" creada exitosamente.`)
      setModalEmpresa(false)
      setFormEmpresa({ Nombre: '', Rfc: '' })
      cargarEmpresas()
    } catch (e) {
      setErrorEmp(e.response?.data?.Mensaje || 'Error al crear la empresa.')
    } finally {
      setEnviandoEmp(false)
    }
  }

  // ── Desactivar empresa ─────────────────────────────────────────
  const handleDesactivar = async (empresa) => {
    if (!confirm(`¿Desactivar la empresa "${empresa.nombre}"? Sus usuarios ya no podrán acceder.`)) return
    try {
      await api.put(`/Empresas/${empresa.id}/desactivar`)
      mostrarOk(`Empresa "${empresa.nombre}" desactivada.`)
      cargarEmpresas()
    } catch (e) {
      mostrarError(e.response?.data?.Mensaje || 'Error al desactivar.')
    }
  }

  // ── Crear admin de empresa ─────────────────────────────────────
  const abrirModalAdmin = (empresa) => {
    setEmpresaSel(empresa)
    setFormAdmin({ NombreCompleto: '', Email: '', Password: '' })
    setErrorAdm('')
    setModalAdmin(true)
  }

  const handleCrearAdmin = async (e) => {
    e.preventDefault()
    setEnviandoAdm(true)
    setErrorAdm('')
    try {
      await api.post(`/Empresas/${empresaSel.id}/admin`, formAdmin)
      mostrarOk(`Admin creado para "${empresaSel.nombre}". Se generó el área General automáticamente.`)
      setModalAdmin(false)
    } catch (e) {
      setErrorAdm(e.response?.data?.Mensaje || 'Error al crear el administrador.')
    } finally {
      setEnviandoAdm(false)
    }
  }

  const formatFecha = (f) => f
    ? new Date(f).toLocaleDateString('es-MX', { day: '2-digit', month: '2-digit', year: 'numeric' })
    : '—'

  return (
    <div>
      {/* ── Encabezado ── */}
      <div className="seccion-header">
        <h2 className="seccion-titulo">Empresas registradas</h2>
        <button className="btn-primario" onClick={() => {
          setFormEmpresa({ Nombre: '', Rfc: '' })
          setErrorEmp('')
          setModalEmpresa(true)
        }}>
          + Nueva empresa
        </button>
      </div>

      {error && <div className="alerta-error">{error}</div>}
      {ok    && <div className="alerta-ok">{ok}</div>}

      {/* ── Info de rol ── */}
      <div className="card" style={{ marginBottom: '1rem', background: 'rgba(167,139,250,0.08)', border: '1px solid rgba(167,139,250,0.25)' }}>
        <p style={{ fontSize: '0.875rem', color: '#a78bfa' }}>
          <strong>Panel SuperAdmin</strong> — Aquí puedes crear empresas, asignarles un administrador
          y desactivarlas. Cada empresa es un entorno completamente aislado.
        </p>
      </div>

      {/* ── Tabla de empresas ── */}
      <div className="card">
        {cargando ? (
          <p className="cargando-txt">Cargando empresas...</p>
        ) : (
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Nombre</th>
                  <th>RFC</th>
                  <th>Estado</th>
                  <th>Creada</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {empresas.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="sin-datos">
                      No hay empresas registradas. Crea la primera con el botón de arriba.
                    </td>
                  </tr>
                ) : empresas.map((emp) => (
                  <tr key={emp.id}>
                    <td style={{ color: 'var(--text-tertiary)', fontFamily: 'monospace' }}>#{emp.id}</td>
                    <td><strong>{emp.nombre}</strong></td>
                    <td>{emp.rfc || <span style={{ color: 'var(--text-tertiary)' }}>—</span>}</td>
                    <td>
                      {emp.activo
                        ? <span className="badge badge-verde">Activa</span>
                        : <span className="badge badge-rojo">Inactiva</span>}
                    </td>
                    <td>{formatFecha(emp.fechaCreacion)}</td>
                    <td>
                      <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                        {emp.activo && (
                          <>
                            <button
                              className="btn-secundario"
                              onClick={() => abrirModalAdmin(emp)}
                              title="Crear administrador para esta empresa"
                            >
                              + Admin
                            </button>
                            <button
                              className="btn-peligro"
                              onClick={() => handleDesactivar(emp)}
                            >
                              Desactivar
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── Modal crear empresa ── */}
      {modalEmpresa && (
        <div className="modal-fondo" onClick={() => setModalEmpresa(false)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Nueva empresa</h3>
            <form onSubmit={handleCrearEmpresa}>
              <div className="form-grid una-col">
                <div className="campo-form">
                  <label>Nombre de la empresa *</label>
                  <input
                    required
                    value={formEmpresa.Nombre}
                    onChange={(e) => setFormEmpresa({ ...formEmpresa, Nombre: e.target.value })}
                    placeholder="Ej. Grupo Industrial Monclova"
                  />
                </div>
                <div className="campo-form">
                  <label>RFC <span style={{ fontWeight: 400, color: 'var(--text-tertiary)' }}>(opcional)</span></label>
                  <input
                    value={formEmpresa.Rfc}
                    onChange={(e) => setFormEmpresa({ ...formEmpresa, Rfc: e.target.value })}
                    placeholder="Ej. GIM850101ABC"
                  />
                </div>
              </div>

              {errorEmp && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorEmp}</div>}

              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalEmpresa(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn-primario" disabled={enviandoEmp}>
                  {enviandoEmp ? 'Creando...' : 'Crear empresa'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal crear admin ── */}
      {modalAdmin && (
        <div className="modal-fondo" onClick={() => setModalAdmin(false)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <h3 className="modal-titulo">Crear administrador</h3>
            <p style={{ fontSize: '0.875rem', color: 'var(--text-secondary)', marginBottom: '1.25rem' }}>
              Empresa: <strong style={{ color: 'var(--text-primary)' }}>{empresaSel?.nombre}</strong>
              <br />
              <span style={{ fontSize: '0.8rem', color: '#a78bfa' }}>
                Se creará también el área General de esta empresa automáticamente.
              </span>
            </p>
            <form onSubmit={handleCrearAdmin}>
              <div className="form-grid una-col">
                <div className="campo-form">
                  <label>Nombre completo *</label>
                  <input
                    required
                    value={formAdmin.NombreCompleto}
                    onChange={(e) => setFormAdmin({ ...formAdmin, NombreCompleto: e.target.value })}
                    placeholder="Ej. María López"
                  />
                </div>
                <div className="campo-form">
                  <label>Correo electrónico *</label>
                  <input
                    type="email"
                    required
                    value={formAdmin.Email}
                    onChange={(e) => setFormAdmin({ ...formAdmin, Email: e.target.value })}
                    placeholder="admin@empresa.com"
                  />
                </div>
                <div className="campo-form">
                  <label>Contraseña *</label>
                  <input
                    type="password"
                    required
                    value={formAdmin.Password}
                    onChange={(e) => setFormAdmin({ ...formAdmin, Password: e.target.value })}
                    placeholder="Mín. 12 caracteres, 1 mayúscula, 1 especial"
                  />
                  <span style={{ fontSize: '0.75rem', color: 'var(--text-tertiary)', fontStyle: 'italic' }}>
                    Comparte esta contraseña con el administrador de la empresa.
                  </span>
                </div>
              </div>

              {errorAdm && <div className="alerta-error" style={{ marginTop: '1rem' }}>{errorAdm}</div>}

              <div className="modal-acciones">
                <button type="button" className="btn-secundario" onClick={() => setModalAdmin(false)}>
                  Cancelar
                </button>
                <button type="submit" className="btn-primario" disabled={enviandoAdm}>
                  {enviandoAdm ? 'Creando...' : 'Crear administrador'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}