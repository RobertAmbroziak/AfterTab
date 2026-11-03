# AfterTab on Android

**[Polski](README.md)** | **English**

The existing PWA can be packaged for Android as a Trusted Web Activity (TWA),
for example with [PWABuilder](https://www.pwabuilder.com/). No separate interface
or backend is required. Each deployment uses its own domain, package ID and
certificate, configured outside Git.

## Preparing a package

1. Deploy your PWA at a public HTTPS URL and check its manifest and icons.
2. Select the Android package in PWABuilder. Set your domain, app name and unique
   Package ID. `com.example.expenses` is an example only; replace it with your own.
3. Store the APK, AAB and signing credentials securely. Install the APK manually;
   upload the AAB to Google Play Console.
4. Keep the same Package ID and signing key for updates and increase `versionCode`.
   Do not generate a new key for each release.

The generator's complete ZIP may contain a private key and passwords. Do not
publish it. Keystores, signing credentials, APK/AAB files and `artifacts/`
directories are ignored by Git. Ignoring files does not provide encryption
or replace a secure backup.

## Associating your domain

Set the following in the ignored `after-tab-app/.env.production.local` file
or in your build environment:

```dotenv
AFTERTAB_ANDROID_PACKAGE_ID=com.example.expenses
AFTERTAB_ANDROID_CERT_SHA256=<SHA-256 fingerprint of your app signing certificate>
```

Replace both values. A fingerprint contains 32 hexadecimal byte pairs separated
by colons. Separate multiple fingerprints with commas. Provide a **certificate
fingerprint**, never a private key or password.

Running `npm run build` in `after-tab-app` generates
`dist/.well-known/assetlinks.json`. When both settings are absent, no file is
generated; supplying only one setting fails the build. Deployment-specific
values are not stored in a tracked public file. The generated JSON is
intentionally public and must be served at:

```text
https://example.com/.well-known/assetlinks.json
```

Use your own domain. Verify HTTP 200, the `application/json` content type and
no redirects or sign-in requirement. A valid association allows the TWA to open
without a browser bar. The certificate fingerprint is a public identifier,
not a secret.

## Optional APK download from your website

You can also configure:

```dotenv
VITE_ANDROID_APK_URL=/downloads/App-1.0.0.apk
AFTERTAB_ANDROID_APK_PATH=<local path to your APK>
```

The source path can be absolute or relative to `after-tab-app`. The build
copies only the APK into `dist/downloads/` and adds a `.sha256` checksum.
Use a versioned file name. Leaving the URL blank hides the footer download link.
No separate storage service is needed if your hosting supports APK files.
Never put the original ZIP, keystore or passwords in public directories.

## Testing and Google Play

After installing the APK, check sign-in, return from Google/Facebook, invitations,
expenses, language selection and the Back button. Use test data. Before
publication, prepare your listing, graphics, policies, data declarations and
reviewer access.

If you use Play App Signing, find the SHA-256 fingerprint of the **App signing
key certificate** in Play Console. It can differ from your local APK certificate
or upload key certificate. Add the correct fingerprint to the configuration.
Keep the local fingerprint too if you still distribute an APK signed locally.
Rebuild and deploy the website, then test an installation from Google Play.

Deploy PWA changes through the frontend. Changes to the Android wrapper require
a new APK/AAB package.

Documentation: [PWABuilder Android](https://github.com/pwa-builder/PWABuilder/blob/main/docs/builder/android.md),
[Trusted Web Activities](https://developer.android.com/develop/ui/views/layout/webapps/guide-trusted-web-activities-version2).
