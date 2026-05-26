import { useState } from "react";
import { useAuth } from "../../context/AuthContext";
import SeccionDocumentosRevisor from "./SeccionDocumentosRevisor";
import "../../index.css";

export default function DashboardRevisor() {
  const { usuario, logout } = useAuth();
  const [seccionActiva, setSeccionActiva] = useState("pendientes");

  const nombre = usuario?.nombre_completo || usuario?.unique_name || "Revisor";
  const area   = usuario?.nombre_area    || "—";

  return (
    <div className="dashboard-layout">
      {/* ── Sidebar ── */}
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-icon">✦</span>
          <span className="brand-name">QualityDoc</span>
        </div>

        <div className="sidebar-user">
          <div className="user-avatar">{nombre.charAt(0).toUpperCase()}</div>
          <div className="user-info">
            <span className="user-name">{nombre}</span>
            <span className="user-role">Revisor · {area}</span>
          </div>
        </div>

        <nav className="sidebar-nav">
          <button
            className={`nav-item ${seccionActiva === "pendientes" ? "active" : ""}`}
            onClick={() => setSeccionActiva("pendientes")}
          >
            <span className="nav-icon">📋</span>
            <span>Pendientes de revisión</span>
          </button>
        </nav>

        <div className="sidebar-footer">
          <button className="btn-logout" onClick={logout}>
            <span>⎋</span> Cerrar sesión
          </button>
        </div>
      </aside>

      {/* ── Contenido principal ── */}
      <main className="main-content">
        <header className="main-header">
          <h1 className="page-title">
            {seccionActiva === "pendientes" && "Documentos pendientes de revisión"}
          </h1>
          <span className="header-badge revisor-badge">Revisor</span>
        </header>

        <div className="content-body">
          {seccionActiva === "pendientes" && <SeccionDocumentosRevisor />}
        </div>
      </main>
    </div>
  );
}
