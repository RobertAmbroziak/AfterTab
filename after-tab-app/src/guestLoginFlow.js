const prefix = 'aftertab.guestFlow.'
const lifetime = 5 * 60 * 1000
const isSecret = value => typeof value === 'string' && /^[A-Fa-f0-9]{64}$/.test(value)

function parse(value) {
  try { return JSON.parse(value || 'null') } catch { return null }
}

export function rememberGuestFlow({ state, verifier }) {
  if (!isSecret(state) || !isSecret(verifier)) throw new Error('Invalid guest sign-in attempt.')
  for (let i = localStorage.length - 1; i >= 0; i--) {
    const key = localStorage.key(i)
    if (!key?.startsWith(prefix)) continue
    const saved = parse(localStorage.getItem(key))
    if (!saved || saved.expiresAt <= Date.now()) localStorage.removeItem(key)
  }
  localStorage.setItem(prefix + state, JSON.stringify({ state, verifier, expiresAt: Date.now() + lifetime }))
}

export function recoverGuestFlow(state) {
  if (!isSecret(state)) return null
  const key = prefix + state
  const saved = parse(localStorage.getItem(key))
  localStorage.removeItem(key)
  if (!saved || saved.state !== state || !isSecret(saved.verifier)
      || !Number.isFinite(saved.expiresAt) || saved.expiresAt <= Date.now()
      || saved.expiresAt > Date.now() + lifetime) return null
  return { state: saved.state, verifier: saved.verifier }
}

export function forgetGuestFlow(state) {
  if (isSecret(state)) localStorage.removeItem(prefix + state)
}
