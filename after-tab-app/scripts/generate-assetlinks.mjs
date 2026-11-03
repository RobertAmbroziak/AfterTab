import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { loadEnv } from 'vite'

const root = fileURLToPath(new URL('../', import.meta.url))
const environment = loadEnv('production', root, 'AFTERTAB_')
const packageName = environment.AFTERTAB_ANDROID_PACKAGE_ID?.trim()
const fingerprints = (environment.AFTERTAB_ANDROID_CERT_SHA256 ?? '')
  .split(',').map(value => value.trim()).filter(Boolean)

if (packageName || fingerprints.length) {
  if (!packageName || !fingerprints.length) {
    throw new Error('Set both AFTERTAB_ANDROID_PACKAGE_ID and AFTERTAB_ANDROID_CERT_SHA256.')
  }
  if (!/^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+$/.test(packageName)) {
    throw new Error('AFTERTAB_ANDROID_PACKAGE_ID must be a valid Android package name.')
  }
  if (fingerprints.some(value => !/^(?:[A-Fa-f0-9]{2}:){31}[A-Fa-f0-9]{2}$/.test(value))) {
    throw new Error('AFTERTAB_ANDROID_CERT_SHA256 must contain comma-separated SHA-256 certificate fingerprints.')
  }
  const directory = new URL('../dist/.well-known/', import.meta.url)
  await mkdir(directory, { recursive: true })
  await writeFile(new URL('assetlinks.json', directory), JSON.stringify([{
    relation: ['delegate_permission/common.handle_all_urls'],
    target: {
      namespace: 'android_app',
      package_name: packageName,
      sha256_cert_fingerprints: [...new Set(fingerprints.map(value => value.toUpperCase()))],
    },
  }], null, 2) + '\n')
  console.log('Prepared Android Digital Asset Links from deployment configuration.')
}
