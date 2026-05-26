import { useState, useEffect } from "react";
import axios from "axios";

const API = import.meta.env.VITE_API_URL || "http://localhost:5000";

function token() {
  return localStorage.getItem("token");
}

function headers() {
  return { Authorization: `Bearer ${token()}` };
}

// Formatea una fecha ISO a dd/mm/aaaa hh:mm
function fmtFecha(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleDateString("es-MX", { day: "2-digit", month: "2-digit", year: "numeric" })
    + " " + d.toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" });
}

export default function SeccionDocumentosRevisor() {
  const [pendientes, setPendientes]   = useState([]);
  const [cargando,   setCargando]     = useState(true);
  const [error,      setError]        = useState("");
  const [modalDoc,   setModalDoc]     = useState(null);   // doc seleccionado para resolver
  const [decision,   setDecision]     = useState("Aprobado");
  const [comentario, setComentario]   = useState("");
  const [enviando,   setEnviando]     = useState(false);
  const [mensaje,    setMensaje]      = useState("");

  // ── Cargar pendientes ──────────────────────────────────────────
  async function cargarPendientes() {
    setCargando(true);
    setError("");
    try {
      const res = await axios.get(`${API}/api/Documentos/pendientes-revision`, { headers: headers() });
      setPendientes(res.data.Resultados || []);
    } catch (e) {
      setError(e.response?.data?.Mensaje || "No se pudieron cargar los documentos pendientes.");
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargarPendientes(); }, []);

  // ── Obtener idFlujo del documento (el último pendiente) ────────
  async function obtenerIdFlujo(idDocumento) {
    const res = await axios.get(`${API}/api/Documentos/${idDocumento}/flujo`, { headers: headers() });
    const historial = res.data.Historial || [];
    const pendiente = historial.find(f => f.Decision === "Pendiente");
    return pendiente?.IdFlujo ?? null;
  }

  // ── Resolver aprobación ────────────────────────────────────────
  async function resolver() {
    if (!modalDoc) return;
    setEnviando(true);
    setMensaje("");
    try {
      const idFlujo = await obtenerIdFlujo(modalDoc.Documento?.sqlId ?? modalDoc.Documento?.SqlId);
      if (!idFlujo) { setMensaje("⚠️ No se encontró la solicitud pendiente."); return; }

      await axios.put(
        `${API}/api/Documentos/resolver-aprobacion/${idFlujo}`,
        { Decision: decision, Comentarios: comentario },
        { headers: headers() }
      );

      setMensaje(`✅ Documento ${decision.toLowerCase()} correctamente.`);
      setTimeout(() => {
        setModalDoc(null);
        setDecision("Aprobado");
        setComentario("");
        setMensaje("");
        cargarPendientes();
      }, 1500);
    } catch (e) {
      setMensaje("❌ " + (e.response?.data?.Mensaje || "Error al resolver la aprobación."));
    } finally {
      setEnviando(false);
    }
  }

  // ── Descargar documento ────────────────────────────────────────
  async function descargar(idDocumento) {
    try {
      const res = await axios.get(`${API}/api/Documentos/descargar/${idDocumento}`, {
        headers: headers(),
        responseType: "blob",
      });
      const url  = URL.createObjectURL(res.data);
      const link = document.createElement("a");
      link.href  = url;
      link.download = `documento_${idDocumento}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      alert("No se pudo descargar el documento.");
    }
  }

  // ── Render ─────────────────────────────────────────────────────
  if (cargando) return <div className="estado-vacio">Cargando pendientes…</div>;
  if (error)    return <div className="estado-error">{error}</div>;

  return (
    <div className="seccion-revisor">
      {pendientes.length === 0 ? (
        <div className="estado-vacio">
          <span className="estado-icono">🎉</span>
          <p>No hay documentos pendientes de revisión en tu área.</p>
        </div>
      ) : (
        <div className="lista-pendientes">
          {pendientes.map((item, i) => {
            const doc = item.Documento;
            const id  = doc?.sqlId ?? doc?.SqlId;
            return (
              <div key={i} className="card-pendiente">
                {/* Encabezado */}
                <div className="card-header-pendiente">
                  <div>
                    <h3 className="card-titulo">{doc?.titulo ?? doc?.Titulo ?? "Sin título"}</h3>
                    <span className="card-area">{doc?.area ?? doc?.Area ?? "—"}</span>
                  </div>
                  <div className="card-badges">
                    <span className="badge-version">v{item.Version}</span>
                    <span className="badge-pendiente">Pendiente</span>
                  </div>
                </div>

                {/* Meta */}
                <div className="card-meta-grid">
                  <div className="meta-item">
                    <span className="meta-label">Categoría</span>
                    <span className="meta-valor">{doc?.categoria ?? doc?.Categoria ?? "—"}</span>
                  </div>
                  <div className="meta-item">
                    <span className="meta-label">Subido por</span>
                    <span className="meta-valor">{doc?.subidoPor ?? doc?.SubidoPor ?? doc?.autor ?? doc?.Autor ?? "—"}</span>
                  </div>
                  <div className="meta-item">
                    <span className="meta-label">Fecha de subida</span>
                    <span className="meta-valor">{fmtFecha(doc?.fechaSubida ?? doc?.FechaSubida)}</span>
                  </div>
                  {doc?.etiquetas?.length > 0 && (
                    <div className="meta-item meta-wide">
                      <span className="meta-label">Etiquetas</span>
                      <div className="etiquetas-row">
                        {doc.etiquetas.map((e, j) => <span key={j} className="etiqueta-chip">{e}</span>)}
                      </div>
                    </div>
                  )}
                </div>

                {/* Acciones */}
                <div className="card-acciones">
                  <button className="btn-secondary" onClick={() => descargar(id)}>
                    ⬇ Descargar
                  </button>
                  <button className="btn-primary" onClick={() => setModalDoc(item)}>
                    Revisar
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ── Modal de resolución ── */}
      {modalDoc && (
        <div className="modal-overlay" onClick={() => setModalDoc(null)}>
          <div className="modal-box" onClick={e => e.stopPropagation()}>
            <h2 className="modal-titulo">Resolver aprobación</h2>
            <p className="modal-subtitulo">
              {modalDoc.Documento?.titulo ?? modalDoc.Documento?.Titulo ?? "Documento"}
            </p>

            <div className="form-group">
              <label className="form-label">Decisión</label>
              <div className="radio-group">
                <label className={`radio-option ${decision === "Aprobado"  ? "selected" : ""}`}>
                  <input type="radio" value="Aprobado"  checked={decision === "Aprobado"}  onChange={() => setDecision("Aprobado")} />
                  ✅ Aprobar
                </label>
                <label className={`radio-option ${decision === "Rechazado" ? "selected" : ""}`}>
                  <input type="radio" value="Rechazado" checked={decision === "Rechazado"} onChange={() => setDecision("Rechazado")} />
                  ❌ Rechazar
                </label>
              </div>
            </div>

            <div className="form-group">
              <label className="form-label">Comentarios <span className="form-optional">(opcional)</span></label>
              <textarea
                className="form-textarea"
                rows={4}
                placeholder="Escribe observaciones o motivo de rechazo…"
                value={comentario}
                onChange={e => setComentario(e.target.value)}
              />
            </div>

            {mensaje && <div className={`form-mensaje ${mensaje.startsWith("✅") ? "ok" : "error"}`}>{mensaje}</div>}

            <div className="modal-acciones">
              <button className="btn-secondary" onClick={() => setModalDoc(null)} disabled={enviando}>
                Cancelar
              </button>
              <button
                className={`btn-primary ${decision === "Rechazado" ? "btn-danger" : ""}`}
                onClick={resolver}
                disabled={enviando}
              >
                {enviando ? "Guardando…" : decision === "Aprobado" ? "Aprobar" : "Rechazar"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
