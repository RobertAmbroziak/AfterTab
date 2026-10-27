# AfterTabApi

Ostatnia poprawka wdrożona na Azure 27.10.2026: automatyczne wersjonowanie adresów
ikon PWA na podstawie zawartości plików, aby przeglądarka wykrywała ich zmianę.

Zmiany z 27.10.2026 są **wdrożone na Azure na wyraźną prośbę użytkownika**: grafit + mięta, menu konta,
procenty domyślne, oddzielny płacący, prywatne oznaczenia przelewów i obsługa wolnego startu.
Opis: [docs/DARK-MODE-AND-PAYMENTS.md](docs/DARK-MODE-AND-PAYMENTS.md).

Najnowsze zmiany: bieżące rozliczenie, kompensacja sald, zamykanie/otwieranie wydarzeń
i poprawianie podziałów przez autora pozycji. Opis: [docs/SETTLEMENTS.md](docs/SETTLEMENTS.md).
Pozycje wydarzeń: podział kwotowy/procentowy, „Inni” i wyrównanie.
Opis: [docs/EXPENSES.md](docs/EXPENSES.md).
Wcześniejsze zmiany: imiona użytkowników, tworzenie/lista wydarzeń i zaproszenia.
Opis oraz weryfikacja: [docs/NAMES-AND-EVENTS.md](docs/NAMES-AND-EVENTS.md).
Wdrożone na Azure 25.10.2026 na wyraźną prośbę użytkownika.
Każdy kolejny deploy wykonujemy wyłącznie na osobną prośbę.

Szkielet backendu AfterTab: .NET 9, EF Core 9.0.21, SQL Server/LocalDB.
Solucja: `AfterTabApi.sln`. Zalecane Visual Studio 2022 17.14 z SDK .NET 9.0.3xx,
workloadem **ASP.NET and web development** i SQL Server Express LocalDB.
`global.json` wybiera najnowszy zainstalowany patch SDK 9.0.3xx.

## Instalacja obok frontendu

Rozpakuj katalog `after-tab-api` z archiwum do `C:\MyApps\AfterTab`.
Otrzymasz `C:\MyApps\AfterTab\after-tab-api\AfterTabApi.sln`.
Nie kopiuj backendu do folderu React. Archiwum nie zmienia frontendu.

1. Otwórz `AfterTabApi.sln` w Visual Studio 2022.
2. Ustaw `AfterTab.API` jako projekt startowy.
3. W PowerShell przejdź do katalogu backendu i uruchom:

```powershell
cd C:\MyApps\AfterTab\after-tab-api
.\scripts\Verify.ps1
.\scripts\Initialize-Database.ps1
```

Pierwszy skrypt odtwarza paczki, kompiluje i uruchamia testy.
Drugi odtwarza lokalne narzędzie `dotnet-ef`. Migracja `InitialCreate`
razem ze snapshotem EF jest już w `src/AfterTab.DAL.EF/Migrations`.
Aby utworzyć lokalną bazę lub zastosować brakujące migracje:

```powershell
.\scripts\Initialize-Database.ps1 -Apply
```

Skrypt używa domyślnie LocalDB i bazy `AfterTab`. Przy innej bazie ustaw zmienną
przed wywołaniem narzędzi EF i uruchomieniem API:

```powershell
$env:ConnectionStrings__AfterTab = 'Server=...;Database=AfterTab;...'
```

Fabryka design-time odczytuje tę zmienną lub domyślny LocalDB, a nie plik appsettings.
API odczytuje zwykłą konfigurację ASP.NET Core (appsettings, user secrets, zmienne środowiskowe).
Nie zapisuj produkcyjnego hasła w repozytorium.

W Visual Studio naciśnij F5, albo:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/AfterTab.API --launch-profile https
```

Adres: `https://localhost:7180/api/health`.
Przykład podziału kosztu jest w `src/AfterTab.API/AfterTab.API.http`.
Endpoint health sprawdza uruchomienie procesu, nie połączenie z bazą.

## Projekty

