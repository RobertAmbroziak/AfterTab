import { test } from 'node:test'
import assert from 'node:assert/strict'
import { fetchSessionResponse, SESSION_CHECK_MS, SESSION_TIMEOUT_MESSAGE } from '../src/sessionCheck.js'

function scenario(steps) {
  let clock = 0
  const calls = [], waits = []
  return {
    calls, waits,
    options: {
      deadline: SESSION_CHECK_MS,
      now: () => clock,
      timeoutSignal: ms => ({ timeout: ms }),
      sleep: async ms => { waits.push(ms); clock += ms },
      fetchImpl: async (url, init) => {
        calls.push({ url, init })
        const step = steps.shift() ?? { error: 'TimeoutError', elapsed: init.signal.timeout }
        clock += step.elapsed ?? 0
        if (step.error) throw Object.assign(new Error('connection'), { name: step.error })
        return new Response(null, { status: step.status })
      },
    },
    elapsed: () => clock,
  }
}

test('cold API and database recover automatically after multiple transient failures', async () => {
  const s = scenario([{ error: 'TimeoutError', elapsed: 25000 }, { status: 503 }, { error: 'TypeError' }, { status: 200 }])
  const response = await fetchSessionResponse('/me', { headers: { Authorization: 'Bearer unchanged' } }, s.options)
  assert.equal(response.status, 200)
  assert.equal(s.calls.length, 4)
  assert.equal(s.elapsed(), 31000)
  assert(s.calls.every(call => call.init.headers.Authorization === 'Bearer unchanged'))
})

test('all attempts and pauses share one two-minute deadline', async () => {
  const s = scenario([])
  await assert.rejects(fetchSessionResponse('/me', {}, s.options), { message: SESSION_TIMEOUT_MESSAGE })
  assert.equal(s.elapsed(), 120000)
  assert.equal(s.calls.length, 5)
  assert.equal(s.calls.at(-1).init.signal.timeout, 12000)
})

for (const status of [200, 400, 401, 403, 429]) {
  test(`HTTP ${status} is returned immediately without retry`, async () => {
    const s = scenario([{ status }])
    assert.equal((await fetchSessionResponse('/me', {}, s.options)).status, status)
    assert.equal(s.calls.length, 1)
    assert.equal(s.waits.length, 0)
  })
}

test('one-use exchange is never replayed after an ambiguous server error', async () => {
  const s = scenario([{ status: 503 }])
  const response = await fetchSessionResponse('/exchange', { method: 'POST' }, { ...s.options, retry: false })
  assert.equal(response.status, 503)
  assert.equal(s.calls.length, 1)
  assert.equal(s.calls[0].init.signal.timeout, 120000)
})

test('one-use exchange is never replayed after a network failure', async () => {
  const s = scenario([{ error: 'TypeError' }])
  await assert.rejects(fetchSessionResponse('/exchange', { method: 'POST' }, { ...s.options, retry: false }), { name: 'TypeError' })
  assert.equal(s.calls.length, 1)
})

test('programming errors are not retried as a sleeping server', async () => {
  const s = scenario([{ error: 'SyntaxError' }])
  await assert.rejects(fetchSessionResponse('/me', {}, s.options), { name: 'SyntaxError' })
  assert.equal(s.calls.length, 1)
})

test('already exhausted budget makes no further request', async () => {
  const s = scenario([])
  await assert.rejects(fetchSessionResponse('/me', {}, { ...s.options, deadline: 0 }), { message: SESSION_TIMEOUT_MESSAGE })
  assert.equal(s.calls.length, 0)
})
