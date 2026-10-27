# AfterTab — zasoby developerskie Azure

Zmiany z 27.10.2026 opisane w [DARK-MODE-AND-PAYMENTS.md](DARK-MODE-AND-PAYMENTS.md)
zostały wdrożone na wyraźną prośbę użytkownika. Migracja
`20261027140319_PayersAndPrivateTransfers` jest zastosowana w Azure `AfterTabDb`.
Kolejne wdrożenia wymagają osobnej prośby użytkownika.

## Wdrożenie 27.10.2026 — grafit, mięta i prywatne oznaczenia

Późniejsza publikacja tego samego dnia objęła tylko PWA: automatyczne wersjonowanie
adresów ikon po SHA-256 zawartości. Backend i baza nie wymagały zmian.
Zweryfikowano publiczny manifest, HTML, service worker i HTTP 200 dla:
`/pwa-192x192.png?v=bcf9a2fc19a2` oraz `/pwa-512x512.png?v=f47f3b2c0e1f`.
Identyfikator `/` i start_url `/` pozostały bez zmian. Lint/build przeszły.

- Potwierdzone konto Azure CLI: `Rob.Ambroziak@outlook.com`, subskrypcja `PrivateSubscription`.
- 48 testów .NET (17 jednostkowych, 31 integracyjnych), lint i produkcyjny build PWA przeszły.
- Przed i po migracji: 18 użytkowników, 24 sesje, 4 wydarzenia i 9 pozycji.
  Migracja uzupełniła autorów istniejących pozycji i dodała tabelę prywatnych oznaczeń;
  wcześniejszych danych nie usuwano ani nie kopiowano z LocalDB do Azure.
- Na czas migracji i publikacji API było zatrzymane, po publikacji zostało uruchomione.
- Wdrożenie API: `22b1626c-3b66-4c3b-8ab4-305e59df63c8`, zakończone
  `2026-10-27T14:35:47Z` (UTC). Publiczne `/api/health` zwraca `status=ok`.
- PWA production: paczka `index-B8Yp0wJ8.js`, style `index-Ki4qW5sU.css`.
  Publiczny adres zwraca HTTP 200 i wskazuje tę paczkę.
- Test Chrome na publicznym adresie potwierdził menu konta, instalację poza menu,
  ikonę kalendarza, imię gościa, zaproszenie, procenty domyślne i wybór innego płacącego.
- Zamknięte rozliczenie testowe: nadawca oznaczył wysłanie 30 zł, odbiorca odbiór;
  autor wydarzenia niebędący stroną nie otrzymał statusów w JSON ani checkboxów.
  Statusy zachowały się po odświeżeniu, widok 390 px mieści się bez przewijania
  w poziomie, brak błędów JavaScript.
- Weryfikacja Azure pozostawiła jedno zamknięte wydarzenie „Test wdrożenia grafit”,
  jedną pozycję i trzy konta testowe. Sesje tych kont wylogowano po teście.
- SHA-256 paczki backendu:
  `74409006E727D960A600E66C1950DF58A2B05EDC13017886E9ECA14EBB263894`.
- Dodano tymczasową regułę dla pojedynczego adresu VPN, następnie ją usunięto
  po wyłączeniu VPN przez użytkownika. Dalsze operacje używały istniejącej reguły.
  Końcowa konfiguracja zapory nie zawiera nowej reguły wdrożeniowej.
- Bez zmian planów i kosztów; zasoby BudgetWallet pozostały bez zmian.

## Wcześniejsze wdrożenie 25.10.2026

Na wyraźną prośbę użytkownika 25.10.2026 wdrożono imiona, wydarzenia, pozycje i rozliczenia
opisane w [NAMES-AND-EVENTS.md](NAMES-AND-EVENTS.md), [EXPENSES.md](EXPENSES.md)
i [SETTLEMENTS.md](SETTLEMENTS.md). Kolejne deploye wymagają osobnej prośby użytkownika.

Stan: 25 października 2026. Zasoby utworzono przez Azure CLI w subskrypcji
`PrivateSubscription`. Ten dokument nie zawiera poświadczeń.

| Zasób | Nazwa | Grupa zasobów | Region / plan |
|---|---|---|---|
| Hosting API | `aftertab-api-ambro` | `AfterTab-RG` | Poland Central, Linux, .NET 9 |
| Plan API | `aftertab-api-plan` | `AfterTab-RG` | Poland Central, F1 Free |
| Baza | `AfterTabDb` | `BudgetWallet-RG` | Poland Central, General Purpose Serverless, free limit |
| Istniejący serwer SQL | `ambrodevserver` | `BudgetWallet-RG` | Poland Central |
| Istniejąca PWA | `aftertab` | `AfterTab-RG` | Central US, Free |

Adres hostingu API: https://aftertab-api-ambro.azurewebsites.net

