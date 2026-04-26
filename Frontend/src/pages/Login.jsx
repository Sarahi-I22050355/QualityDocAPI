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
    setError('') // limpia el error al escribir
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
          <h1 className="login-titulo">QualityDoc</h1>
          <p className="login-subtitulo">Sistema de gestión documental</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">

          <div className="campo">
            <label htmlFor="email">Correo electrónico</label>
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
            <label htmlFor="password">Contraseña</label>
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
              {error}
            </div>
          )}

          <button type="submit" className="login-btn" disabled={cargando}>
            {cargando ? 'Iniciando sesión...' : 'Iniciar sesión'}
          </button>

        </form>
      </div>
    </div>
  )
}