| Folder solucji | Projekt | Odpowiedzialność |
|---|---|---|
| API | AfterTab.API | Kontrolery, konfiguracja hosta, DI, kontekst użytkownika |
| BL | AfterTab.BL.DTO | Kontrakty wejścia i wyjścia |
| BL | AfterTab.BL.Interfaces | Interfejsy usług biznesowych |
| BL | AfterTab.BL.Services | Logika biznesowa i jej implementacje |
| DAL | AfterTab.DAL.Entities | Encje, BaseEntity, enumy |
| DAL | AfterTab.DAL.Interfaces | Interfejsy repozytoriów i jednostki pracy |
| DAL | AfterTab.DAL.EF | DbContext, mapowania, repozytoria, migracje i miejsce na seedy |
| COMMON | AfterTab.Common | ICurrentUser; brak zależności od HTTP i EF |
| TESTS | AfterTab.Tests.Unit | Testy kalkulatora podziałów |
| TESTS | AfterTab.Tests.Integration | Testy audytu/ograniczeń SQLite oraz modelu SQL Server |

BL nie zależy od implementacji EF. API rejestruje implementacje przez
`AddAfterTabBusiness()` i `AddAfterTabData(...)`.

## Model danych

Dziesięć tabel: Users, UserIdentities, Sessions, GuestDevices, GuestLoginRequests,
Events, EventParticipants, Expenses, ExpenseSplits, EventInvites.
EF tworzy też `__EFMigrationsHistory`.
Nie ma tabeli BaseEntity ani wspólnej hierarchii TPH.

- Users: użytkownicy i goście; brak UserIdentity oznacza gościa.
- UserIdentities: metody logowania; identyfikator zewnętrznego dostawcy jest
  unikalny w ramach providera. Lokalne e-maile mają osobny unikalny indeks.
  `NormalizedEmail` trzeba normalizować na serwerze przed zapisem.
- Sessions: sesje ważne przez 30 dni. TokenHash przechowuje SHA-256 losowego
  256-bitowego tokenu, weryfikowanego przez middleware Bearer przy żądaniu.
- Events: jeden właściciel, nowe wydarzenia używają PLN bez wyboru waluty. Serwis tworzenia atomowo zapisuje
  wydarzenie, właściciela jako uczestnika oraz pierwsze zaproszenie.
- EventParticipants: unikalna para EventId/UserId. Zamiast usuwać historię,
  zmieniaj status uczestnictwa.
- Expenses: AuthorUserId identyfikuje autora pozycji, PaidByParticipantId osobno płacącego.
  Tylko właściciel wydarzenia może wskazać/zmienić inną osobę płacącą. AmountMinor to grosze, typ long.
  OthersAmountMinor przechowuje nierozdzieloną część, Balanced oznacza użycie „Wyrównaj”.
  RowVersion obsługuje optymistyczną współbieżność SQL Server.
- ExpenseSplits: wielu wybranych uczestników, płacący może być pominięty.
  PercentageBps: 5000 = 50%; 2500 = 25%. AmountMinor zawsze zawiera ostateczną kwotę.
  EventId jest celowo powtórzony: złożone klucze obce uniemożliwiają wskazanie
  płacącego lub udziałowca z innego wydarzenia.
- EventInvites: miejsce na skrót losowego sekretu zaproszenia, ważność i unieważnienie.

Relacje używają NoAction, aby usunięcie użytkownika nie usuwało historii wydatków.
Klucze obce audytu wskazują Users, ale mogą być puste dla operacji systemowych
oraz pierwszego utworzenia gościa. Nazwy użytkowników nie są unikalne.

Suma udziałów jest regułą agregatu i nie jest egzekwowana zwykłym CHECK SQL.
ExpenseService waliduje uprawnienia, aktywne uczestnictwo, status wydarzenia i sumy.
Suma ExpenseSplits.AmountMinor + Expenses.OthersAmountMinor = Expenses.AmountMinor.
Podział bez wskazanych osób jest dopuszczalny: całość trafia do „Inni”.
Wydatek i podziały są zapisywane atomowo. Przyszła aktualizacja samych udziałów powinna
również aktualizować Expense, by jego RowVersion wykrywał konflikt całego wydatku.

## Audyt

