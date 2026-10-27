const pendingKey = 'aftertab.pendingInvite'
const invitePattern = /^#\/join\/([a-f\d]{64})$/i

export function captureInvitation() {
  const match = location.hash.match(invitePattern)
  if (match) sessionStorage.setItem(pendingKey, match[1].toUpperCase())
}

export function readRoute() {
  const invite = location.hash.match(invitePattern)
  if (invite) return { kind: 'invite', token: invite[1].toUpperCase() }
  const event = location.hash.match(/^#\/events\/([a-f\d-]{36})$/i)
  if (event) return { kind: 'event', id: event[1] }
  const pending = sessionStorage.getItem(pendingKey)
  return pending ? { kind: 'invite', token: pending } : { kind: 'list' }
}

export function showEvents() {
  sessionStorage.removeItem(pendingKey)
  location.hash = '#/'
}

export function showEvent(id) {
  sessionStorage.removeItem(pendingKey)
  location.hash = `#/events/${id}`
}

export function openInvitation(input) {
  const value = input.trim()
  let token = /^[a-f\d]{64}$/i.test(value) ? value : null
  if (!token) {
    try {
      const url = new URL(value)
      if (['https:', 'http:'].includes(url.protocol)) token = url.hash.match(invitePattern)?.[1]
    } catch { /* A pasted value is never followed as a URL. */ }
  }
  if (!token) throw new Error('Wklej pełny link zaproszenia albo jego 64-znakowy kod.')
  sessionStorage.setItem(pendingKey, token.toUpperCase())
  location.hash = `#/join/${token.toUpperCase()}`
}

export function invitationLink(token) {
  return `${location.origin}/#/join/${token}`
}
