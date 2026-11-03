# AfterTab na Androida

**Polski** | **[English](README.en.md)**

Istniejącą PWA można opakować jako aplikację Android za pomocą Trusted Web Activity
(TWA), np. przez [PWABuilder](https://www.pwabuilder.com/). Nie wymaga to osobnego
interfejsu ani backendu. Każde wdrożenie używa własnej domeny, identyfikatora
pakietu i certyfikatu, konfigurowanych poza Gitem.

## Przygotowanie paczki

1. Wdróż własną PWA pod publicznym adresem HTTPS i sprawdź manifest oraz ikony.
2. W PWABuilder wybierz pakiet Android. Ustaw własną domenę, nazwę i unikalny
   Package ID. `com.example.expenses` to tylko przykład — zastąp go własnym.
3. Zapisz APK, AAB i dane podpisywania w bezpiecznym miejscu. APK służy do
   instalacji ręcznej, a AAB do przesłania do Google Play Console.
4. Przy aktualizacjach zachowaj ten sam Package ID i klucz podpisujący,
   zwiększając `versionCode`. Nie generuj nowego klucza przy każdej aktualizacji.

Cały ZIP generatora może zawierać klucz prywatny i hasła — nie publikuj go.
Magazyny kluczy, dane podpisywania, APK/AAB i katalogi `artifacts/` są ignorowane
przez Git. Ignorowanie nie szyfruje danych ani nie zastępuje bezpiecznej kopii.

## Powiązanie własnej domeny

W ignorowanym pliku `after-tab-app/.env.production.local` lub w środowisku
budowania ustaw:

```dotenv
AFTERTAB_ANDROID_PACKAGE_ID=com.example.expenses
AFTERTAB_ANDROID_CERT_SHA256=<SHA-256 certyfikatu podpisującego Twoją aplikację>
```

Zastąp obie wartości własnymi. Odcisk zawiera 32 pary cyfr szesnastkowych
rozdzielone dwukropkami. Wiele odcisków oddziel przecinkami. Podaj odcisk
**certyfikatu**, nigdy klucz prywatny ani hasło.

`npm run build` w `after-tab-app` generuje `dist/.well-known/assetlinks.json`.
Bez obu ustawień plik nie powstaje; ustawienie tylko jednego powoduje błąd.
Konfiguracja konkretnej instancji nie jest przechowywana w śledzonym pliku
publicznym. Wynikowy JSON jest celowo publiczny i musi być dostępny pod adresem:

```text
https://example.com/.well-known/assetlinks.json
```

Użyj własnej domeny. Sprawdź HTTP 200, typ `application/json` i brak przekierowania
lub logowania. Prawidłowe powiązanie pozwala uruchomić TWA bez paska przeglądarki.
Odcisk certyfikatu jest publicznym identyfikatorem, nie sekretem.

## Opcjonalne pobieranie APK z witryny

W tej samej konfiguracji budowania możesz ustawić:

```dotenv
VITE_ANDROID_APK_URL=/downloads/App-1.0.0.apk
AFTERTAB_ANDROID_APK_PATH=<lokalna ścieżka do własnego APK>
```

Ścieżka źródłowa może być względna wobec `after-tab-app` lub bezwzględna.
Budowanie kopiuje wyłącznie APK do `dist/downloads/` i dodaje sumę `.sha256`.
Nazwa powinna zawierać wersję. Brak URL ukrywa link w stopce. Nie potrzeba osobnej
usługi przechowywania, jeśli hosting obsługuje APK. Nigdy nie umieszczaj ZIP-a,
magazynu kluczy ani haseł w katalogach publicznych.

## Testy i Google Play

Po instalacji sprawdź logowanie, powrót z Google/Facebooka, zaproszenia, wydatki,
przełączanie języka i przycisk Wstecz. Używaj danych testowych. Przed publikacją
przygotuj opis, grafiki, polityki, deklaracje danych i dostęp dla recenzenta.

Przy Play App Signing odczytaj w Play Console SHA-256 **App signing key
certificate**. Może różnić się od certyfikatu lokalnego APK lub klucza do
przesyłania paczek. Dodaj właściwy odcisk do konfiguracji, zachowując lokalny,
jeśli nadal udostępniasz APK podpisany lokalnie. Zbuduj i wdróż witrynę ponownie,
a następnie sprawdź instalację z Google Play.

Zmiany PWA wdraża się przez frontend. Zmiany opakowania Androida wymagają nowej
paczki APK/AAB.

Dokumentacja: [PWABuilder Android](https://github.com/pwa-builder/PWABuilder/blob/main/docs/builder/android.md),
[Trusted Web Activities](https://developer.android.com/develop/ui/views/layout/webapps/guide-trusted-web-activities-version2).
