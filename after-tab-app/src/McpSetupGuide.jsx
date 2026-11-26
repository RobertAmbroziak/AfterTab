import { useEffect, useRef } from 'react'
import { useLanguage } from './useLanguage'
import { mcpSetupGuides } from './mcpSetupGuides'

export default function McpSetupGuide({ type, onClose }) {
  const language = useLanguage()
  const dialogRef = useRef(null)
  const guide = mcpSetupGuides[type]
  const copy = language === 'pl'
    ? { label: 'Instrukcja', close: 'Zamknij', image: 'Zrzut ekranu' }
    : { label: 'Instructions', close: 'Close', image: 'Screenshot' }

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  if (!guide) return null

  return <dialog
    ref={dialogRef}
    className="help-guide-dialog"
    aria-labelledby="mcp-guide-title"
    onClose={onClose}
    onClick={event => { if (event.target === dialogRef.current) onClose() }}
  >
    <div className="help-guide-panel">
      <header className="help-guide-header">
        <div>
          <p className="help-guide-eyebrow">{copy.label}</p>
          <h2 id="mcp-guide-title">{guide.title[language]}</h2>
          <p>{guide.intro[language]}</p>
        </div>
        <button type="button" className="help-guide-close" onClick={onClose} aria-label={copy.close} title={copy.close}>
          <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="m18 6-12 12M6 6l12 12" /></svg>
        </button>
      </header>
      <section className="help-guide-list" aria-label={guide.title[language]}>
        {guide.items.map(item => (
          <details className="help-guide-item" key={item.image}>
            <summary>{item.title[language]}</summary>
            <div className="help-guide-item-content">
              <p>{item.description[language]}</p>
              <div className="help-guide-images">
                <img src={`/help/${language.toUpperCase()}/${item.image}${language.toUpperCase()}.PNG`} alt={`${copy.image}: ${item.title[language]}`} loading="lazy" />
              </div>
            </div>
          </details>
        ))}
      </section>
      <footer className="help-guide-footer">
        <button type="button" className="primary-button" onClick={onClose}>{copy.close}</button>
      </footer>
    </div>
  </dialog>
}
