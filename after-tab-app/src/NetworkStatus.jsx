import { useEffect, useState } from 'react'
import { t } from './i18n.js'
import { subscribeNetworkStatus } from './networkStatus'

export default function NetworkStatus() {
  const [state, setState] = useState({ pending: 0, unavailable: false })
  const [showPending, setShowPending] = useState(false)
  useEffect(() => subscribeNetworkStatus(setState), [])
  useEffect(() => {
    if (!state.pending || state.unavailable) { setShowPending(false); return }
    const timer = setTimeout(() => setShowPending(true), 250)
    return () => clearTimeout(timer)
  }, [state.pending, state.unavailable])

  if (state.unavailable) return <div className="connection-status network-status" role="status" aria-live="polite">
    <span className="network-status-dot" aria-hidden="true" />
    <span>{t('Brak połączenia z serwerem. Sprawdź internet i spróbuj ponownie.')}</span>
    <button className="secondary-button" onClick={() => window.location.reload()}>{t('Ponów połączenie')}</button>
  </div>
  if (showPending) return <div className="connection-status network-status" role="status" aria-live="polite">
    <span className="spinner" aria-hidden="true" />
    <span>{t('Łączenie z serwerem…')}</span>
  </div>
  return null
}
