const prefix = 'aftertab.externalFlow.'
const sessionKey = 'aftertab.accountFlow'
const lifetime = 5 * 60 * 1000
const isSecret = value => typeof value === 'string' && /^[A-Fa-f0-9]{64}$/.test(value)

function parse(value) {
  try { return JSON.parse(value || 'null') } catch { return null }
}

// Android can return from the Facebook app into another Chrome/PWA window.
// Only a previously initiated, exact-state flow may cross that window boundary.
export function rememberExternalFlow(flow) {
  if (!isSecret(flow.state) || !isSecret(flow.verifier)) throw new Error('Nieprawidłowa próba logowania.')
  for (let i = localStorage.length - 1; i >= 0; i--) {
    const key = localStorage.key(i)
    if (!key?.startsWith(prefix)) continue
    const saved = parse(localStorage.getItem(key))
    if (!saved || saved.expiresAt <= Date.now()) localStorage.removeItem(key)
  }
  const invite = sessionStorage.getItem('aftertab.pendingInvite')
  localStorage.setItem(prefix + flow.state, JSON.stringify({ ...flow, expiresAt: Date.now() + lifetime,
    ...(isSecret(invite) ? { invite } : {}) }))
}

export function recoverExternalFlow(state) {
  if (!isSecret(state)) return null
  const saved = parse(localStorage.getItem(prefix + state))
  if (!saved || saved.state !== state || !isSecret(saved.verifier)
      || !Number.isFinite(saved.expiresAt) || saved.expiresAt <= Date.now() || saved.expiresAt > Date.now() + lifetime) {
    localStorage.removeItem(prefix + state)
    return null
  }
  const flow = { state: saved.state, verifier: saved.verifier }
  sessionStorage.setItem(sessionKey, JSON.stringify(flow))
  if (isSecret(saved.invite)) sessionStorage.setItem('aftertab.pendingInvite', saved.invite)
  return flow
}

export function forgetExternalFlow(state) {
  if (isSecret(state)) localStorage.removeItem(prefix + state)
}
