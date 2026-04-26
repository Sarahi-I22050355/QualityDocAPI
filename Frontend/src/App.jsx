import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from './context/AuthContext'
import RutaProtegida from './components/RutaProtegida'
import Login from './pages/Login'

// Los dashboards se importan aquí cuando los vayamos creando.
// Por ahora apuntan a páginas placeholder.
import DashboardAdmin      from './pages/DashboardAdmin'
import DashboardSupervisor from './pages/DashboardSupervisor'
import DashboardOperario   from './pages/DashboardOperario'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>

          {/* Ruta pública — solo accesible sin sesión */}
          <Route path="/login" element={<Login />} />

          {/* Ruta raíz → manda al login por defecto */}
          <Route path="/" element={<Navigate to="/login" replace />} />

          {/* ── Rutas protegidas por rol ───────────────────────────── */}

          <Route
            path="/admin/*"
            element={
              <RutaProtegida rol={1}>
                <DashboardAdmin />
              </RutaProtegida>
            }
          />

          <Route
            path="/supervisor/*"
            element={
              <RutaProtegida rol={2}>
                <DashboardSupervisor />
              </RutaProtegida>
            }
          />

          <Route
            path="/operario/*"
            element={
              <RutaProtegida rol={3}>
                <DashboardOperario />
              </RutaProtegida>
            }
          />

          {/* Cualquier ruta desconocida → login */}
          <Route path="*" element={<Navigate to="/login" replace />} />

        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
