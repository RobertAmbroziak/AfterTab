export function money(minor) { return (minor / 100).toLocaleString('pl-PL', { style: 'currency', currency: 'PLN' }) }
export function equalPercentages(ids) {
  if (!ids.length) return {}
  return Object.fromEntries(ids.map(id => [id, String(Math.floor(100 / ids.length))]))
}
export function parseHundredths(value) {
  const text = String(value).trim().replace(',', '.')
  if (!/^\d{1,10}(\.\d{1,2})?$/.test(text)) return NaN
  const [whole, fraction = ''] = text.split('.')
  return Number(whole) * 100 + Number(fraction.padEnd(2, '0'))
}
export function calculateAllocation(amount, method, shares, authorId, balance) {
  const total = parseHundredths(amount)
  if (!Number.isSafeInteger(total) || total <= 0 || total > 100_000_000_000) return { error: 'Podaj dodatnią kwotę (najwyżej dwa miejsca po przecinku), do 1 000 000 000 zł.' }
  const rows = Object.entries(shares).map(([participantId, value]) => ({ participantId, value: parseHundredths(value || '0') }))
  if (rows.some(row => !Number.isSafeInteger(row.value) || row.value < 0 || row.value > (method === 'Percentage' ? 10000 : 100_000_000_000))) return { error: 'Podaj poprawne udziały z najwyżej dwoma miejscami po przecinku.' }
  if (method === 'Percentage' && rows.reduce((sum, row) => sum + row.value, 0) > 10000) return { error: 'Suma procentów nie może przekraczać 100%.' }
  const splits = rows.map(row => ({ participantId: row.participantId, amountMinor: method === 'Percentage' ? Math.floor(total * row.value / 10000) : row.value }))
  let others = total - splits.reduce((sum, row) => sum + row.amountMinor, 0)
  if (others < 0) return { error: 'Suma podziału przekracza kwotę pozycji.' }
  if (balance && others > 0) {
    const maximum = Math.max(0, ...splits.map(row => row.amountMinor))
    const largest = splits.filter(row => row.amountMinor === maximum)
    const targetId = largest.length === 1 ? largest[0].participantId : authorId
    const target = splits.find(row => row.participantId === targetId)
    if (target) target.amountMinor += others; else splits.push({ participantId: targetId, amountMinor: others })
    others = 0
  }
  return { splits, others, total }
}
