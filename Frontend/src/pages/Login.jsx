import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import api from '../api/axios'
import './Login.css'

export default function Login() {
  const navigate  = useNavigate()
  const { login } = useAuth()

  const [form, setForm]       = useState({ email: '', password: '' })
  const [error, setError]     = useState('')
  const [cargando, setCargando] = useState(false)

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value })
    setError('')
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setCargando(true)

    try {
      const respuesta = await api.post('/Usuarios/login', {
        Email:    form.email,
        Password: form.password,
      })

      // Guarda la sesión en el contexto y localStorage
      login(respuesta.data)

      // Redirige según el rol que devolvió el backend
      const rol = respuesta.data.rol
      if (rol === 1) navigate('/admin')
      else if (rol === 2) navigate('/supervisor')
      else if (rol === 3) navigate('/operario')
      else if (rol === 4) navigate('/revisor')
      else if (rol === 5) navigate('/superadmin')   // ← agregar esta línea
      else navigate('/login')
      
    } catch (err) {
      const mensaje = err.response?.data?.mensaje || 'Error al conectar con el servidor.'
      setError(mensaje)
    } finally {
      setCargando(false)
    }
  }

  return (
    <div className="login-fondo">
      <div className="login-card">

        <div className="login-header">
          <div className="login-icono">
            <i className="bi bi-file-earmark-check"></i>
          </div>
          <h1 className="login-titulo">QualityDoc</h1>
          <p className="login-subtitulo">Sistema de gestión documental</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">

          <div className="campo">
            <label htmlFor="email">
              <i className="bi bi-envelope"></i>
              Correo electrónico
            </label>
            <input
              id="email"
              type="email"
              name="email"
              value={form.email}
              onChange={handleChange}
              placeholder="usuario@empresa.com"
              required
              autoComplete="email"
            />
          </div>

          <div className="campo">
            <label htmlFor="password">
              <i className="bi bi-lock"></i>
              Contraseña
            </label>
            <input
              id="password"
              type="password"
              name="password"
              value={form.password}
              onChange={handleChange}
              placeholder="••••••••••••"
              required
              autoComplete="current-password"
            />
          </div>

          {error && (
            <div className="login-error">
              <i className="bi bi-exclamation-triangle-fill"></i>
              {error}
            </div>
          )}

          <button type="submit" className="login-btn" disabled={cargando}>
            {cargando
              ? <><span className="qd-spinner"></span> Iniciando sesión...</>
              : <><i className="bi bi-box-arrow-in-right"></i> Iniciar sesión</>
            }
          </button>

        </form>
      </div>
    </div>
  )
}