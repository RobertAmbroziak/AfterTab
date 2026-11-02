function configuredLink(value, allowRelative = false) {
  const link = value?.trim()
  if (!link) return null
  if (allowRelative && link.startsWith('/') && !link.startsWith('//')) return link
  try { return new URL(link).protocol === 'https:' ? link : null } catch { return null }
}

export const githubUrl = configuredLink(import.meta.env.VITE_GITHUB_URL)
export const androidApkUrl = configuredLink(import.meta.env.VITE_ANDROID_APK_URL, true)
