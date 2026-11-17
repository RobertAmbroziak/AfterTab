import { readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { loadEnv } from 'vite'

const root = fileURLToPath(new URL('../', import.meta.url))
const environment = { ...loadEnv('production', root, 'VITE_'), ...process.env }
const operatorName = (environment.VITE_OPERATOR_NAME || 'Example operator').trim()
const contactEmail = (environment.VITE_CONTACT_EMAIL || 'contact@example.com').trim()
const hostingProvider = (environment.VITE_HOSTING_PROVIDER || 'your hosting provider').trim()
const emailProvider = (environment.VITE_EMAIL_PROVIDER || 'your email provider').trim()

if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contactEmail)) {
  throw new Error('VITE_CONTACT_EMAIL must be a valid public email address.')
}

function escapeHtml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#39;')
}

const replacements = {
  '{{PUBLIC_OPERATOR_NAME}}': escapeHtml(operatorName),
  '{{PUBLIC_CONTACT_EMAIL}}': escapeHtml(contactEmail),
  '{{PUBLIC_HOSTING_PROVIDER}}': escapeHtml(hostingProvider),
  '{{PUBLIC_EMAIL_PROVIDER}}': escapeHtml(emailProvider),
}

for (const page of ['privacy.html', 'terms.html', 'data-deletion.html']) {
  const path = new URL(`../dist/${page}`, import.meta.url)
  let html = await readFile(path, 'utf8')
  for (const [placeholder, value] of Object.entries(replacements)) html = html.replaceAll(placeholder, value)
  await writeFile(path, html)
}

console.log('Inserted public operator and service-provider details into the legal pages.')
