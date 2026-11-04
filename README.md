# AfterTab

---

<div align="center">

https://github.com/user-attachments/assets/83582d70-4d2b-4677-ae0a-f56cb08d18b1

</div>

**Polski** | **[English](README.en.md)**

Instrukcja pakowania własnego wdrożenia na Androida: [Android](after-tab-android/README.md).

AfterTab to aplikacja PWA do dzielenia wspólnych wydatków. Pozwala tworzyć wydarzenia,
zapraszać uczestników, przypisywać koszty i wyliczać, kto komu powinien oddać pieniądze.

Uruchom własną instancję według instrukcji poniżej. Domeny `example.com`,
adresy e-mail `@example.com` i wartości w `<...>` są przykładami do zastąpienia.

## Język aplikacji

AfterTab obsługuje polski i angielski. Przełącznik **PL / EN** znajduje się w nagłówku.
Przy pierwszej wizycie główny język przeglądarki wybiera PL dla polskiego, a EN dla
pozostałych języków; przy braku informacji używany jest PL. Ręczny wybór jest
zapamiętywany na tym urządzeniu i ma pierwszeństwo przy kolejnych wizytach.
Zmiana języka nie wylogowuje, nie usuwa zaproszenia i nie czyści formularzy.
Kwoty pozostają w PLN; format liczb i dat dopasowuje się do języka.

Strony prywatności, usuwania danych i zasad korzystania mają obie wersje pod
dotychczasowymi adresami. Parametr `?lang=pl` lub `?lang=en` wskazuje wersję.
Bez JavaScript obie wersje pozostają dostępne. Nie zmieniamy callbacków OAuth ani
adresów stron w panelach Google i Meta przy samym przełączaniu języka.

Konfigurację narzędzia migracyjnego poza kodem opisuje
[instrukcja wdrożeniowa](after-tab-api/tools/AfterTab.Deployment/README.md).
Nie zapisuj connection stringa narzędzia ani tokenów w repozytorium.

## Uruchomienie lokalne

GitHub Actions uruchamia workflow **Develop CI** po każdym pushu na `develop`:
instaluje zależności, buduje backend i frontend oraz wykonuje testy jednostkowe
(.NET i Node.js). Wyniki i logi są w zakładce **Actions** repozytorium.
Workflow nie uruchamia się dla `main`, nie wykonuje testów integracyjnych,
nie potrzebuje bazy i nie publikuje aplikacji.

Opcjonalne wdrożenie frontend + API + PostgreSQL na mikr.us/VPS:
[Docker Compose](deploy/mikrus/README.md). Azure jest opcjonalnym wariantem hostingu.

Linki stopki można ustawić przez `VITE_GITHUB_URL` i `VITE_ANDROID_APK_URL`.
Puste wartości ukrywają odpowiedni link, dzięki czemu inne wdrożenia nie muszą
wskazywać repozytorium ani plików autora. Ustawienia produkcyjne przechowuj poza
Git, np. w `after-tab-app/.env.production.local`.

APK można udostępnić na własnym hostingu frontendu, np. Azure Static Web Apps. Dla adresu
`/downloads/App-1.0.0.apk` ustaw `AFTERTAB_ANDROID_APK_PATH` na lokalny plik APK
(ścieżka względna względem `after-tab-app` albo bezwzględna). `npm run build`
kopiuje tylko APK do `dist/downloads`, generuje sumę SHA-256 i sprawdza obecność
pliku. Pliki binarne nie trafiają do Git ani do cache offline PWA. Używaj nowego
adresu pliku dla każdej wersji: katalog pobierania ma nagłówki trwałego cache.
Pełny ZIP z PWABuilder i klucze podpisywania pozostają prywatne.

### Wymagania

- SQL Server albo PostgreSQL. Poniższy domyślny przykład używa SQL Server Express LocalDB na Windows; PostgreSQL pozwala uruchomić backend również na Linuxie.
- .NET SDK 9.0.3xx, zgodnie z `after-tab-api/global.json`.
- Node.js 22.12 lub nowszy oraz npm.
- Przy pracy w Visual Studio: wersja 2022 17.14 z narzędziami ASP.NET.

Repozytorium zawiera dwa projekty:

```text
after-tab-api/    Backend .NET i EF Core, solucja AfterTabApi.sln
after-tab-app/    Frontend React, Vite i PWA
```

### 1. Backend i lokalna baza

Ta wersja wymaga **pustej bazy**: przebudowa kluczy nie przenosi dotychczasowych danych.
Przed aktualizacją istniejącej instalacji przeczytaj [reset schematu i klucze](after-tab-api/docs/database.md).

