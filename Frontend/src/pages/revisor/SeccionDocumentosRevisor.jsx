import { useState, useEffect } from "react";
import api from "../../api/axios";
import "../../components/Seccion.css";

function fmtFecha(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleDateString("es-MX", { day: "2-digit", month: "2-digit", year: "numeric" })
    + " " + d.toLocaleTimeString("es-MX", { hour: "2-digit", minute: "2-digit" });
}

function BarraFirmas({ requeridas, obtenidas }) {
  if (!requeridas || requeridas === 0) return null;
  const pct = Math.round((obtenidas / requeridas) * 100);
  return (
    <div style={{ marginTop: "6px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.72rem", color: "#6b7280", marginBottom: "3px" }}>
        <span>Firmas</span>
        <span style={{ fontWeight: 600, color: obtenidas === requeridas ? "#4ade80" : "#fbbf24" }}>
          {obtenidas} / {requeridas}
        </span>
      </div>
      <div style={{ height: "4px", background: "#2a3347", borderRadius: "99px", overflow: "hidden" }}>
        <div style={{
          height: "4px", width: `${pct}%`,
          background: obtenidas === requeridas ? "#4ade80" : "#fbbf24",
          borderRadius: "99px", transition: "width 0.3s"
        }} />
      </div>
    </div>
  );
}

