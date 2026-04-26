import { useAuth } from '../context/AuthContext'
import { useNavigate } from 'react-router-dom'

// PLACEHOLDER — este archivo se reemplazará con el dashboard completo.
export default function DashboardAdmin() {
  const { usuario, logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  return (
    <div style={{ padding: '2rem' }}>
      <h1>Dashboard Admin</h1>
      <p>Bienvenido, {usuario?.nombre}</p>
      <p>Área: {usuario?.area}</p>
      <button onClick={handleLogout} style={{ marginTop: '1rem' }}>
        Cerrar sesión
      </button>
    </div>
  )
}