Otwórz PowerShell w głównym katalogu repozytorium:

```powershell
cd after-tab-api

$env:Database__Provider = 'SqlServer'
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

Logowanie błędów, rotacja plików i ustawienia hostingu: [instrukcja logowania backendu](after-tab-api/docs/logging.md).

### Domeny własnego wdrożenia

Poniższe przykłady używają dwóch publicznych adresów HTTPS:

| Przeznaczenie | Przykład do zastąpienia |
| --- | --- |
| Frontend / PWA | `https://app.example.com` |
| API | `https://api.example.com` |
| Nadawca poczty zaakceptowany przez własną usługę SMTP | `no-reply@example.com` |
| Kontakt administratora własnej instancji | `support@example.com` |

W konfiguracji **API** ustaw `Authentication__FrontendOrigin` i
`Cors__AllowedOrigins__0` na publiczny adres frontendu, bez końcowego `/`.
Przy budowaniu **frontendu** ustaw `VITE_API_BASE_URL` na publiczny adres API,
bez końcowego `/api` — klient dopisuje ścieżki endpointów.
Nazwy zasobów Azure, subskrypcję, bazę, konta SMTP i klientów OAuth wybierasz we własnych usługach.

W wariancie jednej domeny, np. Docker Compose, frontend i publiczne wejście API
mogą używać `https://app.example.com`. Pozostaw wtedy `VITE_API_BASE_URL` puste
(tak robi Compose) — klient sam dodaje `/api`. Callbacki OAuth nadal muszą trafiać do backendu:
`https://app.example.com/signin-google` i `https://app.example.com/signin-facebook`,
a nie pod `/api/signin-*`. Reverse proxy przekazuje te ścieżki do API.
Adresy polityk pozostają adresami frontendu. W panelach Google/Meta podaj
rzeczywiste adresy swojego wdrożenia, nie domeny przykładowe.

### Wybór bazy przy wdrożeniu: SQL Server lub PostgreSQL

Ten sam build API obsługuje oba silniki. W `appsettings.json` domyślne
`Database:Provider` to `SqlServer`. Konfiguracja wyboru silnika pozostaje taka sama;
ta wersja wymaga jednak utworzenia pustego schematu również na Azure SQL.
Na serwerze lub w pipeline ustaw poza Git:

| Ustawienie środowiska | SQL Server / Azure SQL | PostgreSQL |
| --- | --- | --- |
| `Database__Provider` | `SqlServer` | `PostgreSql` |
| `ConnectionStrings__AfterTab` | Connection string SQL Server | Connection string PostgreSQL |

Zmiana wymaga ponownego uruchomienia API. Nieznana nazwa dostawcy przerywa start;
aplikacja nie przełącza się automatycznie na inny silnik. Wybór dotyczy jednego
silnika na uruchomiony proces i nie kopiuje danych pomiędzy bazami.

Dla PostgreSQL uruchom wcześniej własny serwer i utwórz użytkownika oraz bazę.
Następnie w katalogu `after-tab-api` (po `dotnet restore` i `dotnet tool restore`):

```powershell
$env:Database__Provider = 'PostgreSql'
$env:ConnectionStrings__AfterTab = 'Host=localhost;Port=5432;Database=AfterTab;Username=aftertab;Password=<haslo>'
dotnet ef database update --project src/AfterTab.DAL.Migrations.PostgreSql --startup-project src/AfterTab.API
dotnet run --project src/AfterTab.API --launch-profile https
```

`<haslo>` to placeholder. Prawdziwe dane dostępowe przechowuj w konfiguracji
środowiska lub magazynie sekretów. W produkcji skonfiguruj TLS zgodnie z
wymaganiami dostawcy bazy i stosuj migracje kontem z uprawnieniami do schematu.
Konto działającego API nie musi mieć uprawnień do tworzenia tabel.

Migracje SQL Server są w `src/AfterTab.DAL.EF/Migrations`, a PostgreSQL w osobnym projekcie
`src/AfterTab.DAL.Migrations.PostgreSql`. Oba silniki mają nową migrację początkową
`InitialNumericKeys`, która zastępuje wcześniejszą historię. Wymaga pustej bazy;
nie konwertuje danych ze starego modelu GUID. Każda kolejna zmiana modelu wymaga migracji
dla obu silników, np.:

