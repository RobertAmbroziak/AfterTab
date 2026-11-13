import { useEffect, useState } from 'react'
import { api } from './api'
import { getToken } from './auth'
import { t } from './i18n'
import { isNetworkError } from './networkStatus'
import NetworkStatus from './NetworkStatus.jsx'

export default function McpOAuthConsent({ requestId }) {
  const [request, setRequest] = useState(null)
  const [account, setAccount] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let active = true
    Promise.all([
      api(`mcp/oauth/requests/${encodeURIComponent(requestId)}`),
      getToken() ? api('auth/me').catch(() => null) : Promise.resolve(null),
    ]).then(([value, profile]) => {
      if (active) { setRequest(value); setAccount(profile?.user || null) }
    }).catch(reason => { if (active) setError(reason.message) })
    return () => { active = false }
  }, [requestId])

  async function decide(approve) {
    if (busy) return
    setBusy(true); setError('')
    try {
      const result = await api('mcp/oauth/consent', { method: 'POST', body: { requestId, approve } })
      location.assign(result.redirectUrl)
    } catch (reason) { setError(reason.message); setBusy(false) }
  }

  return <main className="app-shell">
    <NetworkStatus />
    <section className="workspace mcp-oauth-consent" aria-labelledby="mcp-oauth-title">
      <h1 id="mcp-oauth-title">{t('Połącz AfterTab')}</h1>
      {error && !isNetworkError(error) && <div className="connection-status auth-error" role="alert">{t(error)}</div>}
      {!request && !error && <p role="status">{t('Sprawdzam prośbę o dostęp…')}</p>}
      {request && <>
        <p>{t('Aplikacja prosi o połączenie z Twoim kontem AfterTab:')} <strong>{request.clientName}</strong></p>
        <p>{t('Aktualnie zalogowane konto:')} <strong>{account?.displayName || t('Brak aktywnej sesji')}</strong></p>
        <p>{t('Udostępniane uprawnienia:')}</p>
        <ul>
          {request.scopes.includes('aftertab.read') && <li>{t('Odczyt Twoich wydarzeń i rozliczeń')}</li>}
          {request.scopes.includes('aftertab.write') && <li>{t('Tworzenie i zmiana wydarzeń oraz rozliczeń')}</li>}
        </ul>
        <p className="helper-text">{t('Zgoda dotyczy konta AfterTab aktualnie zalogowanego w tej przeglądarce. Możesz ją cofnąć, odłączając aplikację w ustawieniach integracji.')}</p>
        <div className="form-actions">
          <button className="primary-button" type="button" disabled={busy} onClick={() => decide(true)}>{busy ? t('Łączenie…') : t('Zezwól')}</button>
          <button className="secondary-button" type="button" disabled={busy} onClick={() => decide(false)}>{t('Odmów')}</button>
        </div>
        <p className="helper-text">{t('Jeśli nie jesteś zalogowany w AfterTab, otwórz aplikację w nowej karcie, zaloguj się i wróć tutaj.')}</p>
        <a href="/" target="_blank" rel="noopener noreferrer">{t('Otwórz AfterTab w nowej karcie')}</a>
      </>}
    </section>
  </main>
}