BaseEntity zawiera Id, CreatedAtUtc, CreatedByUserId, ModifiedAtUtc i ModifiedByUserId.
AppDbContext automatycznie uzupełnia pola przy wszystkich przeciążeniach
SaveChanges/SaveChangesAsync. Pola utworzenia nie są aktualizowane.
Czas dostarcza TimeProvider, wykonawcę ICurrentUser. Nie przyjmujemy audytu w DTO.
Nowo utworzony rekord ma puste ModifiedAtUtc/ModifiedByUserId.
Operacje masowe ExecuteUpdate, ExecuteDelete i raw SQL omijają ten mechanizm.
To informacja o ostatniej zmianie, nie pełny dziennik historii ani rejestr usunięć.

## Co już działa w kodzie, a co jest kolejnym etapem

Zaimplementowano model i mapowania EF, audyt, repozytorium odczytu użytkownika,
serwis profilu bieżącego użytkownika, kalkulator procentowy z deterministycznym
rozdzielaniem reszty groszy, konfigurację DI i bazowe publiczne endpointy:

- GET /api/health
- POST /api/splits/preview — bezstanowy podgląd; nie zapisuje ani nie odczytuje danych użytkowników.

Dodano logowanie gościa i integrację z sąsiednim frontendem `after-tab-app`:

- `POST /api/auth/guest` tworzy `Users` i `Sessions` w jednej transakcji.
  Zwraca `token`, `user` i `expiresAtUtc`. Ważny token w nagłówku powoduje
  zwrot istniejącej sesji bez kolejnego użytkownika. Limit: 10 żądań/minutę/IP.
- `GET /api/auth/me` przyjmuje `Authorization: Bearer <token>` i zwraca profil sesji.
- `POST /api/auth/logout` unieważnia sesję; wymaga tego samego nagłówka.

Brak tokenu, wygasła/unieważniona sesja lub nieaktywny użytkownik oznacza 401
na chronionych endpointach. Odpowiedzi uwierzytelniania mają `Cache-Control: no-store`.
Gość nie ma rekordu `UserIdentities`; ten rekord będzie potrzebny przy logowaniu do konta.
Nie tworzymy wydarzeń ani wydatków przy samym logowaniu.

Nie ma jeszcze logowania do konta, odnawiania sesji, łączenia Google/Facebook,
odzyskiwania konta ani rejestracji wykonanych przelewów.
Tworzenie/lista wydarzeń, zaproszenia, pozycje, poprawianie podziałów oraz
rozliczanie z zamykaniem i ponownym otwieraniem są zaimplementowane lokalnie.
Backend, baza `AfterTabDb` i PWA są wdrożone na Azure; szczegóły i potwierdzone
sprawdzenia opisano w [docs/AZURE-DEVELOPMENT.md](docs/AZURE-DEVELOPMENT.md).

HttpCurrentUser odczytuje tylko uwierzytelnione claimy i wewnętrzny
`aftertab_user_id`; nie ufa nagłówkowi z UserId. Claim jest wystawiany przez
`SessionAuthenticationHandler` po sprawdzeniu sesji i aktywnego użytkownika w bazie.
Przyszłe endpointy danych muszą stosować `[Authorize]` oraz sprawdzać uprawnienia do wydarzenia.

## Uruchomienie PWA z API

Uruchom API profilem `https`, a w `C:\MyApps\AfterTab\after-tab-app` wykonaj `npm run dev`.
Frontend w trybie deweloperskim domyślnie wywołuje `https://localhost:7180`.
Zaufaj certyfikatowi przez `dotnet dev-certs https --trust`, jeśli nie był jeszcze zaufany.
Otwórz `http://localhost:5173` i wybierz „ZALOGUJ JAKO GOŚĆ”.
Przycisk „ZALOGUJ” jest oznaczony jako „Wkrótce” i pozostaje nieaktywny.

PWA sprawdza sesję przez `/api/auth/me` przy otwarciu. Gość jest zapamiętany
osobnym kluczem w ciasteczku HttpOnly; po wylogowaniu następne logowanie wydaje
nową sesję dla tego samego użytkownika. Opcja „Zapomnij gościa” świadomie
unieważnia klucz powrotu i powiązane sesje, bez usuwania danych.
Błąd sieci nie usuwa zapisanego tokenu. Sekrety nie mogą trafiać do logów.
Szczegóły i ograniczenia: [docs/REMEMBERED-GUEST.md](docs/REMEMBERED-GUEST.md).

