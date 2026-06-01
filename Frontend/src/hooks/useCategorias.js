import { useState, useEffect } from 'react'
import api from '../api/axios'

// Hook reutilizable — llama al API una sola vez y devuelve la lista completa
// desde la base de datos, incluyendo las 31 categorías actuales.
// Uso: const categorias = useCategorias()
// Devuelve: [{ id, nombre, descripcion }, ...]
export function useCategorias() {
  const [categorias, setCategorias] = useState([])

  useEffect(() => {
    api.get('/Categorias')
      .then(r => setCategorias(r.data.categorias || []))
      .catch(() => setCategorias([]))  // si falla, queda vacío y no rompe la UI
  }, [])

  return categorias
}