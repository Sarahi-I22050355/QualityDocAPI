import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

// Envuelve cualquier página que requiera estar autenticado.
// Si no hay sesión activa → redirige al login.
// Si hay sesión y se pide un rol específico → verifica que coincida.
// Mientras se está comprobando la sesión → muestra un loader simple.
//
// Uso en App.jsx:
//   <RutaProtegida>           ← solo requiere login
//   <RutaProtegida rol={1}>   ← solo Admin
//   <RutaProtegida rol={2}>   ← solo Supervisor

export default function RutaProtegida({ children, rol }) {
  const { usuario, cargando } = useAuth()

  if (cargando) {
    return (
      <div className="loader-pantalla">
        <p>Cargando...</p>
      </div>
    )
  }

  // Sin sesión → al login
  if (!usuario) {
    return <Navigate to="/login" replace />
  }

  // Se pidió un rol específico y el usuario no lo tiene → al login
  if (rol !== undefined && usuario.rol !== rol) {
    return <Navigate to="/login" replace />
  }

  return children
}