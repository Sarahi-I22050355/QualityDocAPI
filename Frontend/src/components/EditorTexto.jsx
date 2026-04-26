import { useRef, useCallback, useState } from 'react'
import './EditorTexto.css'

const FUENTES = [
  { label: 'Por defecto',      value: '' },
  { label: 'Arial',            value: 'Arial, sans-serif' },
  { label: 'Georgia',          value: 'Georgia, serif' },
  { label: 'Courier',          value: "'Courier Prime', monospace" },
  { label: 'Playfair Display', value: "'Playfair Display', serif" },
  { label: 'Roboto',           value: 'Roboto, sans-serif' },
  { label: 'Open Sans',        value: "'Open Sans', sans-serif" },
]

const COLORES = [
  '#000000', '#374151', '#6b7280', '#ef4444', '#f97316',
  '#eab308', '#22c55e', '#3b82f6', '#8b5cf6', '#ec4899',
]

// Marcador único que envuelve la tabla de contenido.
// Se usa para localizarla con exactitud y nunca confundirla con el contenido real.
const TOC_START = '<!--TOC_START-->'
const TOC_END   = '<!--TOC_END-->'

// Genera el bloque HTML de la tabla a partir de los títulos encontrados
function construirTOC(nodos) {
  const items = Array.from(nodos).map((nodo, i) => {
    const nivel   = parseInt(nodo.tagName.replace('H', ''))
    const texto   = nodo.innerText || nodo.textContent || ''
    const sangria = (nivel - 1) * 20
    const bullet  = nivel === 1 ? '●' : nivel === 2 ? '○' : '▸'
    return `<li style="margin-left:${sangria}px;margin-bottom:4px;">${bullet} ${texto}</li>`
  }).join('')

  return `${TOC_START}<div style="border:1px solid #e5e7eb;border-radius:8px;padding:16px 20px;margin-bottom:20px;background:#f9fafb;">
    <p style="font-weight:700;font-size:1.05rem;margin:0 0 10px;color:#1a1a2e;">📋 Tabla de contenido</p>
    <ol style="list-style:none;padding:0;margin:0;">${items}</ol>
  </div>${TOC_END}`
}

// Extrae el HTML del editor sin la tabla de contenido
function htmlSinTOC(html) {
  const regex = new RegExp(`${TOC_START}[\\s\\S]*?${TOC_END}`, 'g')
  return html.replace(regex, '').trimStart()
}

// Detecta si el HTML tiene una tabla de contenido activa
function tieneTOC(html) {
  return html.includes(TOC_START)
}