## Przygotowanie Azure

Docelowy układ: hosting PWA, App Service dla API i Azure SQL Database `AfterTab`.
Jeśli istniejący serwer to logiczny serwer Azure SQL, można utworzyć na nim nową bazę.
Nie twórz drugiego serwera wyłącznie dla tej aplikacji.

1. W App Service ustaw `ConnectionStrings__AfterTab` na połączenie do bazy AfterTab
   (lub connection string o nazwie `AfterTab`). Sekrety przechowuj w konfiguracji Azure/Key Vault.
2. Zapewnij API dostęp sieciowy i uprawnienia do bazy; zastosuj migracje EF wskazując
   docelową bazę w kontrolowanym procesie wdrożenia. API nie migruje bazy przy starcie.
3. Ustaw `Cors__AllowedOrigins__0` na dokładny adres HTTPS PWA, bez końcowego `/`.
   Wbudowaną konfigurację CORS App Service pozostaw wyłączoną, jeśli CORS obsługuje aplikacja.
4. W frontendowym `.env.production.local` ustaw `VITE_API_BASE_URL=https://<api>.azurewebsites.net`
   i uruchom `npm run build`. Zmienna musi być ustawiona przed kompilacją Vite.
   Wgraj `dist` na hosting PWA. Zmienna nie zawiera hasła ani connection stringa.
5. Sprawdź utworzenie gościa, odświeżenie PWA i wylogowanie na rzeczywistych adresach HTTPS.

Produkcja bez `VITE_API_BASE_URL` korzysta z `/api` na domenie PWA i wymaga przekierowania
tego prefiksu do backendu. Dla osobnych domen ustaw tę zmienną jawnie.
Limit żądań gościa jest lokalny dla instancji API; skalowanie wymaga wspólnego limitu
na bramie oraz poprawnej konfiguracji zaufanego reverse proxy i adresów klienta.
Przed wdrożeniem sprawdź również wspierany runtime .NET dla wybranego App Service.

Dokumentacja: https://learn.microsoft.com/en-us/azure/app-service/tutorial-dotnetcore-sqldb-app

## Kolejne migracje

```powershell
dotnet tool restore
dotnet ef migrations add NazwaZmiany --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API --output-dir Migrations
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
```

Komituj migracje i snapshot razem z kodem. Nie używaj EnsureCreated dla docelowej bazy;
jest używane wyłącznie w efemerycznych bazach testowych SQLite.
API nie wykonuje automatycznych migracji przy starcie.

## Weryfikacja paczki

Sprawdzono kompilację API oraz 48 testów .NET (17 jednostkowych i 31 integracyjnych).
Podziały kwotowe/procentowe sprawdzono też w Chrome na LocalDB, w tym na szerokości 390 px;
szczegóły w [docs/EXPENSES.md](docs/EXPENSES.md).
Testy sesji obejmują zapis gościa, skrót tokenu, przywracanie bez duplikatów,
wygaśnięcie, unieważnienie i blokowanie nieaktywnego użytkownika.
Migrację `InitialCreate` zastosowano do lokalnej bazy SQL Server LocalDB `AfterTab`.
Test HTTP na tej bazie potwierdził tworzenie gościa, przywracanie i ponowne użycie
sesji, CORS, wylogowanie i odpowiedź 401 po unieważnieniu. Pozostawił jednego
testowego użytkownika gościnnego z unieważnioną sesją.
Frontend przeszedł `npm run lint` i `npm run build`, łącznie z generowaniem service workera PWA.
Projektowy `NuGet.Config` korzysta z publicznego nuget.org.

.NET 9 jest ostatnim targetem oficjalnie obsługiwanym przez Visual Studio 2022.
Wsparcie .NET 9 kończy się w listopadzie 2026; dalsza aktualizacja wymaga zaplanowania
przejścia na nowsze IDE i .NET. Wersje NuGet są centralnie przypięte w Directory.Packages.props.

Źródła zgodności:
- https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs
- https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/9.0.21