Adres PWA: https://orange-stone-063c89710.4.azurestaticapps.net

Serwer bazy: `ambrodevserver.database.windows.net`, baza: `AfterTabDb`.
Baza jest w grupie `BudgetWallet-RG`, ponieważ należy do istniejącego serwera SQL
w tej grupie. Utworzenie bazy nie zmienia konfiguracji `BudgetWalletDb`.
API ma osobny plan, niezależny od planu BudgetWallet.

## Koszt i limity

- API: F1 Free, 60 minut CPU dziennie, bez Always On i bez SLA.
- SQL: potwierdzone `useFreeLimit=true` oraz `freeLimitExhaustionBehavior=AutoPause`.
  Darmowy limit obejmuje 100 000 vCore-sekund, 32 GB danych i 32 GB kopii zapasowych
  miesięcznie. Po wyczerpaniu darmowego limitu baza zatrzymuje się do kolejnego miesiąca.
- SQL: maksymalnie 2 vCore, minimum 0,5 vCore, auto-pause po 60 minutach bezczynności,
  maksymalny rozmiar 32 GB, lokalna redundancja kopii zapasowych.
- Nie włączono płatnego przekraczania limitu ani dodatkowego płatnego monitoringu.
- Opłaty za wcześniejsze zasoby, w tym istniejącą bazę BudgetWallet, pozostają osobną sprawą.

Źródła:
- https://azure.microsoft.com/en-us/pricing/details/app-service/linux/
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier

## Konfiguracja hostingu

- HTTPS wymagane, minimalny TLS 1.2, FTP/FTPS wyłączone.
- Runtime `DOTNETCORE|9.0`, środowisko ASP.NET Core `Production`.
- `Cors__AllowedOrigins__0` wskazuje dokładny adres istniejącej PWA.
- API otrzymuje systemową tożsamość zarządzaną do przyszłego połączenia z bazą bez hasła.

## Wdrożenie wykonane 25 października 2026

### Aktualne wdrożenie — imiona, pozycje i rozliczenia

- Wykonano na prośbę użytkownika po zatrzymaniu lokalnych procesów API i Vite.
- 46 testów .NET (17 jednostkowych, 29 integracyjnych), lint i produkcyjny build PWA przeszły.
- Azure potwierdził dostępny runtime `DOTNETCORE:9.0`.
- Do istniejącego `AfterTabDb` zastosowano trzy brakujące migracje:
  `20261025163751_ConfirmDisplayName`, `20261025171442_ExpenseOthers`
  i `20261025174432_EventSettlementLifecycle`. Łącznie pięć migracji.
- Bezpośrednio przed i po migracjach baza miała 13 użytkowników i 18 sesji
  (16 unieważnionych), więc istniejące konta i sesje zostały zachowane.
- App Service: pomyślne wdrożenie `ba1b095d-7274-4edb-a933-57513884646a`,
  zakończone `2026-10-25T18:01:28Z` (UTC).
- PWA: opublikowane środowisko `production`, paczka `index-fu6lgYGA.js`.
- Publiczne API health zwraca `status=ok`, PWA zwraca HTTP 200 i serwuje nową paczkę.
- Chrome na publicznym adresie potwierdził: imię gościa, zachowanie zaproszenia
  podczas logowania, jawne dołączenie, podział procentowy z wyrównaniem i podział
  kwotowy dodany przez uczestnika, wspólne zamknięte rozliczenie z przelewem 10,62 zł,
  blokadę przycisku dodawania po zamknięciu oraz widok 390 px.
- Osobny test wylogowania/powrotu potwierdził ten sam `UserId` i potwierdzone imię,
  bez błędów JavaScript. W pierwszym skrypcie testowym poprawiono jedynie selektor
  przycisku powrotu na jego faktyczną nazwę „WRÓĆ JAKO GOŚĆ”; kod aplikacji nie wymagał zmian.
- Testy Azure pozostawiły jedno zamknięte wydarzenie oznaczone „Test wdrożenia”,
  dwie pozycje oraz trzy konta testowe. Nie zmieniano wydarzeń rzeczywistych użytkowników.
- Nie przenoszono danych LocalDB do Azure. Azure używa własnej istniejącej bazy `AfterTabDb`.
- Nie zmieniano zasobów BudgetWallet ani parametrów kosztowych. `AfterTabDb` nadal
  ma `useFreeLimit=true` i `freeLimitExhaustionBehavior=AutoPause`.

### Poprzednie wdrożenie — zapamiętywanie gościa

