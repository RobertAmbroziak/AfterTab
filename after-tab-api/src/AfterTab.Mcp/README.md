# AfterTab MCP

**Polski** | [English](README.en.md)

Moduł MCP udostępnia wydarzenia i rozliczenia AfterTab klientom AI przez standardowy
Streamable HTTP. To osobny projekt w tej samej solucji, uruchamiany przez istniejące
API pod `/mcp`. Nie wymaga osobnego App Service, konta AI ani klucza OpenAI po stronie
AfterTab. Adres serwera podaje się dopiero w konfiguracji klienta.

Serwer obsługuje OAuth 2.1 z rejestracją klienta, PKCE S256, zgodą w AfterTab oraz
rotacją tokenów. Użytkownik łączy konto przez przeglądarkową stronę zgody AfterTab;
nie kopiuje tokenu do ChatGPT. Ręczne tokeny Bearer nadal są dostępne dla Codexa i
innych klientów technicznych. Moduł nie przyjmuje hasła ani nazwy konta od modelu.
Niestandardowe serwery MCP w ChatGPT są dostępne w przeglądarce; aplikacja mobilna
ChatGPT nie oferuje obecnie dodawania takiego serwera.

## Włączenie we własnym wdrożeniu

Integracja jest domyślnie wyłączona: `Mcp:Enabled=false`. W takim stanie `/mcp`
i zarządzanie tokenami nie są dostępne. Aby ją uruchomić:

1. Zastosuj wszystkie oczekujące migracje MCP (`McpIntegration`,
   `AddMcpUserInstructions`, `McpOAuth`) dla wybranego dostawcy bazy. Dodają
   tabele integracji i ustawień; nie resetują danych. Polecenia migracyjne opisuje
   [główny README](../../../README.md).
2. Ustaw `Mcp__Enabled=true` w środowisku API, np. w konfiguracji App Service.
3. Ustaw `VITE_MCP_ENABLED=true` przy budowaniu frontendu, aby pokazać panel tokenów.
4. Ustaw `Mcp__PublicOrigin` na publiczny origin API, np. `https://api.example.com`.
   To ten sam origin, pod którym są wystawione `/mcp`, endpointy OAuth i dokumenty
   `/.well-known`. Ustaw też istniejące `Authentication__FrontendOrigin` na origin
   aplikacji AfterTab; strona zgody musi używać tego samego API i sesji użytkownika.
5. Udostępnij `/mcp` przez HTTPS na domenie API. Reverse proxy musi przekazywać
   `Authorization` oraz nagłówki `Mcp-*`, obsługiwać POST i odpowiedzi SSE bez buforowania.
   Nie zapisuj nagłówka Authorization ani treści odpowiedzi z tokenami w logach proxy.

W wariancie [Docker Compose](../../../deploy/mikrus/README.md) wystarczy
`MCP_ENABLED=true` w prywatnym `.env` oraz przebudowanie obrazów według instrukcji
wdrożenia. Przygotowany nginx przekazuje `/mcp` do API. Domyślne `false` zachowuje
wyłączoną integrację. Na Azure nie zmieniaj ustawień dostawcy bazy z tego powodu.

Do pracy lokalnej można użyć `dotnet user-secrets set "Mcp:Enabled" "true"`
w katalogu `after-tab-api/src/AfterTab.API`. Lokalnie origin API może być wyliczony
z żądania HTTPS; w publicznym wdrożeniu ustaw `Mcp:PublicOrigin`. Nie wyłączaj
weryfikacji certyfikatów w kliencie: zaufaj lokalnemu certyfikatowi deweloperskiemu
lub użyj HTTPS wdrożenia.

## Token i konto

Po włączeniu funkcji zaloguj się normalnie do AfterTab. W menu konta otwórz
**Integracje AI**. Zgoda OAuth łączy konto aktualnie zalogowane w przeglądarce z
klientem MCP; po autoryzacji możesz odłączyć klienta z tej samej strony. Wymagane
są wszystkie migracje MCP dla SQL Server albo PostgreSQL. Token dostępu wygasa po
10 minutach, a refresh token jest rotowany i wygasa po 30 dniach. Cofnięcie dostępu
unieważnia tokeny klienta.

