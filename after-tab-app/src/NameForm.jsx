import { t } from './i18n.js'
import { useState } from 'react'

export default function NameForm({ initialName = '', title = t("Jak masz na imię?"), submitLabel = t("Zapisz imię"), onSubmit, onCancel, busy = false }) {
  const [name, setName] = useState(initialName)
  function submit(event) {
    event.preventDefault()
    if (name.trim()) onSubmit(name.trim())
  }
  return (
    <form className="app-form name-form" onSubmit={submit}>
      <h2>{title}</h2>
      <p>{t("Tak zobaczą Cię uczestnicy wydarzeń. Imię możesz później zmienić.")}</p>
      <label>{t("Twoje imię")}<input name="displayName" value={name} onChange={e => setName(e.target.value)} maxLength={100} required autoComplete="given-name" autoFocus /></label>
      <div className="form-actions">
        <button className="primary-button" disabled={busy || !name.trim()}>{submitLabel}</button>
        {onCancel && <button type="button" className="secondary-button" onClick={onCancel} disabled={busy}>{t("Anuluj")}</button>}
      </div>
    </form>
  )
}
