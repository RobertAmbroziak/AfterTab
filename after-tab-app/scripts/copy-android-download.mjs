import { copyFile, mkdir, stat, writeFile } from 'node:fs/promises'
import { createReadStream } from 'node:fs'
import { createHash } from 'node:crypto'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { loadEnv } from 'vite'

const root = fileURLToPath(new URL('../', import.meta.url))
const environment = loadEnv('production', root, ['VITE_', 'AFTERTAB_'])
const downloadUrl = environment.VITE_ANDROID_APK_URL?.trim()
const sourcePath = environment.AFTERTAB_ANDROID_APK_PATH?.trim()

if (downloadUrl?.startsWith('/')) {
  if (!/^\/downloads\/[a-zA-Z0-9][a-zA-Z0-9._-]*\.apk$/.test(downloadUrl)) {
    throw new Error('Use a versioned APK path under /downloads/, for example /downloads/App-1.0.0.apk.')
  }
  if (!sourcePath) throw new Error('Set AFTERTAB_ANDROID_APK_PATH for the configured local APK download.')
  const source = resolve(root, sourcePath)
  if (!source.toLowerCase().endsWith('.apk') || !(await stat(source)).isFile()) {
    throw new Error('AFTERTAB_ANDROID_APK_PATH must point to an APK file.')
  }
  const destination = resolve(root, `dist${downloadUrl}`)
  await mkdir(dirname(destination), { recursive: true })
  await copyFile(source, destination)
  const hash = createHash('sha256')
  for await (const chunk of createReadStream(destination)) hash.update(chunk)
  await writeFile(`${destination}.sha256`, `${hash.digest('hex')}  ${basename(destination)}\n`)
  console.log(`Prepared Android download: ${downloadUrl} (${(await stat(destination)).size} bytes) and SHA-256 checksum.`)
} else if (sourcePath) {
  throw new Error('Set VITE_ANDROID_APK_URL to a versioned /downloads/*.apk path when providing a local APK.')
}
