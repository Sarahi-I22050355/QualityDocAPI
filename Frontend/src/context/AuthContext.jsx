import { createContext, useContext, useState, useEffect } from 'react'

// El contexto es una "caja global" que cualquier componente puede leer
// sin tener que pasar props de padre a hijo manualmente.
const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null)
  const [cargando, setCargando] = useState(true)

  // Al arrancar la app, revisa si ya había una sesión guardada en
  // localStorage (por si el usuario recargó la página).
  useEffect(() => {
    const token    = localStorage.getItem('token')
    const datos    = localStorage.getItem('usuario')
    if (token && datos) {
      setUsuario(JSON.parse(datos))
    }
    setCargando(false)
  }, [])

  // Se llama desde la pantalla de Login cuando el backend responde OK.
  // Guarda el token y los datos del usuario en localStorage y en el estado.
  const login = (datosDelBackend) => {
    const datosUsuario = {
      nombre:    datosDelBackend.usuario,
      rol:       datosDelBackend.rol,       // 1=Admin, 2=Supervisor, 3=Operario
      area:      datosDelBackend.area,
      esGeneral: datosDelBackend.esGeneral,
    }
    localStorage.setItem('token',   datosDelBackend.token)
    localStorage.setItem('usuario', JSON.stringify(datosUsuario))
    setUsuario(datosUsuario)
  }

  // Limpia todo y vuelve al estado sin sesión.
  const logout = () => {
    localStorage.clear()
    setUsuario(null)
  }

  // Helpers para no tener que comparar números de rol en cada componente.
  const esAdmin      = () => usuario?.rol === 1
  const esSupervisor = () => usuario?.rol === 2
  const esOperario   = () => usuario?.rol === 3

  return (
    <AuthContext.Provider value={{ usuario, cargando, login, logout, esAdmin, esSupervisor, esOperario }}>
      {children}
    </AuthContext.Provider>
  )
}

// Hook personalizado. En cualquier componente puedes escribir:
// const { usuario, logout, esAdmin } = useAuth()
export function useAuth() {
  return useContext(AuthContext)
}
