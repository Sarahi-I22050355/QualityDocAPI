import axios from 'axios'

// URL base de tu backend. Si cambias el puerto en launchSettings.json
// solo tienes que modificar esta línea.
const api = axios.create({
  baseURL: 'http://localhost:5126/api',
})

// Interceptor: antes de cada request, agarra el token guardado en
// localStorage y lo pone automáticamente en el header Authorization.
// Así no tienes que pasarlo manualmente en cada llamada.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Interceptor de respuesta: si el backend responde 401 (token vencido
// o inválido), limpia todo y manda al usuario al login.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.clear()
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default api