import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import Login from "./pages/Login";
import DashboardAdmin from "./pages/DashboardAdmin";
import DashboardSupervisor from "./pages/DashboardSupervisor";
import DashboardOperario from "./pages/DashboardOperario";
import DashboardRevisor from "./pages/revisor/DashboardRevisor";

// Ruta protegida que verifica el rol
function RutaProtegida({ children, rol }) {
  const { usuario, cargando } = useAuth();

  if (cargando) return <div className="loading-screen">Cargando...</div>;
  if (!usuario) return <Navigate to="/login" replace />;

  const rolNum = Number(usuario.idRol);
  if (rol && rolNum !== Number(rol)) return <Navigate to="/login" replace />;

  return children;
}

// Redirige al dashboard según el rol del usuario
function RedirigirPorRol() {
  const { usuario } = useAuth();
  const rolNum = Number(usuario?.idRol);

  if (rolNum === 1) return <Navigate to="/admin" replace />;
  if (rolNum === 2) return <Navigate to="/supervisor" replace />;
  if (rolNum === 3) return <Navigate to="/operario" replace />;
  if (rolNum === 4) return <Navigate to="/revisor" replace />;

  return <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/"     element={<RedirigirPorRol />} />

          {/* Admin */}
          <Route path="/admin/*" element={
            <RutaProtegida rol={1}>
              <DashboardAdmin />
            </RutaProtegida>
          } />

          {/* Supervisor */}
          <Route path="/supervisor/*" element={
            <RutaProtegida rol={2}>
              <DashboardSupervisor />
            </RutaProtegida>
          } />

          {/* Operario */}
          <Route path="/operario/*" element={
            <RutaProtegida rol={3}>
              <DashboardOperario />
            </RutaProtegida>
          } />

          {/* Revisor — nuevo */}
          <Route path="/revisor/*" element={
            <RutaProtegida rol={4}>
              <DashboardRevisor />
            </RutaProtegida>
          } />

          {/* Fallback */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
