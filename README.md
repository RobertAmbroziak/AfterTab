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

Nierozdzielona kwota trafia do **Inni**, np. gdy część osób jeszcze nie dołączyła.
Przycisk **Wyrównaj** dopisuje resztę osobie z największą kwotą udziału,
a przy remisie autorowi pozycji. Suma udziałów wraz z „Inni” zawsze odpowiada całej kwocie.

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