Ręczne tokeny Bearer nadal można utworzyć dla testów Codexa i innych klientów.
Sekret jest pokazywany tylko
przy tworzeniu i pozostaje jedynie w pamięci otwartego panelu. Domyślnie token ma
tylko odczyt; zaznacz zgodę na zapis, aby tworzyć wydarzenia i zmieniać wydatki.
Ważność wynosi domyślnie 30 dni, do wyboru 1–90 dni. Limit to 20 aktywnych tokenów
na użytkownika. Token można unieważnić w tym samym panelu; wylogowanie z PWA
nie unieważnia automatycznie integracji.

Token działa jako konto, które go utworzyło, także zapamiętany gość. Samo podanie
imienia nie pozwala wejść na cudze konto. Konto musi być aktywne i mieć potwierdzone
imię. ChatGPT także wymaga zalogowania do ChatGPT. AfterTab nie musi pozostać otwarty
po połączeniu, ale jego API musi być dostępne online. OAuth używa konta już
zalogowanego w AfterTab; nie tworzy anonimowego konta ani nie loguje po samym imieniu.

API zapisuje wyłącznie skrót SHA-256 sekretu. Token nie zastępuje sesji PWA,
a sesja PWA nie działa na `/mcp`. Uprawnienia do wydarzeń i wydatków nadal
sprawdzają obecne serwisy biznesowe. Token nie przyznaje uprawnień właściciela.
Zablokowanie/usunięcie konta, wygaśnięcie lub unieważnienie tokena blokuje nowe
żądania MCP. Już rozpoczęte żądanie może się zakończyć.

Zarządzanie jest też dostępne przez API, wyłącznie ze zwykłą sesją AfterTab:

| Metoda i ścieżka | Działanie |
| --- | --- |
| `GET /api/profile/mcp-tokens` | Metadane własnych tokenów, bez sekretów i skrótów |
| `POST /api/profile/mcp-tokens` | `{ "name": "Codex", "canWrite": false, "expiresInDays": 30 }`; odpowiedź zawiera jednorazowy `token` i `details` |
| `DELETE /api/profile/mcp-tokens/{id}` | Unieważnienie własnego tokena |

## Podłączenie Codexa

Przechowuj sekret poza Git i rozmową z modelem. Ustaw `AFTERTAB_MCP_TOKEN`
w środowisku procesu, z którego uruchamiasz Codexa. W PowerShell można wczytać
go bez umieszczania wartości w historii poleceń:

```powershell
$mcpSecret = Read-Host 'AfterTab MCP token' -AsSecureString
$env:AFTERTAB_MCP_TOKEN = [System.Net.NetworkCredential]::new('', $mcpSecret).Password
codex mcp add aftertab --url https://api.example.com/mcp --bearer-token-env-var AFTERTAB_MCP_TOKEN
codex
# Po zakończeniu pracy:
Remove-Item Env:AFTERTAB_MCP_TOKEN
```

Zastąp domenę adresem własnego API. Równoważny wpis w prywatnym `~/.codex/config.toml`:

```toml
[mcp_servers.aftertab]
url = "https://api.example.com/mcp"
bearer_token_env_var = "AFTERTAB_MCP_TOKEN"
```

To konfiguracja klienta, nie plik do dodania do repozytorium. W interfejsie Codexa
sprawdź dostępne narzędzia po uruchomieniu. Zasady zatwierdzania zmian zależą od
klienta; oznaczenia narzędzi nie zastępują jego zgody ani autoryzacji serwera.
Inny klient obsługujący MCP Streamable HTTP z OAuth może połączyć konto tą samą
stroną zgody AfterTab.

Przykładowe polecenie: „Utwórz wydarzenie Kolacja, dodaj wirtualnych uczestników
Ola i Jan. Dodaj pizzę za 100 zł: po 33% dla mnie, Oli i Jana, a resztę wyrównaj
na Olę. Najpierw pokaż podgląd”. Model odczytuje prawdziwe identyfikatory osób,
wywołuje podgląd i dopiero potem zapisuje uzgodnione dane.

## Zakres narzędzi

