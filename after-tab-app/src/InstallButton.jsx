import { t } from './i18n.js'
import { useEffect, useId, useRef, useState } from 'react'

function isStandalone() {
  return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
}

export default function InstallButton() {
  const titleId = useId()
  const [installed, setInstalled] = useState(isStandalone)
  const [installPrompt, setInstallPrompt] = useState(null)
  const [installing, setInstalling] = useState(false)
  const [showHelp, setShowHelp] = useState(false)
  const dialog = useRef(null)
  const isAppleMobile = /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)

  useEffect(() => {
    const displayMode = window.matchMedia('(display-mode: standalone)')
    function onPrompt(event) {
      event.preventDefault()
      setInstallPrompt(event)
    }
    function onInstalled() {
      setInstalled(true)
      setInstallPrompt(null)
      setShowHelp(false)
    }
    function onDisplayModeChange() {
      setInstalled(isStandalone())
    }
    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)
    displayMode.addEventListener('change', onDisplayModeChange)
    return () => {
      window.removeEventListener('beforeinstallprompt', onPrompt)
      window.removeEventListener('appinstalled', onInstalled)
      displayMode.removeEventListener('change', onDisplayModeChange)
    }
  }, [])

  useEffect(() => {
    if (showHelp && !installed) dialog.current?.showModal()
  }, [showHelp, installed])

  async function install() {
    if (installing) return
    if (!installPrompt) {
      setShowHelp(true)
      return
    }
    setInstalling(true)
    try {
      // The browser still requires the user's confirmation.
      await installPrompt.prompt()
    } catch {
      setShowHelp(true)
    } finally {
      setInstallPrompt(null)
      setInstalling(false)
    }
  }

  if (installed) return null

  return (
    <>
      <button className="auth-button install-button" type="button" onClick={install} disabled={installing} aria-label={t("Zainstaluj AfterTab")}>
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5" />
        </svg>
        <span>{installing ? t("Instalowanie…") : t("Zainstaluj")}</span>
      </button>
      <dialog ref={dialog} className="install-dialog" aria-labelledby={titleId} onClose={() => setShowHelp(false)}>
        <h2 id={titleId}>{t("Zainstaluj AfterTab")}</h2>
        {isAppleMobile ? (
          <>
            <p>{t("Na iPhonie instalację potwierdzasz w menu przeglądarki. iOS nie udostępnia stronie automatycznego okna instalacji takiego jak Android.")}</p>
            <ol>
              <li>{t("Otwórz menu")} <strong>{t("Udostępnij")}</strong> {t("w przeglądarce.")}</li>
              <li>{t("Wybierz")} <strong>{t("Dodaj do ekranu początkowego")}</strong>.</li>
              <li>{t("Potwierdź przyciskiem")} <strong>{t("Dodaj")}</strong>.</li>
            </ol>
            <p>{t("Jeśli nie widzisz tej opcji, otwórz stronę w Safari.")}</p>
          </>
        ) : (
          <>
            <p>{t("Otwórz menu przeglądarki i wybierz")} <strong>{t("Zainstaluj aplikację")}</strong> {t("lub")} <strong>{t("Dodaj do ekranu głównego")}</strong>{t(". Na komputerze sprawdź też ikonę instalacji przy pasku adresu.")}</p>
            <p>{t("Jeśli opcja nie jest dostępna, spróbuj w Chrome lub Edge. Przeglądarka może potrzebować chwili, aby udostępnić instalację. Możesz też korzystać z AfterTab bezpośrednio w przeglądarce.")}</p>
          </>
        )}
        <form method="dialog">
          <button className="auth-button" type="submit">{t("Rozumiem")}</button>
        </form>
      </dialog>
    </>
  )
}
