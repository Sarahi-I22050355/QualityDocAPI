import { createContext, useContext, useState, useEffect } from "react";

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split(".")[1]));
        setUsuario(payload);
      } catch {
        localStorage.removeItem("token");
      }
    }
    setCargando(false);
  }, []);

  const login = (data) => {
    // Acepta el objeto completo { mensaje, usuario, rol, token, ... }
    // o solo el string del token
    const token = data.token || data;
    localStorage.setItem("token", token);
    const payload = JSON.parse(atob(token.split(".")[1]));
    setUsuario(payload);
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

  return (
    <AuthContext.Provider value={{ usuario, login, logout, cargando, esAdmin, esSupervisor, esOperario, esRevisor }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}