Aktualizacja tego samego dnia: opublikowano zapamiętywanie gościa i zastosowano
migrację `20261025154530_RememberGuestBrowser`. Wylogowanie zachowuje możliwość
powrotu do tego samego `UserId`; osobna opcja „Zapomnij gościa” ją unieważnia.
Nowy mechanizm i jego ograniczenia opisuje [REMEMBERED-GUEST.md](REMEMBERED-GUEST.md).
Na publicznym adresie Azure test Chrome potwierdził stałe konto po wylogowaniu,
nową sesję, powrót z samego ciasteczka, zapomnienie oraz migrację starego gościa.
20 testów .NET i lint/kompilacja PWA zakończyły się powodzeniem.

- Backend Release opublikowano do App Service; `/api/health` zwraca `status=ok`.
- Migracja `20261025122721_InitialCreate` została zastosowana do `AfterTabDb` przez
  istniejącego administratora Microsoft Entra. Administratora serwera nie zmieniano.
- Użytkownik SQL `aftertab-api-ambro` odpowiada systemowej tożsamości API i ma
  `SELECT, INSERT, UPDATE, DELETE` na schemacie `dbo` w `AfterTabDb`.
  Nie otrzymał uprawnień do migracji ani dostępu do bazy BudgetWallet.
- W App Service ustawiono `ConnectionStrings__AfterTab`:
  `Server=tcp:ambrodevserver.database.windows.net,1433;Database=AfterTabDb;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;`
- Włączono `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` dla obsługi HTTPS za proxy Azure.
  Istniejące reguły zapory SQL wystarczyły; nie dodawano nowych.
- Frontendowy `.env.production.local` zawiera
  `VITE_API_BASE_URL=https://aftertab-api-ambro.azurewebsites.net`.
  PWA z przyciskiem instalacji opublikowano do produkcyjnego środowiska `aftertab`.
- Zachowano F1 Free i darmową ofertę SQL z AutoPause. Pierwsze żądanie po uśpieniu
  API lub bazy może trwać dłużej. API ponawia przejściowe błędy połączenia SQL.

## Potwierdzone sprawdzenia

- 11 testów .NET, lint oraz kompilacja PWA.
- HTTP na Azure: utworzenie gościa, odczyt sesji, ponowne użycie bez duplikatu,
  CORS dla adresu PWA, wylogowanie i odrzucenie unieważnionego tokenu (401).
- Chrome: logowanie gościa przez opublikowaną PWA, odświeżenie z zachowaniem sesji,
  wylogowanie oraz brak błędów JavaScript.
- Widok o szerokości 390 px: brak poziomego przewijania, otwarcie i zamknięcie
  instrukcji instalacji po emulacji iPhone'a. To nie zastępuje sprawdzenia instalacji
  na fizycznym telefonie.
- Testy utworzyły dwóch gości i dwie sesje, które następnie unieważniły.

## Ponowne publikowanie

Po zmianach backendu uruchom testy i przygotuj paczkę:

```powershell
dotnet test AfterTabApi.sln
dotnet publish src/AfterTab.API -c Release -o artifacts/azure-api -p:UseAppHost=false
.\scripts\New-DeploymentZip.ps1 -SourcePath artifacts/azure-api -DestinationPath artifacts/aftertab-api.zip
az webapp deploy -g AfterTab-RG -n aftertab-api-ambro --src-path artifacts/aftertab-api.zip --type zip --track-status false
```

Skrypt ZIP używa separatorów `/`, wymaganych przez Linux App Service.
Windows PowerShell `Compress-Archive` wygenerował niezgodne ścieżki i nie jest tu używany.

Narzędzie `tools/AfterTab.Deployment` wykonuje migracje z opcją `--bootstrap`,
używając tokenu SQL przekazanego wyłącznie przez `AFTERTAB_SQL_ACCESS_TOKEN`
oraz identyfikatora tożsamości API w `AFTERTAB_API_PRINCIPAL_ID`.
Bez `--bootstrap` tylko sprawdza migracje i liczbę rekordów. Jest celowo ograniczone
do `ambrodevserver` / `AfterTabDb`. Token uzyskuj przez Azure CLI i usuwaj ze
środowiska po użyciu; nie zapisuj go w pliku ani nie wyświetlaj.

Po zmianach PWA uruchom `npm run lint` i `npm run build` w `after-tab-app`,
a następnie `swa deploy ./dist --env production --app-name aftertab --resource-group AfterTab-RG`.
Token publikacji pobieraj do zmiennej środowiskowej `SWA_CLI_DEPLOYMENT_TOKEN`
przez `az staticwebapp secrets list`; nie kopiuj go do plików ani opisu wdrożenia.
Po publikacji sprawdź logowanie gościa w przeglądarce. Przy starszej otwartej wersji
PWA zaakceptuj komunikat o aktualizacji lub zamknij i otwórz aplikację ponownie.

Przed kolejną publikacją sprawdzić aktualne wsparcie runtime .NET 9 i zaplanować aktualizację
zgodnie z wymaganiami używanego Visual Studio.