export default function SeccionDocumentosRevisor() {
  const [pendientes, setPendientes] = useState([]);
  const [cargando,   setCargando]   = useState(true);
  const [error,      setError]      = useState("");
  const [modalDoc,   setModalDoc]   = useState(null);
  const [decision,   setDecision]   = useState("Aprobado");
  const [comentario, setComentario] = useState("");
  const [enviando,   setEnviando]   = useState(false);
  const [mensaje,    setMensaje]    = useState("");

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

  useEffect(() => {
    cargarPendientes();
    const interval = setInterval(cargarPendientes, 60000);
    return () => clearInterval(interval);
  }, []);

  async function resolver() {
    if (!modalDoc) return;
    setEnviando(true);
    setMensaje("");
    try {
      // El backend devuelve idFlujoParaResolver directamente — sin roundtrip extra
      const idFlujo = modalDoc.idFlujoParaResolver;
      if (!idFlujo) {
        setMensaje("⚠️ No se encontró la solicitud pendiente para tu área.");
        setEnviando(false);
        return;
      }

      const res = await api.put(`/Documentos/resolver-aprobacion/${idFlujo}`, {
        Decision: decision,
        Comentarios: comentario,
      });

      setMensaje(`✅ ${res.data?.Mensaje || `Documento ${decision.toLowerCase()} correctamente.`}`);
      setTimeout(() => {
        setModalDoc(null);
        setDecision("Aprobado");
        setComentario("");
        setMensaje("");
        cargarPendientes();
      }, 1800);
    } catch (e) {
      setMensaje("❌ " + (e.response?.data?.Mensaje || "Error al resolver la aprobación."));
    } finally {
      setEnviando(false);
    }
  }

  async function descargar(idDocumento) {
    try {
      const res = await api.get(`/Documentos/descargar/${idDocumento}`, { responseType: "blob" });
      const url  = URL.createObjectURL(res.data);
      const link = document.createElement("a");
      link.href     = url;
      link.download = `documento_${idDocumento}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      alert("No se pudo descargar el documento.");
    }
  }

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
                  <th>Firmas</th>
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
                        <div style={{ fontSize: "0.8rem", color: "#9ca3af" }}>{doc?.categoria ?? doc?.Categoria ?? "—"}</div>
                        <div style={{ fontSize: "0.75rem", color: "#6b7280", marginTop: "4px" }}>
                          {(doc?.subidoPor ?? doc?.SubidoPor) && (
                            <span>Subido por: <strong>{doc?.subidoPor ?? doc?.SubidoPor}</strong></span>
                          )}
                          {(doc?.fechaSubida ?? doc?.FechaSubida) && (
                            <span> · {fmtFecha(doc?.fechaSubida ?? doc?.FechaSubida)}</span>
                          )}
                        </div>
                        {doc?.etiquetas?.length > 0 && (
                          <div style={{ display: "flex", gap: "4px", flexWrap: "wrap", marginTop: "4px" }}>
                            {doc.etiquetas.map((e, j) => (
                              <span key={j} style={{
                                background: "#eff6ff", color: "#1d4ed8", fontSize: "0.68rem",
                                padding: "1px 6px", borderRadius: "4px", border: "1px solid #bfdbfe"
                              }}>{e}</span>
                            ))}
                          </div>
                        )}
                      </td>
                      <td>{doc?.area ?? doc?.Area ?? "—"}</td>
                      <td>v{item.version ?? item.Version ?? "—"}</td>
                      <td style={{ minWidth: "110px" }}>
                        <BarraFirmas requeridas={item.firmasReq} obtenidas={item.firmasOk} />
                      </td>
                      <td>
                        <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
                          <button className="btn-secundario" onClick={() => descargar(id)}>
                            <i className="bi bi-download"></i> Descargar
                          </button>
                          <button className="btn-primario" onClick={() => {
                            setModalDoc(item);
                            setDecision("Aprobado");
                            setComentario("");
                            setMensaje("");
                          }}>
                            <i className="bi bi-eye"></i> Revisar
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
        <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.65)', backdropFilter: 'blur(4px)' }} tabIndex="-1" onClick={() => setModalDoc(null)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">
                  <i className="bi bi-check-square-fill" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
                  Resolver aprobación
                </h5>
                <button type="button" className="btn-close" onClick={() => setModalDoc(null)} aria-label="Cerrar"></button>
              </div>
              <div className="modal-body">
                <p style={{ fontSize: "0.85rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                  {(modalDoc.documento ?? modalDoc.Documento)?.titulo ?? "Documento"}
                </p>

                {/* Progreso de firmas en el modal */}
                {modalDoc.firmasReq > 0 && (
                  <div style={{
                    background: "rgba(79,142,247,0.08)", border: "1px solid rgba(79,142,247,0.2)",
                    borderRadius: "8px", padding: "10px 12px", marginBottom: "1rem"
                  }}>
                    <BarraFirmas requeridas={modalDoc.firmasReq} obtenidas={modalDoc.firmasOk} />
                    <p style={{ fontSize: "0.75rem", color: "#6b7280", marginTop: "6px" }}>
                      {modalDoc.firmasReq - modalDoc.firmasOk === 1
                        ? "Esta es la última firma requerida."
                        : `Faltarán ${modalDoc.firmasReq - modalDoc.firmasOk - 1} firma(s) más después de la tuya.`}
                    </p>
                  </div>
                )}

                <div className="form-grid una-col">
                  <div className="campo-form">
                    <label>Decisión</label>
                    <select value={decision} onChange={(e) => setDecision(e.target.value)}>
                      <option value="Aprobado">✅ Aprobar</option>
                      <option value="Rechazado">❌ Rechazar</option>
                    </select>
                  </div>
                  <div className="campo-form">
                    <label>Comentarios {decision === "Rechazado" ? "*" : "(opcional)"}</label>
                    <textarea
                      rows={4}
                      placeholder="Escribe observaciones o motivo de rechazo…"
                      value={comentario}
                      onChange={e => setComentario(e.target.value)}
                      required={decision === "Rechazado"}
                    />
                  </div>
                </div>

                {mensaje && (
                  <div className={mensaje.startsWith("✅") || mensaje.includes("✅") ? "alerta-ok" : "alerta-error"}
                    style={{ marginTop: "1rem" }}>
                    {mensaje}
                  </div>
                )}
              </div>

              <div className="modal-footer">
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
        </div>
      )}
    </div>
  );
}