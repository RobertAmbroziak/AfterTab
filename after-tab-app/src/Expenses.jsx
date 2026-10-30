import { useEffect, useImperativeHandle, useRef, useState } from 'react'
import { api } from './api'
import { calculateAllocation, equalPercentages, money, parseHundredths } from './expenseAllocation'

const colors = ['#1d4ed8', '#7e22ce', '#047857', '#b45309', '#be123c', '#0e7490']
export function PersonAvatar({ person }) {
  const hash = [...person.participantId].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return <span className="person-avatar" aria-hidden="true" style={{ backgroundColor: colors[hash % colors.length] }}>{[...person.displayName][0]?.toLocaleUpperCase('pl') || '?'}</span>
}

function CreateExpense({ eventId, participants, user, isOwner, onCreated, onCancel, item }) {
  const [name, setName] = useState(item?.name || '')
  const [amount, setAmount] = useState(item ? (item.amountMinor / 100).toFixed(2) : '')
  const [method, setMethod] = useState(item?.splitMethod || 'Percentage')
  const [shares, setShares] = useState(item ? Object.fromEntries(item.splits.map(person => [person.participantId, String((item.splitMethod === 'Percentage' ? person.percentageBps || 0 : person.amountMinor) / 100)])) : equalPercentages(participants.map(person => person.participantId)))
  const [payerId, setPayerId] = useState(item?.paidByParticipantId || user.participantId)
  const [balanced, setBalanced] = useState(item?.balanced || false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const lock = useRef(false)
  const people = new Map(participants.map(person => [person.participantId, person]))
  const calculation = calculateAllocation(amount, method, shares, user.participantId, balanced)
  function change(action) { action(); setBalanced(false); setError('') }
  function toggle(id) {
    change(() => setShares(previous => {
      const next = { ...previous }
      if (id in next) delete next[id]; else next[id] = ''
      return method === 'Percentage' ? equalPercentages(Object.keys(next)) : next
    }))
  }
  async function submit(event) {
    event.preventDefault()
    if (lock.current || calculation.error) return
    lock.current = true; setBusy(true); setError('')
    try {
      const result = await api(`events/${eventId}/expenses${item ? `/${item.id}/split` : ''}`, { method: item ? 'PUT' : 'POST', body: {
        rowVersion: item?.rowVersion,
        paidByParticipantId: payerId,
        name: name.trim(), amountMinor: parseHundredths(amount), splitMethod: method, balance: balanced,
        shares: Object.entries(shares).map(([participantId, value]) => ({ participantId,
          amountMinor: method === 'Amount' ? parseHundredths(value || '0') : 0,
          percentageBps: method === 'Percentage' ? parseHundredths(value || '0') : 0 })),
      } })
      onCreated(result)
    } catch (reason) { setError(reason.message) }
    finally { lock.current = false; setBusy(false) }
  }
  return <form className="app-form expense-form" onSubmit={submit}>
    <h3>{item ? 'Popraw podział' : 'Dodaj pozycję'}</h3>
    <fieldset disabled={busy}>
      <label>Nazwa pozycji<input value={name} readOnly={!!item} onChange={e => setName(e.target.value)} required maxLength={200} placeholder="Np. Pizza" autoFocus /></label>
      <label>Kwota pozycji (zł)<input inputMode="decimal" value={amount} readOnly={!!item} onChange={e => change(() => setAmount(e.target.value))} required placeholder="62,50" /></label>
      {!item && isOwner && <label>Płacący<select aria-label="Płacący" value={payerId} onChange={e => setPayerId(e.target.value)}>{participants.map(person => <option key={person.participantId} value={person.participantId}>{person.displayName}{person.isVirtual ? ' — bez konta' : ''}</option>)}</select></label>}
      <div className="form-actions" role="group" aria-label="Rodzaj podziału">
        {['Percentage', 'Amount'].map(value => <button key={value} type="button" className={method === value ? 'primary-button' : 'secondary-button'} aria-pressed={method === value} onClick={() => { if (value !== method) change(() => { setMethod(value); setShares(value === 'Percentage' ? equalPercentages(Object.keys(shares)) : Object.fromEntries(Object.keys(shares).map(id => [id, '']))) }) }}>{value === 'Amount' ? 'Kwotowo' : 'Procentowo'}</button>)}
      </div>
      <p>Wybierz osoby do podziału. Kolor i litera pomagają je rozpoznać.</p>
      <div className="person-picker" role="group" aria-label="Osoby do podziału">{participants.map(person => <button key={person.participantId} type="button" className="person-chip" aria-pressed={person.participantId in shares} onClick={() => toggle(person.participantId)}><PersonAvatar person={person} /><span>{person.displayName}{person.isVirtual && <small>Bez konta</small>}</span></button>)}</div>
      {Object.entries(shares).map(([id, value]) => <label className="share-input" key={id}><span>{people.get(id)?.displayName || 'Uczestnik'} ({method === 'Amount' ? 'zł' : '%'})</span><input aria-label={`Udział ${people.get(id)?.displayName || 'uczestnika'}`} inputMode="decimal" value={value} placeholder="0" onChange={e => change(() => setShares(previous => ({ ...previous, [id]: e.target.value })))} /></label>)}
      <div className="allocation-preview" aria-live="polite">
        <h4>Podgląd podziału</h4>
        {calculation.error ? <p>{calculation.error}</p> : <>
          <ul className="allocation-list">{calculation.splits.map(row => <li key={row.participantId}><span>{people.get(row.participantId)?.displayName || user.displayName}</span><strong>{money(row.amountMinor)}</strong></li>)}<li><span>Inni</span><strong>{money(calculation.others)}</strong></li><li className="allocation-total"><span>Razem</span><strong>{money(calculation.total)}</strong></li></ul>
          {balanced && <p>Wyrównano: pozostała kwota została doliczona osobie z największym udziałem, a przy remisie autorowi pozycji. Podgląd pokazuje końcowe kwoty.</p>}
        </>}
      </div>
      <p className="helper-text">Nierozdzielona kwota trafi do „Inni”. Osobę bez telefonu autor wydarzenia może dodać jako uczestnika bez konta. Dodanie osoby nie zmienia już zapisanych podziałów.</p>
      <div className="form-actions"><button type="button" className="secondary-button" disabled={!!calculation.error || calculation.others === 0} onClick={() => setBalanced(true)}>Wyrównaj</button><button className="primary-button" disabled={!!calculation.error}>{busy ? 'Zapisywanie…' : 'Zatwierdź'}</button><button type="button" className="text-button" onClick={onCancel}>Anuluj</button></div>
    </fieldset>
    {error && <p className="auth-error" role="alert">{error}</p>}
  </form>
}

function PayerEditor({ eventId, item, participants, onChanged }) {
  const [payerId, setPayerId] = useState(item.paidByParticipantId)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function save(event) {
    event.preventDefault(); if (busy) return; setBusy(true); setError('')
    try { await api(`events/${eventId}/expenses/${item.id}/payer`, { method: 'PUT', body: { paidByParticipantId: payerId, rowVersion: item.rowVersion } }); onChanged() }
    catch (reason) { setError(reason.message) }
    finally { setBusy(false) }
  }
  return <details className="payer-editor"><summary>Zmień płacącego</summary><form className="app-form" onSubmit={save}><label>Płacący<select aria-label="Płacący" value={payerId} onChange={e => setPayerId(e.target.value)} disabled={busy}>{participants.map(person => <option key={person.participantId} value={person.participantId}>{person.displayName}{person.isVirtual ? ' — bez konta' : ''}</option>)}</select></label><button className="secondary-button" disabled={busy}>Zapisz płacącego</button><p className="helper-text">Autor pozycji pozostanie bez zmian.</p>{error && <p className="auth-error" role="alert">{error}</p>}</form></details>
}

export function Expenses({ ref, eventId, participants, user, isOwner, refresh, active, onChanged, onEditing }) {
  const [items, setItems] = useState(null)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState(null)
  const [error, setError] = useState('')
  useImperativeHandle(ref, () => ({ add() { setCreating(true); setEditing(null); onEditing(true) } }), [onEditing])
  useEffect(() => {
    let current = true
    api(`events/${eventId}/expenses`).then(value => { if (current) { setItems(value); setError('') } }).catch(reason => { if (current) setError(reason.message) })
    return () => { current = false }
  }, [eventId, refresh, user.displayName])
  useEffect(() => () => onEditing(false), [onEditing])
  return <div className="event-expenses">
    <div className="section-heading"><h3>Pozycje wydarzenia</h3>{active && !creating && !editing && <button className="primary-button" onClick={() => { setCreating(true); onEditing(true) }}>Dodaj pozycję</button>}</div>
    {(creating || editing) && active && <CreateExpense key={editing?.id || 'new'} item={editing} eventId={eventId} participants={participants} user={user} isOwner={isOwner} onCancel={() => { setCreating(false); setEditing(null); onEditing(false) }} onCreated={item => { setItems(previous => [...(previous || []).filter(x => x.id !== item.id), item]); setCreating(false); setEditing(null); onEditing(false); onChanged() }} />}
    {error && <p className="auth-error" role="alert">{error}</p>}
    {!items && !error ? <p>Wczytywanie pozycji…</p> : items?.length === 0 ? <p>Nie ma jeszcze pozycji. Każdy uczestnik może dodać pierwszą.</p> : <ul className="expense-list">{items?.map(item => <li className="expense-card" key={item.id}>
      <details className="expense-breakdown"><summary><span className="expense-symbol" aria-hidden="true">▤</span><span><strong>{item.name}</strong><small>Zapłacił(a): {item.payerName || item.authorName}</small><small>{item.splitMethod === 'Percentage' ? 'Podział procentowy' : 'Podział kwotowy'}{item.balanced ? ' · Wyrównano' : ''}</small></span><strong>{money(item.amountMinor)}</strong><span aria-hidden="true" className="chevron">⌄</span></summary>
      <p className="helper-text">Dodał(a): {item.authorName}</p>
      {active && item.authorUserId === user.id && !creating && !editing && <button className="text-button" onClick={() => { setEditing(item); onEditing(true) }}>Popraw podział</button>}
      <ul className="allocation-list">{item.splits.map(person => <li key={person.participantId}><span className="split-person"><PersonAvatar person={person} /><span>{person.displayName}{person.percentageBps != null && <small>zadano {(person.percentageBps / 100).toLocaleString('pl-PL')}%</small>}</span></span><strong>{money(person.amountMinor)}</strong></li>)}<li><span>Inni</span><strong>{money(item.othersAmountMinor)}</strong></li><li className="allocation-total"><span>Razem</span><strong>{money(item.amountMinor)}</strong></li></ul>
      {active && isOwner && <PayerEditor key={item.rowVersion} eventId={eventId} item={item} participants={participants} onChanged={onChanged} />}
      </details>
    </li>)}</ul>}
  </div>
}
