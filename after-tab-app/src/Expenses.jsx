import { useEffect, useImperativeHandle, useRef, useState } from 'react'
import { api } from './api'
import { calculateAllocation, equalPercentages, money, parseHundredths } from './expenseAllocation'

const colors = ['#1d4ed8', '#7e22ce', '#047857', '#b45309', '#be123c', '#0e7490']
export function PersonAvatar({ person }) {
  const hash = [...person.participantId].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return <span className="person-avatar" aria-hidden="true" style={{ backgroundColor: colors[hash % colors.length] }}>{[...person.displayName][0]?.toLocaleUpperCase('pl') || '?'}</span>
}

function BalanceButton({ name, selected, disabled, onClick }) {
  return <button type="button" className={`balance-button${selected ? ' is-selected' : ''}`} aria-label={`Wyrównaj na ${name}`} aria-pressed={selected} disabled={disabled} title={selected ? `Wyrównano na ${name}. Kliknij, aby wycofać wyrównanie.` : `Wyrównaj na ${name}`} onClick={onClick}>
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 8h15m-4-4 4 4-4 4M20 16H5m4-4-4 4 4 4" /></svg><span>Wyrównaj</span>
  </button>
}

function CreateExpense({ eventId, participants, user, isOwner, onCreated, onCancel, item }) {
  const [name, setName] = useState(item?.name || '')
  const [amount, setAmount] = useState(item ? (item.amountMinor / 100).toFixed(2) : '')
  const [method, setMethod] = useState(item?.splitMethod || 'Percentage')
  const [shares, setShares] = useState(item ? Object.fromEntries(item.splits.map(person => [person.participantId, String((item.splitMethod === 'Percentage' ? person.percentageBps || 0 : person.amountMinor - (person.participantId === item.balanceParticipantId ? item.balanceAmountMinor || 0 : 0)) / 100)])) : equalPercentages(participants.map(person => person.participantId)))
  const [payerId, setPayerId] = useState(item?.paidByParticipantId || user.participantId)
  const [balanceTarget, setBalanceTarget] = useState(item?.balanceParticipantId || null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const lock = useRef(false)
  const people = new Map(participants.map(person => [person.participantId, person]))
  const canBalance = isOwner || payerId === user.participantId
  const calculation = calculateAllocation(amount, method, shares, balanceTarget)
  const selectedBalance = calculation.balanceParticipantId
  function change(action) { action(); setBalanceTarget(null); setError('') }
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
        name: name.trim(), amountMinor: parseHundredths(amount), splitMethod: method, balanceParticipantId: selectedBalance,
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
      {Object.entries(shares).map(([id, value]) => <div className="share-row" key={id}><label className="share-input"><span>{people.get(id)?.displayName || 'Uczestnik'} ({method === 'Amount' ? 'zł' : '%'})</span><input aria-label={`Udział ${people.get(id)?.displayName || 'uczestnika'}`} inputMode="decimal" value={value} placeholder="0" onChange={e => change(() => setShares(previous => ({ ...previous, [id]: e.target.value })))} /></label><BalanceButton name={people.get(id)?.displayName || 'uczestnika'} selected={selectedBalance === id} disabled={busy || !canBalance || !!calculation.error || !(calculation.remainingBeforeBalance > 0)} onClick={() => setBalanceTarget(previous => previous === id ? null : id)} /></div>)}
      <div className="allocation-preview" aria-live="polite">
        <h4>Podgląd podziału</h4>
        {calculation.error ? <p>{calculation.error}</p> : <>
          <ul className="allocation-list">{calculation.splits.map(row => <li key={row.participantId}><span>{people.get(row.participantId)?.displayName || user.displayName}</span><strong>{money(row.amountMinor)}</strong></li>)}<li><span>Inni</span><strong>{money(calculation.others)}</strong></li><li className="allocation-total"><span>Razem</span><strong>{money(calculation.total)}</strong></li></ul>
          {selectedBalance && <p>Wyrównanie {money(calculation.balanceAmount)}: <strong>{people.get(selectedBalance)?.displayName}</strong>. Kliknij przy innej osobie, aby przenieść tę samą resztę, albo ponownie przy wybranej, aby ją wycofać.</p>}
        </>}
      </div>
      <p className="helper-text">Nierozdzielona kwota trafi do „Inni”. Osobę bez telefonu autor wydarzenia może dodać jako uczestnika bez konta. Dodanie osoby nie zmienia już zapisanych podziałów.</p>
      <div className="form-actions"><button className="primary-button" disabled={!!calculation.error}>{busy ? 'Zapisywanie…' : 'Zatwierdź'}</button><button type="button" className="text-button" onClick={onCancel}>Anuluj</button></div>
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
  const [balancing, setBalancing] = useState(null)
  const balanceLock = useRef(false)
  async function balance(item, participantId) {
    if (balanceLock.current) return
    balanceLock.current = true; setBalancing(item.id); setError('')
    try {
      const result = await api(`events/${eventId}/expenses/${item.id}/balance`, { method: 'PUT', body: { participantId: item.balanceParticipantId === participantId ? null : participantId, rowVersion: item.rowVersion } })
      setItems(previous => previous.map(row => row.id === result.id ? result : row)); onChanged()
    } catch (reason) { setError(reason.message); onChanged() }
    finally { balanceLock.current = false; setBalancing(null) }
  }
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
      {active && item.authorUserId === user.id && (!item.balanceParticipantId || isOwner || item.paidByParticipantId === user.participantId) && !creating && !editing && <button className="text-button" onClick={() => { setEditing(item); onEditing(true) }}>Popraw podział</button>}
      <ul className="allocation-list">{item.splits.map(person => <li key={person.participantId}><span className="split-person"><PersonAvatar person={person} /><span>{person.displayName}{person.percentageBps != null && <small>zadano {(person.percentageBps / 100).toLocaleString('pl-PL')}%</small>}</span></span><span className="split-amount"><strong>{money(person.amountMinor)}</strong><BalanceButton name={person.displayName} selected={item.balanceParticipantId === person.participantId} disabled={!active || !!balancing || creating || !!editing || !(isOwner || item.paidByParticipantId === user.participantId) || !((item.othersAmountMinor || 0) + (item.balanceAmountMinor || 0) > 0)} onClick={() => balance(item, person.participantId)} /></span></li>)}<li><span>Inni</span><strong>{money(item.othersAmountMinor)}</strong></li><li className="allocation-total"><span>Razem</span><strong>{money(item.amountMinor)}</strong></li></ul>
      {item.balanceParticipantId && <p className="helper-text">Wyrównanie {money(item.balanceAmountMinor)}: {item.splits.find(person => person.participantId === item.balanceParticipantId)?.displayName}. Podświetlony przycisk wskazuje wybraną osobę.</p>}
      {item.balanced && !item.balanceParticipantId && <p className="helper-text">Pozycja została wyrównana w starszej wersji aplikacji. Zapisane kwoty pozostają bez zmian; aby wybrać osobę do nowego wyrównania, autor pozycji musi najpierw poprawić podział.</p>}
      <p className="helper-text">Wyrównanie może zmienić autor wydarzenia lub płacący. Bez pozostałej kwoty przyciski są nieaktywne.</p>
      {active && isOwner && <PayerEditor key={item.rowVersion} eventId={eventId} item={item} participants={participants} onChanged={onChanged} />}
      </details>
    </li>)}</ul>}
  </div>
}
