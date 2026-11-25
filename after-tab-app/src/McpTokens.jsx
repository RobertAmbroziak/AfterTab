import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { getLocale, t } from './i18n'
import { isNetworkError } from './networkStatus'

export default function McpTokens({ onClose }) {
  const [tokens, setTokens] = useState([])
  const [oauthClients, setOauthClients] = useState([])
  const [instructions, setInstructions] = useState('')
  const [savedInstructions, setSavedInstructions] = useState('')
  const [savingInstructions, setSavingInstructions] = useState(false)
  const [name, setName] = useState('AfterTab MCP')
  const [canWrite, setCanWrite] = useState(true)
  const [days, setDays] = useState(30)
  const [secret, setSecret] = useState('')
  const [copied, setCopied] = useState(false)
  const [copiedCommand, setCopiedCommand] = useState('')
  const [copyPulse, setCopyPulse] = useState(0)
  const [commandCopyPulse, setCommandCopyPulse] = useState(0)
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState('')
  const copyFeedback = useRef(null)
  const mcpServerUrl = import.meta.env.VITE_MCP_SERVER_URL || 'https://YOUR_MCP_SERVER_URL/mcp'

  useEffect(() => {
    let active = true
    Promise.all([api('profile/mcp-tokens'), api('profile/mcp-instructions'), api('mcp/oauth/clients')]).then(([value, preferences, clients]) => {
      if (active) {
        setTokens(value.filter(token => !token.revokedAtUtc))
        setOauthClients(clients)
        setInstructions(preferences.instructions || '')
        setSavedInstructions(preferences.instructions || '')
      }
    })
      .catch(reason => { if (active) setError(reason.message) })
      .finally(() => { if (active) setBusy(false) })
    return () => { active = false }
  }, [])

  useEffect(() => () => clearTimeout(copyFeedback.current), [])

  async function create(event) {
    event.preventDefault()
    if (busy) return
    setBusy(true); setError(''); setSecret(''); setCopied(false); setCopiedCommand('')
    try {
      const result = await api('profile/mcp-tokens', { method: 'POST', body: { name, canWrite, expiresInDays: Number(days) } })
      setSecret(result.token)
      setTokens(items => [result.details, ...items])
    } catch (reason) { setError(reason.message) }
    finally { setBusy(false) }
  }

  async function saveInstructions(event) {
    event.preventDefault()
    if (savingInstructions || instructions === savedInstructions) return
    setSavingInstructions(true); setError('')
    try {
      const result = await api('profile/mcp-instructions', { method: 'PUT', body: { instructions } })
      setInstructions(result.instructions || '')
      setSavedInstructions(result.instructions || '')
    } catch (reason) { setError(reason.message) }
    finally { setSavingInstructions(false) }
  }

  async function revoke(id) {
    if (busy || !window.confirm(t('Unieważnić ten token? Połączony klient utraci dostęp.'))) return
    setBusy(true); setError(''); setSecret(''); setCopied(false); setCopiedCommand('')
    try {
      await api(`profile/mcp-tokens/${id}`, { method: 'DELETE' })
      setTokens((await api('profile/mcp-tokens')).filter(token => !token.revokedAtUtc))
    } catch (reason) { setError(reason.message) }
    finally { setBusy(false) }
  }

  async function disconnectOAuth(clientId) {
    if (busy || !window.confirm(t('Odłączyć tę aplikację od konta AfterTab?'))) return
    setBusy(true); setError('')
    try {
      await api(`mcp/oauth/clients/${encodeURIComponent(clientId)}`, { method: 'DELETE' })
      setOauthClients(await api('mcp/oauth/clients'))
    } catch (reason) { setError(reason.message) }
    finally { setBusy(false) }
  }

  async function copySecret() {
    try {
      await navigator.clipboard.writeText(secret)
      setCopied(true)
      setCopiedCommand('')
      setCopyPulse(value => value + 1)
      setError('')
      clearTimeout(copyFeedback.current)
      copyFeedback.current = setTimeout(() => setCopied(false), 1400)
    } catch {
      setError(t('Przeglądarka nie udostępnia schowka. Zaznacz token i skopiuj go ręcznie.'))
    }
  }

  async function copySetupCommand(target) {
    const url = mcpServerUrl
    let command

    if (target === 'claude') {
      command = `claude mcp add --transport http --scope user aftertab "${url}" --header "Authorization: Bearer ${secret}"`
    } else if (target === 'cmd-codex') {
      command = `codex mcp logout aftertab >nul 2>&1 & codex mcp remove aftertab >nul 2>&1 & (echo. & echo [mcp_servers.aftertab] & echo url = "${url}" & echo http_headers = { Authorization = "Bearer ${secret}" }) >>"%USERPROFILE%\\.codex\\config.toml"`
    } else {
      command = `codex mcp logout aftertab >/dev/null 2>&1 || true; codex mcp remove aftertab >/dev/null 2>&1 || true; printf '\\n[mcp_servers.aftertab]\\nurl = "%s"\\nhttp_headers = { Authorization = "Bearer %s" }\\n' '${url}' '${secret}' >> "$HOME/.codex/config.toml"`
    }

    try {
      await navigator.clipboard.writeText(command)
      clearTimeout(copyFeedback.current)
      setCopied(false)
      setCopiedCommand(target)
      setCommandCopyPulse(value => value + 1)
      setError('')
      copyFeedback.current = setTimeout(() => setCopiedCommand(''), 1400)
    } catch {
      setError('Clipboard unavailable. Select and copy the token manually.')
    }
  }

  return <section className="workspace integration-panel" aria-label={t('Integracje AI')}>
    <div className="section-heading"><h2>{t('Integracje AI')}</h2><button type="button" className="text-button" onClick={event => { event.preventDefault(); event.stopPropagation(); onClose() }}>{t('Zamknij')}</button></div>

    <details className="integration-section mcp-instructions">
      <summary><span>{t('Instrukcje Asystenta')}</span></summary>
      <form className="app-form" onSubmit={saveInstructions}>
        <label htmlFor="mcp-instructions">{t('Osobiste instrukcje dla asystenta')}</label>
        <textarea id="mcp-instructions" rows="5" maxLength={2000} value={instructions}
          placeholder={t('Np. gdy piszę „ja” lub „mnie”, chodzi o moje konto AfterTab.')}
          onChange={event => setInstructions(event.target.value)} />
        <p className="helper-text">{t('Asystent odczyta te zasady z Twojego konta przed zmianami w wydarzeniach. Nie wpisuj tu haseł ani tokenów. Maksymalnie 2000 znaków.')}</p>
        <button className="secondary-button" disabled={savingInstructions || instructions === savedInstructions}>{savingInstructions ? t('Zapisywanie…') : t('Zapisz zmiany')}</button>
      </form>
    </details>

    <details className="integration-section mcp-oauth-clients">
      <summary><span>{t('Połączenie OAuth')}</span></summary>
      <div className="mcp-server-address">
        <span>{t('Adres serwera MCP:')}</span>
        {mcpServerUrl && <code>{mcpServerUrl}</code>}
      </div>
      {oauthClients.length === 0 && !busy && <p className="helper-text">{t('Nie ma połączonych aplikacji.')}</p>}
      <div className="mcp-oauth-list">{oauthClients.map(client => <article className="mcp-oauth-client" key={client.clientId}>
        <div className="mcp-oauth-client-info">
          <strong>{client.clientName || t('Aplikacja OAuth')}</strong>
          <span className="helper-text">{t('Połączono:')} {client.connectedAtUtc ? new Date(client.connectedAtUtc).toLocaleString(getLocale()) : t('Data niedostępna')}</span>
        </div>
        <button type="button" className="text-button danger" disabled={busy} onClick={() => disconnectOAuth(client.clientId)}>{t('Odłącz')}</button>
      </article>)}</div>
    </details>

    {error && !isNetworkError(error) && <p role="alert" className="auth-error">{t(error)}</p>}

    <details className="integration-section manual-mcp">
      <summary><span>{t('Połączenie Token')}</span></summary>
      <p className="helper-text">{t('Token działa z uprawnieniami bieżącego konta AfterTab. Konto może być trwałe albo zapamiętanym gościem. Nie wklejaj tokenu do rozmowy z ChatGPT.')}</p>
      <form className="app-form" onSubmit={create}>
        <label>{t('Nazwa tokena')}<input value={name} maxLength={100} required onChange={event => setName(event.target.value)} /></label>
        <label>{t('Ważność (dni)')}<input type="number" min="1" max="90" required value={days} onChange={event => setDays(event.target.value)} /></label>
        <fieldset className="integration-access">
          <legend>{t('Zakres dostępu')}</legend>
          <label className="access-option"><input type="radio" name="mcp-access" checked={!canWrite} onChange={() => setCanWrite(false)} /><span><strong>{t('Tylko odczyt')}</strong><small>{t('Przegląd wydarzeń, pozycji i rozliczeń.')}</small></span></label>
          <label className="access-option"><input type="radio" name="mcp-access" checked={canWrite} onChange={() => setCanWrite(true)} /><span><strong>{t('Odczyt i zapis')}</strong><small>{t('Dodatkowo tworzenie wydarzeń, uczestników i pozycji oraz zmiany rozliczeń.')}</small></span></label>
        </fieldset>
        <button className="primary-button" disabled={busy}>{t('Utwórz token MCP')}</button>
      </form>
      {secret && <div className="new-secret" role="status">
        <p>{t('Token pokaże się tylko teraz. Skopiuj go bezpośrednio do ustawień klienta MCP; nie wklejaj go do czatu.')}</p>
        <label>{t('Nowy token')}<input type="password" readOnly value={secret} autoComplete="off" onFocus={event => event.target.select()} /></label>
        <div className="form-actions">
          <button key={copyPulse} type="button" className={`secondary-button copy-token-button${copied ? ' is-copied' : ''}`} onClick={copySecret}>{copied ? t('Skopiowano') : t('Kopiuj token')}</button>
          <button type="button" className="text-button" onClick={() => { setSecret(''); setCopied(false); setCopiedCommand('') }}>{t('Ukryj token')}</button>
        </div>
        <div className="mcp-command-copy" role="group" aria-label="Copy MCP setup command">
          {[
            ['claude', 'Claude (CMD / Bash)'],
            ['cmd-codex', 'CMD Codex'], ['bash-codex', 'Bash Codex'],
          ].map(([target, label]) => <button key={copiedCommand === target ? `${target}-${commandCopyPulse}` : target} type="button"
            className={`mcp-command-button${copiedCommand === target ? ' is-copied' : ''}`}
            title={`Copy ${label} setup command`} aria-label={`Copy ${label} setup command`}
            onClick={() => copySetupCommand(target)}>
            <span className="mcp-command-glyph" aria-hidden="true">&gt;_</span>
            <span>{copiedCommand === target ? 'Copied' : label}</span>
          </button>)}
        </div>
        <p className="mcp-command-note">Claude works in CMD and Bash. Codex commands are shell-specific. Commands contain your token and may be saved in terminal history. Codex also stores it in its local config.</p>
      </div>}
      <ul className="integration-token-list">{tokens.map(token => <li key={token.id}>
        <strong>{token.name}</strong> — {token.canWrite ? t('Odczyt i zapis') : t('Tylko odczyt')}
        <p>{t('Ważny do:')} {new Date(token.expiresAtUtc).toLocaleString(getLocale())}</p>
        <button className="text-button danger" disabled={busy} onClick={() => revoke(token.id)}>{t('Unieważnij token')}</button>
      </li>)}</ul>
    </details>
  </section>
}
