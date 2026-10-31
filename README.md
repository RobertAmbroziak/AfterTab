# AfterTab

AfterTab to aplikacja PWA do dzielenia wspólnych wydatków. Pozwala tworzyć wydarzenia,
zapraszać uczestników, przypisywać koszty i wyliczać, kto komu powinien oddać pieniądze.

**[Otwórz aplikację](https://orange-stone-063c89710.4.azurestaticapps.net)**

## Uruchomienie lokalne

### Wymagania

- Windows i SQL Server Express LocalDB, np. z instalacji Visual Studio 2022.
- .NET SDK 9.0.3xx, zgodnie z `after-tab-api/global.json`.
- Node.js 22.12 lub nowszy oraz npm.
- Przy pracy w Visual Studio: wersja 2022 17.14 z narzędziami ASP.NET.

Repozytorium zawiera dwa projekty:

```text
after-tab-api/    Backend .NET i EF Core, solucja AfterTabApi.sln
after-tab-app/    Frontend React, Vite i PWA
```

### 1. Backend i lokalna baza

Otwórz PowerShell w głównym katalogu repozytorium:

```powershell
cd after-tab-api

$env:ConnectionStrings__AfterTab = 'Server=(localdb)\MSSQLLocalDB;Database=AfterTab;Trusted_Connection=True;TrustServerCertificate=True'
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'

dotnet restore
dotnet tool restore
dotnet dev-certs https --trust
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
dotnet run --project src/AfterTab.API --launch-profile https
```

Za pierwszym razem zaakceptuj zaufanie do lokalnego certyfikatu HTTPS.
Polecenie `database update` tworzy lokalną bazę lub stosuje brakujące migracje;
nie uruchamiaj go przeciwko innej bazie bez sprawdzenia connection stringa.

- Baza: **AfterTab**, serwer **`(localdb)\MSSQLLocalDB`**, logowanie Windows.
- API: **https://localhost:7180**.
- Sprawdzenie uruchomienia API: **https://localhost:7180/api/health**.
  Ten endpoint potwierdza działanie procesu, nie połączenie z bazą.

Pozostaw terminal backendu uruchomiony.

### 2. Frontend

Otwórz drugi PowerShell w głównym katalogu repozytorium:

```powershell
cd after-tab-app

$env:VITE_API_BASE_URL = 'https://localhost:7180'

npm ci
npm run dev -- --port 5173 --strictPort
```

Otwórz **http://localhost:5173**. Frontend połączy się z lokalnym API i bazą LocalDB.
Jeśli port jest zajęty, zatrzymaj wcześniejszą instancję aplikacji.

Przy kolejnych uruchomieniach wystarczy ponownie ustawić powyższe zmienne w nowych
terminalach i uruchomić `dotnet run` oraz `npm run dev`.
Odtwarzanie zależności i migracje powtarzaj po odpowiednich zmianach projektu.
Aplikację zatrzymasz przez **Ctrl+C** w obu terminalach.

Lokalna baza i baza aplikacji internetowej są oddzielne. Dane nie przenoszą się
między nimi automatycznie.

## Instrukcja obsługi

### Konto gościa

1. Wybierz **ZALOGUJ JAKO GOŚĆ**, wpisz imię i zatwierdź.
2. Przy kolejnym wejściu **WRÓĆ JAKO GOŚĆ** przywraca konto zapamiętane w tej przeglądarce.
3. Przycisk konta w nagłówku otwiera menu: **Zmień imię**, **Wyloguj** i **Zapomnij gościa**.

Wylogowanie pozostawia możliwość powrotu do tego samego konta.
**Zapomnij gościa** ją usuwa. Usunięcie danych przeglądarki lub instalacji PWA
także może spowodować utratę dostępu do konta gościa. Samo wpisanie tego samego
imienia nie odzyskuje wcześniejszego konta.

### Wydarzenia i zaproszenia

1. Przejdź do **Wydarzenia** i wybierz **Utwórz wydarzenie**.
2. Wpisz nazwę i opcjonalny opis.
3. Rozwiń **Zaproś znajomych**, a następnie skopiuj lub udostępnij link.
4. Odbiorca otwiera zaproszenie, loguje się i potwierdza **Dołącz do wydarzenia**.

Autor i dołączone osoby widzą wydarzenie na swoich listach.
Formularz **Dołącz linkiem lub kodem** pozwala również wprowadzić zaproszenie ręcznie.
Przeglądarka komunikatora może korzystać z innego konta niż zainstalowana PWA.
Nowy link wygenerowany przez autora unieważnia poprzedni; obecni uczestnicy pozostają.

### Uczestnicy bez konta

Osoba bez telefonu lub konta nie musi dołączać do aplikacji, żeby rozliczenie było pełne.
Autor **otwartego** wydarzenia może rozwinąć **Uczestnicy bez konta**, podać imię
i wybrać **Dodaj uczestnika bez konta**. Nie powstaje konto użytkownika ani sesja.
Taka osoba jest widoczna dla wszystkich uczestników z oznaczeniem **Bez konta**,
kolorowym awatarem i inicjałem. Można przypisywać jej udziały i wskazać jako płacącego.
Wskazanie innego płacącego nadal jest dostępne wyłącznie autorowi wydarzenia.

Autor może zmienić imię uczestnika bez konta. Usunąć można tylko osobę, która nie ma
wydatków, udziałów ani historycznych oznaczeń przelewów. Osoby już uwzględnione
w kosztach trzeba najpierw usunąć z odpowiednich podziałów albo połączyć z kontem.
Zamknięte wydarzenie należy przed takimi zmianami ponownie otworzyć.

#### Połączenie z osobą, która później dołączyła

1. Grzegorz dołącza do wydarzenia zwykłym linkiem zaproszenia.
2. Autor otwiera **Uczestnicy bez konta** i znajduje wcześniejszego Grzegorza bez konta.
3. W polu **Połącz z osobą, która dołączyła** wybiera właściwego uczestnika z kontem.
4. Klika **Połącz uczestników** i potwierdza wybór. Operacji nie można cofnąć w interfejsie.

Pozostaje jeden aktywny uczestnik powiązany z kontem Grzegorza. Zachowuje on udziały
i wydatki obu wpisów; jeżeli oba były w tej samej pozycji, ich kwoty i zadane procenty
są sumowane. Płacący zostają przypisani do połączonego uczestnika, a autorzy wpisów
pozostają bez zmian. Łączenie nie przenosi danych z innych wydarzeń ani nie łączy kont.
Takie samo imię nigdy nie powoduje automatycznego połączenia. Dodanie lub połączenie
uczestnika nie rozdziela automatycznie kwoty **Inni** — autor pozycji nadal poprawia podział.

Uczestnik bez konta nie może sam oznaczać **Wysłane/Odebrane**. Zalogowana druga
strona może oznaczyć wyłącznie własną stronę przelewu; autor wydarzenia nie uzyskuje
prawa do potwierdzania w imieniu innych osób. Po połączeniu konto przejmuje uprawnienia
uczestnika. Prywatna historia oznaczeń wcześniejszych wersji zostaje zachowana;
wcześniejsze płatności nadal nie są automatycznie odliczane od nowego planu.

### Dodawanie wydatków

1. W wydarzeniu wybierz **Dodaj pozycję** i podaj nazwę oraz kwotę w złotych.
2. Wybierz osoby do podziału przez ich kolorowe ikonki.
3. Ustal podział **Procentowo** lub **Kwotowo**. Domyślne procenty są równe
   dla zaznaczonych osób, np. 33%, 50% lub 25%, i można je ręcznie zmienić.
   Dodanie lub usunięcie osoby ponownie ustawia równe procenty.
4. Sprawdź podgląd i naciśnij **Zatwierdź**.

Pozycję może dodać każdy aktywny uczestnik. Autor pozycji jest osobą, która ją
wprowadziła; płacący to osoba, która poniosła koszt. Domyślnie jest to ta sama osoba.
Tylko **autor wydarzenia** może wskazać innego płacącego lub zmienić go później
w szczegółach pozycji przez **Zmień płacącego**.

Nierozdzielona kwota trafia do **Inni**. Znane osoby bez konta warto dodać jako
uczestników bez konta i wskazać je w podziale, zamiast pozostawiać ich koszt w „Inni”.
Mały przycisk **Wyrównaj** przy każdej osobie w podziale przypisuje resztę właśnie
tej osobie — bez automatycznego wyboru największego udziału ani autora wpisu.
Wybrany przycisk jest podświetlony. Kliknięcie przy innej osobie przenosi wyrównanie:
poprzednia osoba wraca do swojego udziału bazowego, a nowa dostaje tę samą resztę.
Ponowne kliknięcie podświetlonego przycisku wycofuje wyrównanie do **Inni**.
Gdy nie ma reszty ani wcześniej przypisanego wyrównania, przyciski są wyszarzone.
Suma udziałów wraz z „Inni” zawsze odpowiada całej kwocie.

Wyrównanie może zmienić tylko **autor wydarzenia lub wskazany płacący** i tylko
w otwartym wydarzeniu. Samo autorstwo pozycji nie daje tego uprawnienia, jeżeli
płacił ktoś inny. Ta zasada jest sprawdzana również w API. Dla płacącego bez konta
czynność wykonuje autor wydarzenia, bez podszywania się pod wirtualnego uczestnika.

W formularzu wybór zostanie zapisany po **Zatwierdź**. W rozwiniętej, już zapisanej
pozycji przyciski przy osobach zapisują zmianę od razu i aktualizują rozliczenie.
Autor wydarzenia i płacący mogą w ten sposób przenieść samo wyrównanie również
w pozycji dodanej przez inną osobę; nie uzyskują przez to prawa do dowolnej edycji
jej udziałów. Gdy formularz pozycji jest otwarty, przyciski przy zapisanych pozycjach
są zablokowane do jego zatwierdzenia lub anulowania.

Szczegóły pozycji są rozwijane. Jej autor może wybrać **Popraw podział**, dopóki
wydarzenie jest otwarte. Dołączenie nowych osób nie zmienia wcześniejszych podziałów
automatycznie.

### Rozliczenie i płatności

Rozliczenie przelicza zapisane pozycje i kompensuje długi. Jeśli jedna osoba jest
winna drugiej 12 zł, a druga pierwszej 3 zł, wynik to jeden przelew 9 zł.
Kwota do oddania lub otrzymania jest widoczna nad listą wydatków.
Na komputerze rozliczenie jest obok wydatków, na telefonie w zakładce **Rozliczenie**.

- **Podgląd** jest bieżącym planem, który może się zmieniać. Dane można odświeżyć
  przyciskiem **Odśwież**; widoczny ekran aktualizuje się także co 15 sekund.
- **Zamknij i rozlicz** zatwierdza plan i blokuje dodawanie pozycji, zmianę podziałów
  oraz dołączanie. Może to zrobić tylko autor wydarzenia, gdy „Inni” wynosi zero
  we wszystkich pozycjach i formularz dodawania/edycji jest zakończony.
- Dopiero w zamkniętym rozliczeniu nadawca może zaznaczyć **Wysłane**, a odbiorca
  **Odebrane**. Każdy zmienia wyłącznie własne oznaczenie. Oba statusy widzą tylko
  strony przelewu; pozostali uczestnicy ich nie widzą.
- Checkboxy są oznaczeniami użytkowników, nie potwierdzeniem bankowym.
- **Otwórz ponownie** pozwala autorowi wydarzenia uzupełnić dane. Kolejne zamknięcie
  tworzy nową wersję rozliczenia. Wcześniejsze oznaczenia pozostają w prywatnej historii
  stron przelewu. Nowy plan **nie odejmuje automatycznie wcześniejszych płatności** —
  sprawdź je przed kolejnym przelewem.

### Instalacja PWA

Przycisk instalacji znajduje się w nagłówku, poza menu konta.
Na Androidzie i obsługiwanych przeglądarkach komputerowych wywołuje instalację,
a gdy przeglądarka jej nie udostępnia, pokazuje instrukcję.
Na iPhonie użyj menu przeglądarki **Udostępnij → Dodaj do ekranu początkowego**.

Do logowania, zapisywania danych i rozliczania potrzebne jest połączenie z API.
Po dłuższej bezczynności pierwsze połączenie z serwerem może potrwać dłużej.
Jeżeli pojawi się błąd oczekiwania, wybierz **Ponów połączenie**.

## Sprawdzenie projektu

W katalogu `after-tab-api`:

```powershell
dotnet test AfterTabApi.sln
```

W katalogu `after-tab-app`:

```powershell
npm run lint
npm run build
```

Lokalne konfiguracje, poświadczenia, bazy, zależności i pliki wynikowe są wykluczone
z Git. Publikacja aplikacji jest osobną czynnością — wysłanie kodu na GitHub
nie uruchamia automatycznego deployu.

### Konto trwałe

Wybierz **ZALOGUJ / UTWÓRZ KONTO**. Możesz zarejestrować e-mail i hasło albo użyć
Google/Facebooka, jeśli administrator skonfigurował danego dostawcę.

- Rejestracja e-mail wymaga imienia i hasła o długości 12–128 znaków. Przed pierwszym
  logowaniem trzeba potwierdzić adres jednorazowym linkiem z wiadomości (ważność 24 godziny).
- **Nie pamiętam hasła** wysyła link ważny 30 minut. Ustawienie nowego hasła unieważnia
  dotychczasowe sesje i pozostałe linki resetu. Link sam w sobie nie loguje użytkownika.
- Po pięciu błędnych hasłach konto ma 15 minut blokady. Formularze mają też limity żądań.
- Nowa osoba z Google/Facebooka potwierdza lub poprawia imię po pierwszym zalogowaniu.
  Odzyskiwanie hasła do Google/Facebooka odbywa się u dostawcy, a nie w AfterTab.
- Zalogowany gość ma w menu **Zachowaj konto / Zaloguj**. Rejestracja nowego konta
  zachowuje jego identyfikator, wydarzenia i wydatki. Pierwsze użycie nowego konta Google
  lub Facebook również może zachować tego gościa. Logowanie do już istniejącego konta
  otwiera dane tamtego konta; aplikacja nie scala dwóch użytkowników automatycznie.
- Konta e-mail, Google i Facebook nie łączą się automatycznie na podstawie identycznego
  adresu e-mail. Zarządzanie dodatkowymi metodami logowania do istniejącego konta
  nie jest częścią tego wdrożenia.
- Trwałe ciasteczko API jest `HttpOnly`, `Secure`, `SameSite=Lax`, ważne do roku.
  Aplikacja przechowuje też sesję API i wskazówkę przywracania w swojej przeglądarce.
  Po wygaśnięciu sesji może przywrócić ją przez krótkie przekierowanie do API.
  **Wyloguj** usuwa zapamiętane logowanie konta trwałego w tej przeglądarce.
  Usunięcie cookies, cofnięcie sesji albo upływ terminu wymaga ponownego logowania.

Lista **Moje wydarzenia** pokazuje zawsze po **10 wydarzeń**, od najnowszych.
Przyciski **Poprzednia / Następna** przełączają strony. Znaczniki **Otwarte / Zamknięte**
oraz inny wygląd zamkniętych wierszy wskazują stan wydarzenia.

## Konfiguracja kont trwałych krok po kroku

### 1. Migracja lokalna i e-maile testowe

W katalogu `after-tab-api` wykonaj migrację jak w instrukcji uruchamiania.
Nowa migracja `PermanentAccountsAndEventPaging` dodaje tabelę jednorazowych operacji
konta, pola blokady logowania oraz indeks listy wydarzeń. Nie usuwa danych.

Do sprawdzenia potwierdzania i resetu bez prawdziwej skrzynki, w terminalu backendu:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Mail__PickupDirectory = Join-Path $PWD 'artifacts\mail'
dotnet run --project src/AfterTab.API --launch-profile https
```

Wiadomości `.eml` pojawią się w ignorowanym przez Git katalogu `artifacts\mail`.
Otwórz je programem pocztowym i użyj linku. Ten tryb działa tylko w Development;
w produkcji trzeba skonfigurować SMTP. Nie publikuj plików `.eml` — zawierają jednorazowe linki.

### 2. Google

Możesz używać tego samego konta administracyjnego Google co do drugiej aplikacji.
Można dopisać adresy przekierowania do istniejącego klienta, ale oddzielny klient OAuth
ułatwia zarządzanie AfterTab. Nie usuwaj adresów drugiej aplikacji.

1. Otwórz [Google Cloud Console](https://console.cloud.google.com/) i wybierz projekt.
2. Otwórz **Google Auth Platform → Clients** (w starszym widoku **APIs & Services → Credentials**).
3. Utwórz klienta **Web application**, np. `AfterTab`, lub edytuj wybranego istniejącego.
4. W **Authorized redirect URIs** dodaj dokładnie:
   - `https://localhost:7180/signin-google`
   - `https://aftertab-api-ambro.azurewebsites.net/signin-google`
5. Skonfiguruj **Branding / Audience** i użytkowników testowych. Nazwa i ekran zgody
   dotyczą projektu Google; zmiana ich we wspólnym projekcie może wpłynąć na drugą aplikację.
6. Zapisz Client ID i Client secret w lokalnych User Secrets backendu:

```powershell
dotnet user-secrets set 'Authentication:Google:ClientId' '<client-id>' --project src/AfterTab.API
dotnet user-secrets set 'Authentication:Google:ClientSecret' '<client-secret>' --project src/AfterTab.API
```

Nie potrzebujesz konfiguracji JavaScript SDK ani kluczy Google we frontendzie.
Logowanie obsługuje backend. Restart API uaktywni przycisk.

[Instrukcja Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-9.0)
i [reguły przekierowań Google](https://developers.google.com/identity/protocols/oauth2/web-server).

### 3. Facebook

Możesz używać tego samego konta administratora Meta. Istniejąca aplikacja Meta może
obsługiwać dodatkowe przekierowania, ale ma wspólną nazwę, ustawienia i identyfikatory
użytkowników. Dla osobnej aplikacji AfterTab najlepiej utworzyć osobną aplikację Meta.

1. Otwórz [Meta for Developers](https://developers.facebook.com/apps/) i wybierz lub utwórz aplikację.
2. Włącz przypadek użycia / produkt **Facebook Login** dla witryny.
3. W **Facebook Login → Settings → Valid OAuth Redirect URIs** dodaj:
   - `https://localhost:7180/signin-facebook`
   - `https://aftertab-api-ambro.azurewebsites.net/signin-facebook`
4. Włącz logowanie przez WWW i HTTPS. Sprawdź dostępność uprawnienia `email`.
   AfterTab identyfikuje użytkownika identyfikatorem dostawcy i działa także bez e-maila Facebooka.
5. Na początku testuj kontem administratora/dewelopera/testera aplikacji Meta.
   Udostępnienie innym osobom może wymagać przełączenia trybu, polityki prywatności,
   informacji o usuwaniu danych i spełnienia wymagań widocznych w panelu Meta.
6. W **Settings → Basic** odczytaj App ID / App secret i zapisz lokalnie:

```powershell
dotnet user-secrets set 'Authentication:Facebook:AppId' '<app-id>' --project src/AfterTab.API
dotnet user-secrets set 'Authentication:Facebook:AppSecret' '<app-secret>' --project src/AfterTab.API
```

Restart API uaktywni przycisk. Callbacki obu dostawców wskazują **API**, nie adres PWA.

[Instrukcja konfiguracji Facebook Login](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/facebook-logins?view=aspnetcore-9.0).

### 4. Wysyłka prawdziwych e-maili

Potrzebujesz usługi SMTP, która obsługuje uwierzytelnianie loginem/hasłem lub kluczem
SMTP i szyfrowane połączenie STARTTLS. Ustaw nadawcę zaakceptowanego przez tę usługę.
Po otrzymaniu danych od dostawcy zapisz je lokalnie:

```powershell
dotnet user-secrets set 'Mail:Host' '<host-smtp>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Port' '587' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Username' '<login-smtp>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:Password' '<haslo-lub-klucz-smtp>' --project src/AfterTab.API
dotnet user-secrets set 'Mail:From' '<zweryfikowany-adres-nadawcy>' --project src/AfterTab.API
```

Nie wpisuj sekretów do repozytorium ani do zmiennych `VITE_*`. Nie wysyłaj ich na czacie.
Tryb PickupDirectory nie jest używany, jeśli podasz `Mail:Host`.

### 5. Przy późniejszym deployu Azure

Ta zmiana kodu nie wykonuje deployu. Gdy zostanie zlecony, poza migracją i publikacją
trzeba ustawić w konfiguracji API odpowiedniki powyższych sekretów:

```text
Authentication__FrontendOrigin=https://orange-stone-063c89710.4.azurestaticapps.net
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__Facebook__AppId
Authentication__Facebook__AppSecret
Mail__Host
Mail__Port
Mail__Username
Mail__Password
Mail__From
```

Zachowaj właściwe `Cors__AllowedOrigins__0`, HTTPS i konfigurację forwarded headers
App Service — dostawca musi dostać callback HTTPS. Klucze ASP.NET Data Protection
muszą przetrwać restart API, aby trwające logowanie OAuth mogło się zakończyć.
Sekrety ustawiaj w Azure, nie w kodzie ani w komendach przechowywanych w repozytorium.

### 6. Sprawdzenie po konfiguracji

1. Zarejestruj e-mail; przed potwierdzeniem logowanie ma być odrzucone.
2. Potwierdź wiadomość i zaloguj się. Zamknij i otwórz aplikację — sesja ma wrócić.
3. Wyloguj się — aplikacja nie powinna automatycznie logować ponownie.
4. Użyj odzyskiwania hasła; po zmianie stare hasło i poprzednie sesje mają nie działać.
5. Sprawdź Google i Facebook oraz anulowanie zgody dostawcy.
6. Zaloguj się na innym urządzeniu tym samym kontem — lista wydarzeń ma być ta sama.
7. Jako gość utwórz wydarzenie, potem zarejestruj nowe konto z menu gościa i sprawdź,
   czy wydarzenie pozostało. Nie używaj tu istniejącego konta z innymi wydarzeniami.

Testy automatyczne nie zastępują logowania prawdziwym kontem Google/Facebook ani testu
dostarczania e-maili z wybranej usługi SMTP. Te sprawdzenia wymagają Twojej konfiguracji.

### Gmail na start

1. W koncie Google otwórz **Bezpieczeństwo → Weryfikacja dwuetapowa** i włącz ją.
2. Otwórz [Hasła aplikacji](https://myaccount.google.com/apppasswords), utwórz hasło
   `AfterTab` i zapisz je w User Secrets. Dostępność tej funkcji zależy od ustawień konta.
3. Ustaw `Mail:Host=smtp.gmail.com`, `Mail:Port=587`, a `Mail:Username` i `Mail:From`
   na pełny adres Gmail. `Mail:Password` to wygenerowane hasło aplikacji, nie zwykłe hasło Google.
4. Uruchom ponownie API i przetestuj rejestrację oraz reset na swojej skrzynce.

Ten wariant korzysta ze skrzynki Gmail i jej limitów. Dla większego ruchu przejdź
na usługę do wiadomości transakcyjnych.

[Wymagania haseł aplikacji Google](https://support.google.com/mail/answer/185833)
i [serwer SMTP Gmail](https://developers.google.com/workspace/gmail/imap/imap-smtp).

### Azure Communication Services Email

Rozliczenie jest za użycie. Dokumentacja podaje przykładowo **0,00025 USD za wiadomość**
oraz **0,00012 USD za MB danych**, czyli około **0,25 USD za 1000 krótkich maili** plus
dane, podatki i przeliczenie waluty. To szacunek z dokumentacji, nie gwarantowana oferta;
sprawdź cenę dla subskrypcji przed utworzeniem zasobów.

1. W Azure utwórz **Email Communication Services** i dodaj domenę zarządzaną Azure
   albo zweryfikuj własną domenę. Domena Azure pozwala zacząć bez kupowania domeny.
2. Utwórz **Communication Services** i połącz ją z tą domeną e-mail.
3. Zgodnie z instrukcją SMTP zarejestruj aplikację Microsoft Entra, nadaj jej uprawnienia
   do wysyłki w tym zasobie i utwórz sekret klienta.
4. W **Communication Services → SMTP Usernames** utwórz login powiązany z aplikacją Entra.
5. Ustaw `Mail:Host=smtp.azurecomm.net`, `Mail:Port=587`, `Mail:Username` na utworzony
   login SMTP, `Mail:Password` na sekret aplikacji Entra, `Mail:From` na adres nadawcy
   widoczny w skonfigurowanej domenie e-mail.
6. Sprawdź dostarczenie maila i spam. Pilnuj terminu ważności sekretu Entra.

Regiony/dostępne obszary danych sprawdź dla usługi e-mail osobno; lokalizacja API
w Poland Central nie oznacza automatycznie przetwarzania poczty w tym samym regionie.

[Cennik przykładowy](https://learn.microsoft.com/en-us/azure/communication-services/concepts/email-pricing)
i [konfiguracja SMTP w Azure](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication).


## Publiczne strony informacyjne AfterTab

Administrator wskazany przez właściciela: **AmbroDev Robert Ambroziak**.
Kontakt: **amb77ro@gmail.com**.
Strony są statyczne, dostępne bez JavaScript, konta i połączenia z API:

- `/privacy.html` — polityka prywatności.
- `/data-deletion.html` — instrukcja zgłoszenia usunięcia danych e-mailem.
- `/terms.html` — zasady korzystania.

W Meta → App settings → Basic po opublikowaniu tych plików wpisz:

```text
Privacy policy URL: https://orange-stone-063c89710.4.azurestaticapps.net/privacy.html
Terms of Service URL: https://orange-stone-063c89710.4.azurestaticapps.net/terms.html
User data deletion → Data deletion instructions URL:
https://orange-stone-063c89710.4.azurestaticapps.net/data-deletion.html
```

Instrukcja usuwania nie jest callbackiem API. Żądania trzeba obsługiwać ręcznie,
potwierdzając własność konta i usuwając lub anonimizując odpowiednie dane.
Odłączenie Facebooka/Google, wylogowanie i „Zapomnij gościa” nie realizują same
usunięcia danych serwerowych. Strony opisują obecny stan funkcji; przy zmianie
retencji, dostawców, opłat lub sposobu usuwania trzeba zaktualizować ich treść.
Sprawdź i zatwierdź treść przed publikacją. Sam build nie udostępnia stron na Azure.

## Model uczestników i migracja lokalna

`EventParticipants.Id` jest identyfikatorem uczestnika rozliczenia. `UserId` jest
opcjonalnym powiązaniem z kontem; `DisplayName` przechowuje imię osoby bez konta.
Uczestnicy z kontem wyświetlają aktualne imię z `Users`. Łączenie zachowuje identyfikator
uczestnika bez konta, a wycofany wiersz staje się aliasem `MergedIntoParticipantId`,
żeby zachować prywatną historię płatności. Nie tworzymy fikcyjnych rekordów `Users`.

Migracja `VirtualEventParticipants` zmienia `UserId` uczestnika na opcjonalny,
uzupełnia dotychczasowe imiona i zamienia identyfikatory kont w historycznych
`TransferConfirmations` na identyfikatory uczestników tego samego wydarzenia.
Istniejące wydarzenia, kwoty i oznaczenia przelewów pozostają zachowane.
Migracja w dół odmawia wykonania, jeżeli istnieją osoby bez konta lub aliasy po połączeniu,
aby nie usuwać ich danych finansowych. Przed aktualizacją rzeczywistej bazy wykonaj kopię.

Po pobraniu tej wersji uruchom lokalne `dotnet ef database update` z instrukcji powyżej.
Do testów używaj lokalnej LocalDB i frontendu z `VITE_API_BASE_URL=https://localhost:7180`.
Frontend oraz API muszą pochodzić z tej samej wersji: pola rozliczeń odnoszą się teraz
do uczestników (`participantId`, `paidByParticipantId`, `fromParticipantId`, `toParticipantId`),
a nie kont. `authorUserId` nadal oznacza konto autora wpisu. Aktualizacja frontend/API
na Azure wymaga osobnego, świadomego wdrożenia i migracji bazy; push kodu nie zastępuje migracji.

Zarządzanie osobami bez konta (tylko autor otwartego wydarzenia):

- `POST /api/events/{eventId}/participants/virtual` — `{ "displayName": "Grzegorz" }`.
- `PUT /api/events/{eventId}/participants/{participantId}` — zmiana imienia osoby bez konta.
- `DELETE /api/events/{eventId}/participants/{participantId}` — wycofanie nieużywanej osoby bez konta.
- `POST /api/events/{eventId}/participants/{participantId}/link` — `{ "targetParticipantId": "..." }`;
  cel musi być aktywnym uczestnikiem z kontem w tym samym wydarzeniu.

Testy: `dotnet test AfterTabApi.sln` w backendzie; `npm test`, `npm run lint` i `npm run build`
w frontendzie. Testy `VirtualParticipantTests` sprawdzają kwoty, łączenie, uprawnienia,
blokadę zamkniętych wydarzeń i prywatną historię. Nie łączymy osób automatycznie po imieniu.

### Jawne wyrównanie na uczestnika

Migracja `ExplicitParticipantBalance` dodaje do `Expenses` opcjonalne
`BalanceParticipantId` i kwotę `BalanceAmountMinor`. Dzięki temu wyrównanie można
przenosić po zapisie bez zgadywania poprzednich udziałów. Istniejące kwoty i podziały
nie są przeliczane przez migrację. Dla starych pozycji wyrównanych wcześniejszym
mechanizmem nie znamy osoby ani kwoty korekty — dostają `null` i `0`, zachowując
dotychczasowe finalne udziały oraz znacznik `Balanced`. Aby wybrać nowe wyrównanie
w takiej pozycji, jej autor musi najpierw poprawić bazowy podział; nowe wyrównanie
wskazuje następnie płacący albo autor wydarzenia.

Tworzenie i edycja podziału przyjmują `balanceParticipantId` zamiast automatycznego
wyboru. Pozostawione pole `balance=true` bez wybranej osoby jest odrzucane.
`PUT /api/events/{eventId}/expenses/{expenseId}/balance` przyjmuje
`{ "participantId": "...", "rowVersion": "..." }`; `participantId: null` wycofuje korektę.
Osoba musi być już wybrana w podziale tej pozycji. Stare formularze są chronione
przez `rowVersion`, a zamknięte wydarzenie nie pozwala zmienić wyrównania.
Łączenie uczestnika bez konta przenosi także wskazanie odbiorcy wyrównania.

Przed lokalnym uruchomieniem zastosuj brakującą migrację standardowym poleceniem
`dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API`.
Ta zmiana wymaga zgodnych wersji API i frontendu oraz migracji bazy; nie wprowadza
automatycznego deployu ani zmian w Azure.
