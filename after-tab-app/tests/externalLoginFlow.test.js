import { afterEach, beforeEach, test } from 'node:test'
import assert from 'node:assert/strict'
import { rememberExternalFlow, recoverExternalFlow, forgetExternalFlow } from '../src/externalLoginFlow.js'

class Storage {
  data = new Map()
  get length() { return this.data.size }
  key(index) { return [...this.data.keys()][index] ?? null }
  getItem(key) { return this.data.get(key) ?? null }
  setItem(key, value) { this.data.set(key, String(value)) }
  removeItem(key) { this.data.delete(key) }
}
const state = 'A'.repeat(64), verifier = 'B'.repeat(64), invite = 'C'.repeat(64)
const previousLocal = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
const previousSession = Object.getOwnPropertyDescriptor(globalThis, 'sessionStorage')
beforeEach(() => {
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, writable: true, value: new Storage() })
  Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, writable: true, value: new Storage() })
})
afterEach(() => {
  if (previousLocal) Object.defineProperty(globalThis, 'localStorage', previousLocal)
  else delete globalThis.localStorage
  if (previousSession) Object.defineProperty(globalThis, 'sessionStorage', previousSession)
  else delete globalThis.sessionStorage
})

test('new window restores only its previously initiated login and invitation', () => {
  sessionStorage.setItem('aftertab.pendingInvite', invite)
  rememberExternalFlow({ state, verifier })
  globalThis.sessionStorage = new Storage()
  assert.deepEqual(recoverExternalFlow(state), { state, verifier })
  assert.equal(sessionStorage.getItem('aftertab.pendingInvite'), invite)
  assert.deepEqual(JSON.parse(sessionStorage.getItem('aftertab.accountFlow')), { state, verifier })
})

test('foreign state and another browser cannot recover a login', () => {
  rememberExternalFlow({ state, verifier })
  assert.equal(recoverExternalFlow('D'.repeat(64)), null)
  globalThis.localStorage = new Storage()
  assert.equal(recoverExternalFlow(state), null)
})

test('expired or malformed proofs are rejected and removed', () => {
  for (const expiresAt of [Date.now() - 1, null, Date.now() + 600000]) {
    localStorage.setItem('aftertab.externalFlow.' + state, JSON.stringify({ state, verifier, expiresAt }))
    assert.equal(recoverExternalFlow(state), null)
    assert.equal(localStorage.getItem('aftertab.externalFlow.' + state), null)
  }
  localStorage.setItem('aftertab.externalFlow.' + state, 'invalid JSON')
  assert.equal(recoverExternalFlow(state), null)
})

test('parallel attempts do not overwrite each other and completed flow is removed', () => {
  const other = { state: 'D'.repeat(64), verifier: 'E'.repeat(64) }
  rememberExternalFlow({ state, verifier })
  rememberExternalFlow(other)
  forgetExternalFlow(state)
  assert.equal(recoverExternalFlow(state), null)
  assert.deepEqual(recoverExternalFlow(other.state), other)
})

test('arbitrary callback strings cannot be used as flow storage keys', () => {
  assert.equal(recoverExternalFlow('aftertab.session'), null)
  assert.throws(() => rememberExternalFlow({ state: 'invalid', verifier }))
  assert.equal(localStorage.length, 0)
})
