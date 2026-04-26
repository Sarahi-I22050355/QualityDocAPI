import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import './Layout.css'

// Componente de layout reutilizable para Admin, Supervisor y Operario.
// Recibe: titulo (nombre del sistema), seccionActiva, setSección,
// y el arreglo de secciones disponibles según el rol.
export default function Layout({ titulo, secciones, seccionActiva, setSeccion, children }) {
  const { usuario, logout } = useAuth()
  const navigate            = useNavigate()
  const [menuAbierto, setMenuAbierto] = useState(false)

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  return (
    <div className="layout">

      {/* ── Barra superior ─────────────────────────────────────────── */}
      <header className="layout-header">
        <div className="header-izq">
          <button
            className="menu-toggle"
            onClick={() => setMenuAbierto(!menuAbierto)}
            aria-label="Abrir menú"
          >
            ☰
          </button>
          <span className="header-titulo">{titulo}</span>
        </div>
        <div className="header-der">
          <span className="header-usuario">
            {usuario?.nombre}
            <small>{usuario?.area}</small>
          </span>
          <button className="btn-logout" onClick={handleLogout}>
            Cerrar sesión
          </button>
        </div>
      </header>

      <div className="layout-cuerpo">

        {/* ── Menú lateral ───────────────────────────────────────────── */}
        <aside className={`layout-sidebar ${menuAbierto ? 'abierto' : ''}`}>
          <nav>
            {secciones.map((sec) => (
              <button
                key={sec.id}
                className={`sidebar-item ${seccionActiva === sec.id ? 'activo' : ''}`}
                onClick={() => {
                  setSeccion(sec.id)
                  setMenuAbierto(false)
                }}
              >
                <span className="sidebar-icono">{sec.icono}</span>
                <span className="sidebar-label">{sec.label}</span>
              </button>
            ))}
          </nav>
        </aside>

        {/* ── Contenido principal ────────────────────────────────────── */}
        <main className="layout-main">
          {children}
        </main>

      </div>

      {/* Overlay para cerrar menú en móvil */}
      {menuAbierto && (
        <div className="sidebar-overlay" onClick={() => setMenuAbierto(false)} />
      )}
    </div>
  )
}
