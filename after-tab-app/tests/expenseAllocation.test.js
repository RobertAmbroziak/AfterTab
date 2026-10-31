import { test } from 'node:test'
import assert from 'node:assert/strict'
import { calculateAllocation } from '../src/expenseAllocation.js'

test('percentage remainder is assigned only to the clicked participant', () => {
  const base = { robert: '33', magda: '33', kamil: '33' }
  const plain = calculateAllocation('62,50', 'Percentage', base)
  assert.equal(plain.others, 64)
  assert.equal(plain.balanceAmount, 0)
  const first = calculateAllocation('62,50', 'Percentage', base, 'magda')
  assert.equal(first.balanceAmount, 64)
  assert.equal(first.balanceParticipantId, 'magda')
  assert.equal(first.splits.find(x => x.participantId === 'magda').amountMinor, 2126)
  const moved = calculateAllocation('62,50', 'Percentage', base, 'kamil')
  assert.equal(moved.splits.find(x => x.participantId === 'magda').amountMinor, 2062)
  assert.equal(moved.splits.find(x => x.participantId === 'kamil').amountMinor, 2126)
  assert.equal(moved.remainingBeforeBalance, 64)
  assert.equal(moved.others, 0)
  assert.equal(calculateAllocation('62,50', 'Percentage', base, null).others, 64)
})

test('smallest amount share can receive the remainder without changing entered values', () => {
  const base = { robert: '30', magda: '20' }
  const result = calculateAllocation('62,50', 'Amount', base, 'magda')
  assert.deepEqual(base, { robert: '30', magda: '20' })
  assert.equal(result.splits.find(x => x.participantId === 'magda').amountMinor, 3250)
  assert.equal(result.balanceAmount, 1250)
})

test('no remainder means no active balance target; unselected target is invalid', () => {
  const result = calculateAllocation('10', 'Amount', { robert: '10' }, 'robert')
  assert.equal(result.remainingBeforeBalance, 0)
  assert.equal(result.balanceAmount, 0)
  assert.equal(result.balanceParticipantId, null)
  assert(calculateAllocation('10', 'Amount', { robert: '9' }, 'unknown').error)
})
