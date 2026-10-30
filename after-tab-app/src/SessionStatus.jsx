import { useEffect, useState } from 'react'
import { SESSION_CHECK_MS } from './sessionCheck'

export default function SessionStatus() {
  const [deadline] = useState(() => Date.now() + SESSION_CHECK_MS)
  const [remaining, setRemaining] = useState(SESSION_CHECK_MS / 1000)
  useEffect(() => {
    const update = () => setRemaining(Math.max(0, Math.ceil((deadline - Date.now()) / 1000)))
    const timer = window.setInterval(update, 250)
    const onVisible = () => update()
    document.addEventListener('visibilitychange', onVisible)
    return () => { window.clearInterval(timer); document.removeEventListener('visibilitychange', onVisible) }
  }, [deadline])
  const minutes = Math.floor(remaining / 60)
  const seconds = String(remaining % 60).padStart(2, '0')
  return <div className="connection-status session-status">
    <span className="spinner" aria-hidden="true" />
    <span className="session-status-copy" role="status"><strong>Sprawdzanie sesji…</strong><span>{remaining <= 114 ? 'Serwer może się wybudzać. Połączenie może potrwać do 2 minut. Próby ponawiamy automatycznie.' : 'Pierwsze połączenie po przerwie może potrwać do 2 minut.'}</span></span>
    <span className="session-countdown" role="timer" aria-label="Pozostały czas sprawdzania sesji" aria-live="off">{minutes}:{seconds}</span>
  </div>
}