| Narzędzia | Zakres |
| --- | --- |
| `get_current_user`, `get_user_instructions`, `list_events`, `get_event` | Konto tokena, osobiste instrukcje AI, lista własnych wydarzeń (10 na stronę), szczegóły i uczestnicy |
| `create_event` | Nowe wydarzenie; nie zwraca sekretu zaproszenia |
| `add_virtual_participant`, `rename_virtual_participant`, `remove_virtual_participant` | Zarządzanie wirtualnymi osobami, tylko właściciel otwartego wydarzenia; usunięcie tylko osoby bez powiązań |
| `list_expenses`, `preview_expense`, `create_expense` | Odczyt, podgląd bez zapisu, zapis pozycji wraz z udziałami |
| `update_expense_split`, `set_expense_balance`, `set_expense_payer` | Zmiana podziału, jawne wyrównanie lub cofnięcie go, zmiana płacącego; obecne uprawnienia i `rowVersion` |
| `get_settlement`, `close_event`, `reopen_event` | Plan rozliczenia; zamknięcie/ponowne otwarcie tylko przez właściciela |
| `mark_transfer` | Własne oznaczenie `Sent` albo `Received`, także cofnięcie; bieżąca zamknięta wersja rozliczenia |

Osobiste instrukcje asystenta zapisuje się w panelu Integracje AI i są przypisane do
konta AfterTab, a nie do pojedynczego tokena. MCP udostępnia je tylko temu samemu
uwierzytelnionemu kontu. Bez własnej reguły asystent powinien dopytać, gdy nie wie,
którego uczestnika oznacza wypowiedź użytkownika.

Każda czynność wymaga zdefiniowanego narzędzia — sam model nie dostaje dowolnego
dostępu do API ani bazy. W tej wersji nie ma usuwania/zmiany nazwy wydatku,
dołączania przez zaproszenie, scalania uczestników ani zarządzania kontami.
Do istniejącego wydarzenia trzeba najpierw dołączyć normalnie w AfterTab.
„Otwórz wydarzenie” odczytuje je; ponowne otwarcie rozliczenia to osobne narzędzie.

Kwoty są całkowitymi groszami: `100 PLN = 10000 amountMinor`. Udziały procentowe
to punkty bazowe: `33% = 3300 percentageBps`. `splitMethod` to `Amount` lub `Percentage`.
W metodzie procentowej `amountMinor` każdego udziału wynosi 0, a przy kwotowej
`percentageBps` wynosi 0. `balanceParticipantId` wskazuje osobę już obecną w podziale;
brak wartości pozostawia resztę jako „Inni”. Wyrównanie nigdy nie wybiera osoby samo.

Edycja wymaga aktualnego `rowVersion` z listy wydatków. Oznaczenie przelewu wymaga
aktualnej wersji, pary osób i kwoty z planu rozliczenia. Oznaczenie to deklaracja
użytkownika, nie potwierdzenie bankowe. Tekst wpisany do wydarzenia jest danymi,
a nie instrukcjami dla AI.

## Ponowienia i utrzymanie

Każdy zapis wymaga nowego `operationId` (UUID) dla nowej intencji. Identyczne
ponowienie musi zachować ten sam ID i parametry: serwer zwróci zapisany wynik bez
powtórzenia zmiany. Sprawdza przy tym aktualny token i członkostwo w wydarzeniu.
ID jest unikalne dla konta, także pomiędzy jego tokenami. Zmiana parametrów przy
tym samym ID powoduje błąd.

Rezerwacja operacji jest trwała i powstaje przed zmianą danych. Przy awarii między
zapisem a odpowiedzią operacja może mieć niepewny wynik. Serwer blokuje jej ponowne
wykonanie; trzeba odczytać stan i ustalić, co się zapisało. Dotyczy to również
odrzuconych operacji po rezerwacji. Nie próbuj automatycznie nowego ID po błędzie.

`McpOperations` przechowuje wynik operacji zawierający dane wydarzenia/rozliczenia.
W tej wersji nie ma automatycznego czyszczenia tabeli: kopie zapasowe i retencja
dotyczą również jej. Usunięcie wpisu usuwa ochronę przed ponowieniem jego starego ID;
nie czyść rejestru w czasie aktywnych ponowień. MCP ma limit 120 żądań/min na IP;
przy wielu użytkownikach za jednym proxy trzeba dopasować limit i prawidłowo
skonfigurować zaufane proxy.

Testy jednostkowe obejmują uprawnienia narzędzi, a testy HTTP/SQLite weryfikują
protokół, GUID-y, tworzenie i ponowienia, tokeny tylko do odczytu, unieważnienie,
wygaśnięcie oraz izolację kont. Testy modelu sprawdzają migracje obu dostawców.

```powershell
cd after-tab-api
dotnet test AfterTabApi.sln
```