```powershell
dotnet ef migrations add NazwaZmiany --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API -- --Database:Provider SqlServer
dotnet ef migrations add NazwaZmiany --project src/AfterTab.DAL.Migrations.PostgreSql --startup-project src/AfterTab.API -- --Database:Provider PostgreSql
```

Dla każdego polecenia ustaw connection string odpowiedniego silnika. Nie stosuj
migracji SQL Server do PostgreSQL. API nie wykonuje migracji podczas startu.
Do wdrożenia obu silników można też użyć narzędzia z `--migrate`, opisanego
w [instrukcji wdrożeniowej](after-tab-api/tools/AfterTab.Deployment/README.md).

Ochrona równoczesnej edycji zachowuje format Base64 w API: SQL Server używa
natywnego `rowversion`, PostgreSQL tokenu aktualizowanego przez EF przy zapisie.
Ręczne modyfikacje wydatków w PostgreSQL z pominięciem EF muszą również zmieniać
ten token. Klucze główne i obce mają typ `bigint` (`long` w C#), generowany przez
sekwencję HiLo. Publiczne identyfikatory kont, sesji, wydarzeń, uczestników i wydatków
pozostają GUID-ami z unikalnym indeksem; relacje i indeksy kluczy obcych używają liczb.
Szczegóły resetu i uprawnień sekwencji: [baza danych](after-tab-api/docs/database.md).

Testy konfiguracji i SQL uruchamiają się przez `dotnet test AfterTabApi.sln`.
Testy rzeczywistych serwerów wymagają zmiennych `AFTERTAB_TEST_SQLSERVER` oraz
`AFTERTAB_TEST_POSTGRESQL` z connection stringami do lokalnych serwerów testowych
i uprawnieniami do tworzenia/usuwania baz. Tworzą i usuwają własne bazy o losowych
nazwach `aftertab_test_*`; bez zmiennych te dwa przypadki są pomijane.

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
Schemat `InitialNumericKeys` zawiera tabelę jednorazowych operacji konta, pola
blokady logowania oraz indeks listy wydarzeń. Wcześniejsze migracje zostały
zastąpione; istniejąca instalacja wymaga resetu opisanego powyżej.

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

Użyj własnego projektu Google Cloud i klienta OAuth dla tego wdrożenia.
Jeśli współdzielisz projekt lub klienta z innymi aplikacjami, zachowaj ich konfigurację.

1. Otwórz [Google Cloud Console](https://console.cloud.google.com/) i wybierz projekt.
2. Otwórz **Google Auth Platform → Clients** (w starszym widoku **APIs & Services → Credentials**).
3. Utwórz klienta **Web application**, np. `AfterTab`, lub edytuj wybranego istniejącego.
4. W **Authorized redirect URIs** dodaj adresy środowisk, których używasz
   (drugi przykład zastąp publicznym adresem swojego API):
   - `https://localhost:7180/signin-google`
   - `https://api.example.com/signin-google`
5. Skonfiguruj **Branding / Audience** i użytkowników testowych. Nazwa i ekran zgody
   dotyczą projektu Google; zmiana ich we wspólnym projekcie może wpłynąć na inne aplikacje.
6. Zapisz Client ID i Client secret w lokalnych User Secrets backendu:

```powershell
dotnet user-secrets set 'Authentication:Google:ClientId' '<client-id>' --project src/AfterTab.API
dotnet user-secrets set 'Authentication:Google:ClientSecret' '<client-secret>' --project src/AfterTab.API
```

Nie potrzebujesz konfiguracji JavaScript SDK ani kluczy Google we frontendzie.
Logowanie obsługuje backend. Restart API uaktywni przycisk.
W ustawieniach ekranu zgody podaj własną domenę frontendu, dane kontaktowe oraz
adresy polityk z sekcji „Publiczne strony informacyjne”.

[Instrukcja Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-9.0)
i [reguły przekierowań Google](https://developers.google.com/identity/protocols/oauth2/web-server).

### 3. Facebook

Użyj własnej aplikacji w Meta for Developers. Jeżeli współdzielisz ją z innym
wdrożeniem, ustawienia nazwy, logowania i identyfikatorów użytkowników są wspólne.

1. Otwórz [Meta for Developers](https://developers.facebook.com/apps/) i wybierz lub utwórz aplikację.
2. Włącz przypadek użycia / produkt **Facebook Login** dla witryny.
3. W **Facebook Login → Settings → Valid OAuth Redirect URIs** dodaj adresy
   używanych środowisk (drugi przykład zastąp publicznym adresem swojego API):
   - `https://localhost:7180/signin-facebook`
   - `https://api.example.com/signin-facebook`
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

### 5. Konfiguracja produkcyjnego API

Na wybranym hostingu ustaw odpowiedniki powyższych sekretów i własne adresy.
W Azure App Service użyj ustawień środowiska usługi, a na VPS konfiguracji procesu
lub kontenera. Przykład nazw zmiennych dla API:

```text
Authentication__FrontendOrigin=https://app.example.com
Cors__AllowedOrigins__0=https://app.example.com
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

Podaj własny `ConnectionStrings__AfterTab`, włącz HTTPS i skonfiguruj zaufane
forwarded headers zgodnie z hostingiem — dostawca musi dostać callback HTTPS. Klucze ASP.NET Data Protection
muszą przetrwać restart API, aby trwające logowanie OAuth mogło się zakończyć.
Sekrety ustawiaj w środowisku hostingu, nie w kodzie ani w komendach przechowywanych w repozytorium.

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

To opcjonalny dostawca SMTP. Przed utworzeniem zasobów sprawdź aktualny cennik
i dostępność dla swojej subskrypcji; konfigurację wykonujesz we własnym koncie Azure.

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
w wybranym regionie nie oznacza automatycznie przetwarzania poczty w tym samym regionie.

[Cennik przykładowy](https://learn.microsoft.com/en-us/azure/communication-services/concepts/email-pricing)
i [konfiguracja SMTP w Azure](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication).


## Publiczne strony informacyjne AfterTab

Przed publikacją dostosuj `after-tab-app/public/privacy.html`, `terms.html` i
`data-deletion.html` w obu językach do własnej instancji: wpisz administratora
danych, kontakt (np. `support@example.com`), dostawców usług i zasady retencji.
Przykładowy kontakt zastąp rzeczywistym adresem swojego administratora.
Strony są statyczne, dostępne bez JavaScript, konta i połączenia z API:

- `/privacy.html` — polityka prywatności.
- `/data-deletion.html` — instrukcja zgłoszenia usunięcia danych e-mailem.
- `/terms.html` — zasady korzystania.

W Meta → App settings → Basic po opublikowaniu tych plików wpisz adresy własnego
frontendu. Przykład do zastąpienia (tych samych adresów polityk użyj w Google):

```text
Privacy policy URL: https://app.example.com/privacy.html
Terms of Service URL: https://app.example.com/terms.html
User data deletion → Data deletion instructions URL:
https://app.example.com/data-deletion.html
```

Instrukcja usuwania nie jest callbackiem API. Żądania trzeba obsługiwać ręcznie,
potwierdzając własność konta i usuwając lub anonimizując odpowiednie dane.
Odłączenie Facebooka/Google, wylogowanie i „Zapomnij gościa” nie realizują same
usunięcia danych serwerowych. Treść stron musi opisywać Twoje wdrożenie; przy zmianie
retencji, dostawców, opłat lub sposobu usuwania trzeba zaktualizować ich treść.
Sprawdź i zatwierdź treść przed publikacją na wybranym hostingu.

## Model uczestników i migracja lokalna

`EventParticipants.Id` jest identyfikatorem uczestnika rozliczenia. `UserId` jest
opcjonalnym powiązaniem z kontem; `DisplayName` przechowuje imię osoby bez konta.
Uczestnicy z kontem wyświetlają aktualne imię z `Users`. Łączenie zachowuje identyfikator
uczestnika bez konta, a wycofany wiersz staje się aliasem `MergedIntoParticipantId`,
żeby zachować prywatną historię płatności. Nie tworzymy fikcyjnych rekordów `Users`.

Schemat `InitialNumericKeys` od razu tworzy opcjonalny `UserId` uczestnika
i powiązania `TransferConfirmations` z uczestnikami tego samego wydarzenia.
W bazie `Id` oraz klucze obce są liczbowe; API używa odrębnego `PublicId` GUID.
Nie ma migracji zachowującej dane ze starego schematu — reset usuwa również historię płatności.

Po pobraniu tej wersji uruchom lokalne `dotnet ef database update` z instrukcji powyżej.
Do testów używaj lokalnej LocalDB lub PostgreSQL i frontendu z `VITE_API_BASE_URL=https://localhost:7180`.
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

Schemat `InitialNumericKeys` zawiera opcjonalne `Expenses.BalanceParticipantId`
i kwotę `BalanceAmountMinor`. Dzięki temu wyrównanie można przenosić po zapisie
bez zgadywania poprzednich udziałów. Wyrównanie wskazuje płacący albo autor wydarzenia.

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