export default function EditorTexto({ value, onChange, placeholder }) {
  const editorRef           = useRef(null)
  const [titulos, setTitulos] = useState([])
  const [tocActiva, setTocActiva] = useState(false)

  const notificar = () => {
    if (onChange) onChange(editorRef.current?.innerHTML || '')
  }

  const cmd = useCallback((comando, valor = null) => {
    editorRef.current?.focus()
    document.execCommand(comando, false, valor)
    notificar()
  }, [onChange])

  const aplicarFuente = (fontFamily) => {
    const editor = editorRef.current
    if (!editor) return
    editor.focus()

    if (!fontFamily) {
      document.execCommand('removeFormat', false, null)
      notificar()
      return
    }

    const selection = window.getSelection()
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed) {
      alert('Selecciona el texto al que quieres aplicar la fuente.')
      return
    }

    // Envolver el texto seleccionado en un span con font-family inline
    // iText7 reconoce style="font-family:..." correctamente en el PDF
    const range = selection.getRangeAt(0)
    const contenidoSeleccionado = range.extractContents()
    const span = document.createElement('span')
    span.style.fontFamily = fontFamily
    span.appendChild(contenidoSeleccionado)
    range.insertNode(span)

    // Mover cursor al final del span
    selection.removeAllRanges()
    const nuevoRango = document.createRange()
    nuevoRango.setStartAfter(span)
    nuevoRango.collapse(true)
    selection.addRange(nuevoRango)

    notificar()
  }

  const handleInput = () => notificar()

  const handlePaste = (e) => {
    e.preventDefault()
    document.execCommand('insertText', false, e.clipboardData.getData('text/plain'))
  }

  // ── Generar / actualizar tabla de contenido ───────────────────────
  // Funciona así:
  // 1. Lee el HTML actual del editor
  // 2. Separa la parte que es tabla de contenido de la que es contenido real
  // 3. Busca los títulos SOLO en el contenido real (no en la tabla)
  // 4. Genera una tabla nueva y la pone al inicio
  // 5. Reinserta: tabla nueva + contenido real (sin la tabla vieja)
  const generarTablaContenido = () => {
    const editor = editorRef.current
    if (!editor) return

    // El contenido real es el HTML sin la tabla anterior
    const htmlReal = htmlSinTOC(editor.innerHTML)

    // Parsear el contenido real para buscar títulos
    const tempDiv = document.createElement('div')
    tempDiv.innerHTML = htmlReal
    const nodosTitulos = tempDiv.querySelectorAll('h1, h2, h3')

    if (nodosTitulos.length === 0) {
      alert('No hay títulos en el documento. Usa el selector "Título" para agregar Título 1, 2 o 3.')
      return
    }

    // Guardar títulos para el panel de vista previa
    const titulosEncontrados = Array.from(nodosTitulos).map((n) => ({
      texto: n.innerText || n.textContent || '',
      nivel: parseInt(n.tagName.replace('H', ''))
    }))
    setTitulos(titulosEncontrados)
    setTocActiva(true)

    // Construir la tabla y recomponer el editor
    const nuevaTOC = construirTOC(nodosTitulos)
    editor.innerHTML = nuevaTOC + htmlReal
    notificar()
  }

  // ── Eliminar tabla de contenido ───────────────────────────────────
  // Solo elimina la tabla — el contenido real no se toca.
  const eliminarTabla = () => {
    const editor = editorRef.current
    if (!editor) return
    const htmlReal = htmlSinTOC(editor.innerHTML)
    editor.innerHTML = htmlReal
    notificar()
    setTocActiva(false)
    setTitulos([])
  }

  return (
    <div className="editor-wrap">

      {/* ── Barra de herramientas ─────────────────────────────────── */}
      <div className="editor-toolbar">

        <select
          className="editor-select"
          onChange={(e) => { cmd('formatBlock', e.target.value); e.target.value = '' }}
          defaultValue=""
          title="Estilo de texto"
        >
          <option value="" disabled>Título</option>
          <option value="p">Párrafo</option>
          <option value="h1">Título 1</option>
          <option value="h2">Título 2</option>
          <option value="h3">Título 3</option>
        </select>

        <select
          className="editor-select editor-select-fuente"
          onChange={(e) => { aplicarFuente(e.target.value); e.target.value = '__ph__' }}
          defaultValue="__ph__"
          title="Fuente"
        >
          <option value="__ph__" disabled>Fuente</option>
          {FUENTES.map((f) => (
            <option key={f.label} value={f.value} style={{ fontFamily: f.value }}>
              {f.label}
            </option>
          ))}
        </select>

        <div className="editor-sep" />

        <button type="button" className="editor-btn" onClick={() => cmd('bold')}          title="Negrita"><strong>N</strong></button>
        <button type="button" className="editor-btn" onClick={() => cmd('italic')}        title="Cursiva"><em>C</em></button>
        <button type="button" className="editor-btn" onClick={() => cmd('underline')}     title="Subrayado"><u>S</u></button>
        <button type="button" className="editor-btn" onClick={() => cmd('strikeThrough')} title="Tachado"><s>T</s></button>

        <div className="editor-sep" />

        <button type="button" className="editor-btn" onClick={() => cmd('insertUnorderedList')} title="Lista con viñetas">• —</button>
        <button type="button" className="editor-btn" onClick={() => cmd('insertOrderedList')}   title="Lista numerada">1.</button>

        <div className="editor-sep" />

        <button type="button" className="editor-btn" onClick={() => cmd('justifyLeft')}   title="Izquierda">⬅</button>
        <button type="button" className="editor-btn" onClick={() => cmd('justifyCenter')} title="Centrar">≡</button>
        <button type="button" className="editor-btn" onClick={() => cmd('justifyRight')}  title="Derecha">➡</button>

        <div className="editor-sep" />

        <div className="editor-colores">
          <span className="editor-color-label">A</span>
          {COLORES.map((c) => (
            <button key={c} type="button" className="editor-color-btn"
              style={{ background: c }} onClick={() => cmd('foreColor', c)} title={c} />
          ))}
        </div>

        <div className="editor-sep" />

        <button type="button" className="editor-btn editor-btn-clear"
          onClick={() => cmd('removeFormat')} title="Limpiar formato">
          ✕ fmt
        </button>

        <div className="editor-sep" />

        {/* Botón generar/actualizar tabla */}
        <button type="button" className="editor-btn editor-btn-toc"
          onClick={generarTablaContenido}
          title={tocActiva ? 'Actualizar tabla de contenido' : 'Generar tabla de contenido'}>
          📋 {tocActiva ? 'Actualizar índice' : 'Índice'}
        </button>

        {/* Botón eliminar tabla — solo visible si hay tabla activa */}
        {tocActiva && (
          <button type="button" className="editor-btn editor-btn-clear"
            onClick={eliminarTabla} title="Eliminar tabla de contenido">
            ✕ índice
          </button>
        )}
      </div>

      {/* ── Panel de vista previa del índice ─────────────────────── */}
      {tocActiva && titulos.length > 0 && (
        <div className="editor-toc-preview">
          <span className="editor-toc-label">Índice generado — haz clic en "Actualizar índice" si agregas más títulos</span>
          {titulos.map((t, i) => (
            <div key={i} className="editor-toc-item" style={{ paddingLeft: `${(t.nivel - 1) * 12}px` }}>
              <span className="editor-toc-bullet">
                {t.nivel === 1 ? '●' : t.nivel === 2 ? '○' : '▸'}
              </span>
              {t.texto}
            </div>
          ))}
        </div>
      )}

      {/* ── Área editable ────────────────────────────────────────── */}
      <div
        ref={editorRef}
        className="editor-content"
        contentEditable
        suppressContentEditableWarning
        onInput={handleInput}
        onPaste={handlePaste}
        data-placeholder={placeholder || 'Escribe el contenido del documento aquí...'}
      />
    </div>
  )
}
