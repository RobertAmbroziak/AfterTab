# AfterTab na Androida

Projekt Androida jest opakowaniem istniejącej PWA (Trusted Web Activity).
Nie zawiera osobnego interfejsu ani backendu.

| Ustawienie | Wartość |
| --- | --- |
| Nazwa | AfterTab |
| Package ID | `pl.aftertab.app` |
| Adres | `https://aftertab.pl/` |
| Pierwsza wersja | `1.0.0.0`, versionCode `1` |
| Minimalny Android | Android 7.0 / API 24 |
| Target / compile SDK | API 36 |
| Tryb | Standalone / TWA, fallback Custom Tabs |
| Powiadomienia i lokalizacja | Wyłączone |

## Pakiet i test na telefonie

Oryginalna paczka PWABuilder oraz wyodrębnione pliki znajdują się lokalnie w
`artifacts/pwabuilder-1.0.0.0/`, ignorowanym przez Git.

- `AfterTab.apk` — instalacja ręczna na Androidzie do testów.
- `AfterTab.aab` — pakiet do przesłania do Google Play Console; nie instaluje się go bezpośrednio.
- `original-package.zip` — pełna paczka ze źródłami i informacjami o podpisywaniu.
- `signing.keystore` oraz `signing-key-info.txt` — klucz i dane podpisywania.

Zachowaj osobną bezpieczną kopię oryginalnego ZIP-a i danych podpisywania. Samo
ignorowanie przez Git nie szyfruje plików. Nie publikuj klucza, haseł ani całej paczki.

Na telefonie pobierz/przenieś APK, otwórz go i w razie potrzeby zezwól wybranej
aplikacji na instalację z tego źródła. Po instalacji sprawdź: logowanie gościa,
Google i Facebook, powrót po logowaniu, zaproszenie do wydarzenia, wydatki,
przełącznik PL/EN oraz działanie przycisku Wstecz. Testy wykonuj na testowych danych.

## Powiązanie domeny

Publiczny plik `after-tab-app/public/.well-known/assetlinks.json` wiąże domenę
z pakietem Androida i odciskiem certyfikatu podpisującego. Sam odcisk jest publiczny.
Po wdrożeniu plik musi być dostępny pod:

```text
https://aftertab.pl/.well-known/assetlinks.json
```

Oczekiwana odpowiedź: HTTP 200 i JSON, bez uwierzytelniania ani przekierowania.
Brak prawidłowego powiązania może spowodować wyświetlanie paska przeglądarki.

Zweryfikowany SHA-256 certyfikatu pierwszej paczki APK/AAB:

```text
E8:04:29:23:83:13:33:80:EF:EF:55:DF:10:D8:A5:A5:F2:EC:F9:AE:FE:8C:05:5E:2D:DB:4E:61:D5:C1:A4:4D
```

## Google Play i kolejne wersje

### Pobieranie APK z witryny

Frontend obsługuje opcjonalny link `VITE_ANDROID_APK_URL`, np.
`/downloads/AfterTab-1.0.0.0.apk`. `AFTERTAB_ANDROID_APK_PATH` wskazuje prywatny,
lokalny plik źródłowy. Polecenie `npm run build` w `after-tab-app` kopiuje APK do
paczki `dist` i dodaje plik `.sha256`. Azure serwuje APK jako pobierany plik z
właściwym MIME. Nie potrzeba osobnego kontenera Storage ani publikowania APK w Git.
Nigdy nie kopiuj do publicznego katalogu ZIP-a, magazynu kluczy ani haseł.

### Publikacja w sklepie

1. Dokończ rejestrację i weryfikację konta dewelopera firmy.
2. Utwórz aplikację i przygotuj test wewnętrzny z AAB.
3. Po skonfigurowaniu Play App Signing odczytaj z Play Console SHA-256
   **App signing key certificate**. To certyfikat pakietu dostarczanego użytkownikom.
4. Jeśli jest inny, dopisz jego odcisk do `sha256_cert_fingerprints` i ponownie
   opublikuj plik assetlinks, zachowując odcisk do testów ręcznego APK.
5. Sprawdź instalację z Google Play, nie tylko ręczną instalację APK.
6. Uzupełnij opis, grafiki, deklaracje danych, dostęp do aplikacji dla recenzenta
   i wymagane informacje o usuwaniu konta.

Przy ponownym generowaniu paczki w PWABuilder wybierz **Use mine**, użyj tego
samego klucza oraz `pl.aftertab.app`, a `versionCode` zwiększ. Nie generuj za każdym
razem nowego klucza. Zmiany samej PWA publikuje się przez dotychczasowy frontend;
zmiana opakowania Androida wymaga kolejnego APK/AAB.

Dokumentacja: [PWABuilder Android](https://github.com/pwa-builder/PWABuilder/blob/main/docs/builder/android.md).
