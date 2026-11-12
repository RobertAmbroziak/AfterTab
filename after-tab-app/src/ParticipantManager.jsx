import { t } from './i18n.js'
import { useRef, useState } from 'react'
import { api } from './api'

function VirtualMember({ person, accounts, disabled, onAction }) {
  const [name, setName] = useState(person.displayName)
  const [targetId, setTargetId] = useState('')
  return <div className="virtual-member-editor">
    <strong>{person.displayName} <span className="badge virtual-badge">{t("Bez konta")}</span></strong>
    <form className="app-form" onSubmit={event => { event.preventDefault(); onAction(`/${person.participantId}`, 'PUT', { displayName: name.trim() }) }}>
      <label>{t("Imię uczestnika bez konta")}<input value={name} onChange={event => setName(event.target.value)} required maxLength={100} disabled={disabled} /></label>
      <div className="form-actions"><button className="secondary-button" disabled={disabled || name.trim() === person.displayName}>{t("Zapisz imię")}</button>
        <button type="button" className="text-button danger" disabled={disabled} onClick={() => {
          if (window.confirm(t("Usunąć uczestnika „{v0}”? Można usunąć tylko osobę bez zapisanych wydatków, udziałów i oznaczeń przelewów.", { v0: person.displayName }))) onAction(`/${person.participantId}`, 'DELETE')
        }}>{t("Usuń uczestnika")}</button></div>
    </form>
    {accounts.length > 0 && <form className="app-form" onSubmit={event => {
      event.preventDefault()
      const target = accounts.find(person => person.participantId === targetId)
      if (!target || !window.confirm(t("Połączyć „{v0}” z „{v1}”? Ich udziały i wydatki zostaną połączone. Konto tej osoby przejmie rozliczenie uczestnika bez konta. Tej operacji nie można cofnąć w aplikacji.", { v0: person.displayName, v1: target.displayName }))) return
      onAction(`/${person.participantId}/link`, 'POST', { targetParticipantId: targetId })
    }}>
      <label>{t("Połącz z osobą, która dołączyła")}<select value={targetId} onChange={event => setTargetId(event.target.value)} required disabled={disabled}><option value="">{t("Wybierz uczestnika z kontem")}</option>{accounts.map(account => <option key={account.participantId} value={account.participantId}>{account.displayName}{account.isOwner ? t(" — autor wydarzenia") : ''}</option>)}</select></label>
      <button className="secondary-button" disabled={disabled || !accounts.some(account => account.participantId === targetId)}>{t("Połącz uczestników")}</button>
    </form>}
  </div>
}

export default function ParticipantManager({ eventId, participants, active, editing, onChanged }) {
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const lock = useRef(false)
  const virtual = participants.filter(person => person.isVirtual)
  const accounts = participants.filter(person => !person.isVirtual)
  async function action(path, method, body) {
    if (lock.current || !active || editing) return
    lock.current = true; setBusy(true); setError(''); setMessage('')
    try {
      await api(`events/${eventId}/participants${path}`, { method, body })
      if (path === '/virtual') setName('')
      setMessage(path.endsWith('/link') ? t("Uczestnicy zostali połączeni. Wydatki i rozliczenie są przeliczane.") : t("Uczestnicy zostali zaktualizowani."))
      onChanged()
    } catch (reason) { setError(reason.message); onChanged() }
    finally { lock.current = false; setBusy(false) }
  }
  return <details className="participant-tools">
    <summary><span>{t("Uczestnicy bez konta")}{virtual.length > 0 ? ` (${virtual.length})` : ''}</span></summary>
    <p>{t("Dodaj osobę bez telefonu lub konta. Będzie można uwzględnić ją w podziałach i wskazać jako płacącego. Jeśli później dołączy przez zaproszenie, połącz obie pozycje poniżej.")}</p>
    {!active ? <p className="helper-text">{t("Aby zmienić uczestników, najpierw otwórz wydarzenie ponownie.")}</p> : <>
      {editing && <p className="helper-text">{t("Najpierw zatwierdź lub anuluj formularz pozycji.")}</p>}
      <form className="app-form" onSubmit={event => { event.preventDefault(); action('/virtual', 'POST', { displayName: name.trim() }) }}>
        <label>{t("Imię nowego uczestnika bez konta")}<input value={name} onChange={event => setName(event.target.value)} required maxLength={100} placeholder={t("Np. Adam")} disabled={busy || editing} /></label>
        <button className="secondary-button" disabled={busy || editing}>{busy ? t("Zapisywanie…") : t("Dodaj uczestnika bez konta")}</button>
      </form>
      {virtual.map(person => <VirtualMember key={`${person.participantId}-${person.displayName}`} person={person} accounts={accounts} disabled={busy || editing} onAction={action} />)}
    </>}
    {error && <p className="auth-error" role="alert">{t(error)}</p>}
    {message && <p role="status">{t(message)}</p>}
  </details>
}
