import { createContext, useContext, useState, useEffect } from "react";

const AuthContext = createContext();

const CLAIM_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

function normalizarPayload(payload) {
  return {
    ...payload,
    nombre:  payload[CLAIM_NAME]        ?? "",
    area:    payload["nombre_area"]     ?? "",
    empresa: payload["nombre_empresa"]  ?? "",
  };
}

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split(".")[1]));
        setUsuario(normalizarPayload(payload));
      } catch {
        localStorage.removeItem("token");
      }
    }
    setCargando(false);
  }, []);

  const login = (data) => {
    const token = data.token || data;
    localStorage.setItem("token", token);
    const payload = JSON.parse(atob(token.split(".")[1]));
    setUsuario(normalizarPayload(payload));
  };

  const logout = () => {
    localStorage.removeItem("token");
    setUsuario(null);
  };

  // ── Helpers de rol ──────────────────────────────────────────────
  const esAdmin      = () => usuario?.idRol === 1 || usuario?.idRol === "1";
  const esSupervisor = () => usuario?.idRol === 2 || usuario?.idRol === "2";
  const esOperario   = () => usuario?.idRol === 3 || usuario?.idRol === "3";
  const esRevisor    = () => usuario?.idRol === 4 || usuario?.idRol === "4";
  const esSuperAdmin = () => usuario?.idRol === 5 || usuario?.idRol === "5";

  return (
    <AuthContext.Provider value={{
      usuario,
      login,
      logout,
      cargando,
      esAdmin,
      esSupervisor,
      esOperario,
      esRevisor,
      esSuperAdmin,
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}