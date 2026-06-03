/**
 * swal.js — Utilidades de SweetAlert2 para QualityDoc
 * Tema "Slate Dark" consistente con el resto de la UI.
 *
 * USO:
 *   import { swalConfirm, swalExito, swalError, swalInfo, swalWarning } from '../utils/swal'
 */
import Swal from 'sweetalert2'
import 'sweetalert2/dist/sweetalert2.min.css'

// ── Toast de notificación rápida (esquina inferior derecha) ──────────
const Toast = Swal.mixin({
  toast: true,
  position: 'bottom-end',
  showConfirmButton: false,
  timer: 3200,
  timerProgressBar: true,
  didOpen: (toast) => {
    toast.addEventListener('mouseenter', Swal.stopTimer)
    toast.addEventListener('mouseleave', Swal.resumeTimer)
  },
})

/** Muestra un toast de éxito */
export function swalExito(mensaje) {
  return Toast.fire({ icon: 'success', title: mensaje })
}

/** Muestra un toast de error */
export function swalError(mensaje) {
  return Toast.fire({ icon: 'error', title: mensaje })
}

/** Muestra un toast de información */
export function swalInfo(mensaje) {
  return Toast.fire({ icon: 'info', title: mensaje })
}

/** Muestra un toast de advertencia */
export function swalWarning(mensaje) {
  return Toast.fire({ icon: 'warning', title: mensaje })
}

/**
 * Diálogo de confirmación (reemplaza window.confirm).
 * Retorna true si el usuario confirma, false si cancela.
 *
 * @param {object} opciones
 * @param {string} opciones.titulo   - Título del diálogo
 * @param {string} opciones.texto    - Texto descriptivo (HTML permitido)
 * @param {string} [opciones.textoConfirmar] - Texto del botón confirmar
 * @param {string} [opciones.textoCancelar]  - Texto del botón cancelar
 * @param {string} [opciones.icono]          - warning|question|info|error
 */
export async function swalConfirm({
  titulo = '¿Estás seguro?',
  texto = '',
  textoConfirmar = 'Confirmar',
  textoCancelar = 'Cancelar',
  icono = 'warning',
} = {}) {
  const result = await Swal.fire({
    title: titulo,
    html: texto,
    icon: icono,
    showCancelButton: true,
    confirmButtonText: textoConfirmar,
    cancelButtonText: textoCancelar,
    reverseButtons: true,
    focusCancel: true,
  })
  return result.isConfirmed
}

/**
 * Diálogo de confirmación destructiva (botón rojo).
 * Para acciones peligrosas como desactivar, eliminar, etc.
 */
export async function swalConfirmPeligro({
  titulo = '¿Estás seguro?',
  texto = '',
  textoConfirmar = 'Sí, continuar',
  textoCancelar = 'Cancelar',
} = {}) {
  const result = await Swal.fire({
    title: titulo,
    html: texto,
    icon: 'warning',
    showCancelButton: true,
    confirmButtonText: textoConfirmar,
    cancelButtonText: textoCancelar,
    reverseButtons: true,
    focusCancel: true,
    customClass: {
      confirmButton: 'swal2-confirm swal2-confirm-danger',
    },
  })
  return result.isConfirmed
}

/** Exporta la instancia base por si se necesita personalizar más */
export default Swal
