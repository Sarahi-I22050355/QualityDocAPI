import { useState, useEffect } from "react";
import api from "../../api/axios";
import "../../components/Seccion.css";

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
  const [modalDoc,   setModalDoc]     = useState(null);
  const [decision,   setDecision]     = useState("Aprobado");
  const [comentario, setComentario]   = useState("");
  const [enviando,   setEnviando]     = useState(false);
  const [mensaje,    setMensaje]      = useState("");

  // ── Cargar pendientes ──────────────────────────────────────────
  async function cargarPendientes() {
    setCargando(true);
    setError("");
    try {
      const res = await api.get("/Documentos/pendientes-revision");
      setPendientes(res.data.resultados || []);
    } catch (e) {
      setError(e.response?.data?.Mensaje || "No se pudieron cargar los documentos pendientes.");
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargarPendientes(); }, []);

  // ── Obtener idFlujo del documento (el último pendiente) ────────
  async function obtenerIdFlujo(idDocumento) {
    const res = await api.get(`/Documentos/${idDocumento}/flujo`);
    const historial = res.data.historial || [];
    const pendiente = historial.find(f => f.decision === "Pendiente");
    return pendiente?.idFlujo ?? null;
  }

  // ── Resolver aprobación ────────────────────────────────────────
  async function resolver() {
    if (!modalDoc) return;
    setEnviando(true);
    setMensaje("");
    try {
      const doc = modalDoc.documento ?? modalDoc.Documento;
      const idFlujo = await obtenerIdFlujo(doc?.sqlId ?? doc?.SqlId);
      if (!idFlujo) { setMensaje("⚠️ No se encontró la solicitud pendiente."); setEnviando(false); return; }

      await api.put(`/Documentos/resolver-aprobacion/${idFlujo}`, {
        Decision: decision,
        Comentarios: comentario,
      });

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
      const res = await api.get(`/Documentos/descargar/${idDocumento}`, { responseType: "blob" });
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
  if (cargando) return <p className="cargando-txt">Cargando pendientes…</p>;
  if (error)    return <div className="alerta-error">{error}</div>;

  return (
    <div>
      <div className="seccion-header">
        <h2 className="seccion-titulo">Documentos pendientes de revisión</h2>
      </div>

      {pendientes.length === 0 ? (
        <div className="card">
          <p className="sin-datos">🎉 No hay documentos pendientes de revisión en tu área.</p>
        </div>
      ) : (
        <div className="card">
          <div className="tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Título</th>
                  <th>Área</th>
                  <th>Ver.</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {pendientes.map((item, i) => {
                  const doc = item.documento ?? item.Documento;
                  const id  = doc?.sqlId ?? doc?.SqlId;
                  return (
                    <tr key={i}>
                      <td>
                        <div style={{ fontWeight: 500 }}>{doc?.titulo ?? doc?.Titulo ?? "Sin título"}</div>
                        <div style={{ fontSize: '0.8rem', color: '#9ca3af' }}>{doc?.categoria ?? doc?.Categoria ?? "—"}</div>
                        <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '4px' }}>
                          {(doc?.subidoPor ?? doc?.SubidoPor) && <span>Subido por: <strong>{doc?.subidoPor ?? doc?.SubidoPor}</strong></span>}
                          {(doc?.fechaSubida ?? doc?.FechaSubida) && <span> · {fmtFecha(doc?.fechaSubida ?? doc?.FechaSubida)}</span>}
                        </div>
                        {doc?.etiquetas?.length > 0 && (
                          <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap', marginTop: '4px' }}>
                            {doc.etiquetas.map((e, j) => (
                              <span key={j} style={{
                                background: '#eff6ff', color: '#1d4ed8', fontSize: '0.68rem',
                                padding: '1px 6px', borderRadius: '4px', border: '1px solid #bfdbfe'
                              }}>{e}</span>
                            ))}
                          </div>
                        )}
                      </td>
                      <td>{doc?.area ?? doc?.Area ?? "—"}</td>
                      <td>v{item.version ?? item.Version ?? "—"}</td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                          <button className="btn-secundario" onClick={() => descargar(id)}>
                            Descargar
                          </button>
                          <button className="btn-primario" onClick={() => setModalDoc(item)}>
                            Revisar
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Modal de resolución ── */}
      {modalDoc && (
        <div className="modal-fondo" onClick={() => setModalDoc(null)}>
          <div className="modal-card" style={{ maxWidth: '440px' }} onClick={e => e.stopPropagation()}>
            <h3 className="modal-titulo">Resolver aprobación</h3>
            <p style={{ fontSize: '0.85rem', color: '#6b7280', marginBottom: '1rem' }}>
              {(modalDoc.documento ?? modalDoc.Documento)?.titulo ?? (modalDoc.documento ?? modalDoc.Documento)?.Titulo ?? "Documento"}
            </p>

            <div className="form-grid una-col">
              <div className="campo-form">
                <label>Decisión</label>
                <select value={decision} onChange={(e) => setDecision(e.target.value)}>
                  <option value="Aprobado">✅ Aprobar</option>
                  <option value="Rechazado">❌ Rechazar</option>
                </select>
              </div>
              <div className="campo-form">
                <label>Comentarios {decision === 'Rechazado' ? '*' : '(opcional)'}</label>
                <textarea
                  rows={4}
                  placeholder="Escribe observaciones o motivo de rechazo…"
                  value={comentario}
                  onChange={e => setComentario(e.target.value)}
                  required={decision === 'Rechazado'}
                />
              </div>
            </div>

            {mensaje && <div className={mensaje.startsWith("✅") ? "alerta-ok" : "alerta-error"} style={{ marginTop: '1rem' }}>{mensaje}</div>}

            <div className="modal-acciones">
              <button className="btn-secundario" onClick={() => setModalDoc(null)} disabled={enviando}>
                Cancelar
              </button>
              <button
                className={decision === "Rechazado" ? "btn-peligro" : "btn-primario"}
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