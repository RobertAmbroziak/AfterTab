import { afterEach, beforeEach, test } from 'node:test'
import assert from 'node:assert/strict'
import { redirectForAuthentication } from '../src/authNavigation.js'

let timers, assigned, win, doc
const previousWindow = Object.getOwnPropertyDescriptor(globalThis, 'window')
const previousDocument = Object.getOwnPropertyDescriptor(globalThis, 'document')

beforeEach(() => {
  timers = new Map()
  assigned = []
  win = new EventTarget()
  doc = new EventTarget()
  doc.visibilityState = 'visible'
  win.location = { assign: url => assigned.push(url) }
  win.setTimeout = callback => { const id = Symbol(); timers.set(id, callback); return id }
  win.clearTimeout = id => timers.delete(id)
  globalThis.window = win
  globalThis.document = doc
})
afterEach(() => {
  if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow)
  else delete globalThis.window
  if (previousDocument) Object.defineProperty(globalThis, 'document', previousDocument)
  else delete globalThis.document
})
function shown(persisted) {
  const event = new Event('pageshow')
  Object.defineProperty(event, 'persisted', { value: persisted })
  win.dispatchEvent(event)
}
function visible(state) { doc.visibilityState = state; doc.dispatchEvent(new Event('visibilitychange')) }

test('return from back/forward cache settles the pending login and clears its timer', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  const rejection = assert.rejects(pending, /przerwane/)
  assert.deepEqual(assigned, ['https://auth.example.test/login'])
  shown(true)
  await rejection
  assert.equal(timers.size, 0)
})

test('mobile visibility return cancels the attempt without requiring a pageshow event', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  const rejection = assert.rejects(pending, /przerwane/)
  visible('hidden')
  visible('visible')
  await rejection
  assert.equal(timers.size, 0)
})

test('focus after pagehide releases a restored login screen', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  const rejection = assert.rejects(pending, /przerwane/)
  win.dispatchEvent(new Event('pagehide'))
  win.dispatchEvent(new Event('focus'))
  await rejection
})

test('normal initial pageshow and focus do not cancel a new login', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  let settled = false
  const handled = pending.catch(() => { settled = true })
  shown(false)
  win.dispatchEvent(new Event('focus'))
  await Promise.resolve()
  assert.equal(settled, false)
  shown(true)
  await handled
})

test('blocked navigation times out while the original screen remains visible', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  const rejection = assert.rejects(pending, /ponowić/)
  for (const expire of [...timers.values()]) expire()
  await rejection
  assert.equal(timers.size, 0)
})

test('timer does not cancel an OAuth login while the provider is still in the foreground', async () => {
  const pending = redirectForAuthentication('https://auth.example.test/login')
  let settled = false
  const handled = pending.catch(() => { settled = true })
  visible('hidden')
  for (const expire of [...timers.values()]) expire()
  await Promise.resolve()
  assert.equal(settled, false)
  visible('visible')
  await handled
})

test('a fresh attempt works after cancellation without retaining previous return handlers', async () => {
  const first = redirectForAuthentication('https://auth.example.test/first')
  const cancelled = assert.rejects(first, /przerwane/)
  shown(true)
  await cancelled
  const second = redirectForAuthentication('https://auth.example.test/second')
  const timedOut = assert.rejects(second, /ponowić/)
  shown(false)
  assert.equal(timers.size, 1)
  for (const expire of [...timers.values()]) expire()
  await timedOut
  assert.equal(assigned.length, 2)
})

test('a synchronous browser navigation failure also releases the attempt', async () => {
  win.location.assign = () => { throw new Error('Navigation blocked') }
  await assert.rejects(redirectForAuthentication('https://auth.example.test/login'), /ponowić/)
  assert.equal(timers.size, 0)
})

test('initial pageshow in a background tab does not prematurely cancel restoration', async () => {
  doc.visibilityState = 'hidden'
  const pending = redirectForAuthentication('https://auth.example.test/login')
  let settled = false
  const handled = pending.catch(() => { settled = true })
  shown(false)
  await Promise.resolve()
  assert.equal(settled, false)
  visible('visible')
  await handled
})
