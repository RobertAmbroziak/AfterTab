import { useEffect, useRef, useState } from 'react'
import { useLanguage } from './useLanguage'
import { helpSections } from './helpContent'

export default function HelpGuide() {
  const language = useLanguage()
  const [open, setOpen] = useState(false)
  const [activeSection, setActiveSection] = useState(helpSections[0].id)
  const dialogRef = useRef(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  const section = helpSections.find(item => item.id === activeSection) || helpSections[0]
  const copy = language === 'pl'
    ? { label: 'Instrukcja', close: 'Wróć do aplikacji', heading: 'Jak korzystać z AfterTab', intro: 'Wybierz temat, aby zobaczyć krótką instrukcję i ekran z aplikacji.', image: 'Zrzut ekranu' }
    : { label: 'Help', close: 'Back to the app', heading: 'How to use AfterTab', intro: 'Choose a topic for a short guide and a screenshot from the app.', image: 'Screenshot' }

  return <>
    <button type="button" className="help-guide-trigger" aria-label={language === 'pl' ? 'Otwórz instrukcję' : 'Open help'} title={copy.label} onClick={() => setOpen(true)}>
      <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M9.6 9a2.5 2.5 0 0 1 4.8.8c0 1.7-2.4 2-2.4 3.7M12 17h.01" />
      </svg>
      <span>{copy.label}</span>
    </button>
    <dialog
      ref={dialogRef}
      className="help-guide-dialog"
      aria-labelledby="help-guide-title"
      onClose={() => setOpen(false)}
      onClick={event => { if (event.target === dialogRef.current) setOpen(false) }}
    >
      <div className="help-guide-panel">
        <header className="help-guide-header">
          <div>
            <p className="help-guide-eyebrow">{copy.label}</p>
            <h2 id="help-guide-title">{copy.heading}</h2>
            <p>{copy.intro}</p>
          </div>
          <button type="button" className="help-guide-close" onClick={() => setOpen(false)} aria-label={copy.close} title={copy.close}>
            <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="m18 6-12 12M6 6l12 12" /></svg>
          </button>
        </header>
        <nav className="help-guide-sections" aria-label={copy.label}>
          {helpSections.map(item => (
            <button key={item.id} type="button" aria-pressed={activeSection === item.id} onClick={() => setActiveSection(item.id)}>
              {item.title[language]}
            </button>
          ))}
        </nav>
        <section className="help-guide-list" aria-label={section.title[language]}>
          {section.items.map(item => (
            <details className="help-guide-item" key={item.title.pl}>
              <summary>{item.title[language]}</summary>
              <div className="help-guide-item-content">
                <p>{item.description[language]}</p>
                <div className="help-guide-images">
                  {item.images.map(image => (
                    <img key={image} src={`/help/${language.toUpperCase()}/${image}${language.toUpperCase()}.PNG`} alt={`${copy.image}: ${item.title[language]}`} loading="lazy" />
                  ))}
                </div>
              </div>
            </details>
          ))}
        </section>
        <footer className="help-guide-footer">
          <button type="button" className="primary-button" onClick={() => setOpen(false)}>{copy.close}</button>
        </footer>
      </div>
    </dialog>
  </>
}
