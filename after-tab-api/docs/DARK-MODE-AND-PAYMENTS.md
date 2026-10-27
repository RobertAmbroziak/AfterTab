# Grafit, mięta, płacący i prywatne oznaczenia — 27.10.2026

Późniejsza poprawka **wdrożona na Azure 27.10.2026 na prośbę użytkownika**: Vite automatycznie
dopisuje do adresów ikon w manifeście, favicon i apple-touch-icon parametr `v`
z SHA-256 zawartości pliku. Po zmianie bitmapy zmienia się więc także adres ikony,
co umożliwia wykrycie aktualizacji przez przeglądarkę. Wersjonowane adresy są również
w cache offline. `id`, `start_url` i nazwy plików pozostają bez zmian.
Lint/build oraz zawartość wygenerowanego manifestu i service workera sprawdzono.

Wdrożono na Azure 27.10.2026 na wyraźną prośbę użytkownika.
Migracja `20261027140319_PayersAndPrivateTransfers` jest zastosowana
w LocalDB `(localdb)\MSSQLLocalDB`, baza `AfterTab`, oraz w Azure `AfterTabDb`.
Zachowano wcześniejsze dane. Każdy kolejny deploy wymaga osobnej prośby.

## Interfejs

- Kompaktowy nagłówek: marka z dostarczoną ikoną, „Wydarzenia” i menu konta.
  Zmiana imienia, wylogowanie i zapomnienie gościa są w menu konta.
  Instalacja ma osobny przycisk w nagłówku. „Wydarzenia” używają ikony kalendarza;
  na telefonie przyciski wydarzeń i instalacji pokazują ikony z dostępnymi nazwami.
- Grafitowe tło i panele, mięta jako główny kolor akcji. Duży panel powitalny
  jest zastąpiony prostym ekranem logowania i nagłówkiem dla zalogowanych.
- W wydarzeniu: uczestnicy, statystyki oraz widoczna od razu kwota do oddania
  lub otrzymania. Dla zamkniętego planu własne oznaczenia wysłania/odbioru
  pomniejszają tę informacyjną kwotę. Saldo rozliczenia pozostaje niezmienione.
- Komputer: wydatki i rozliczenie w dwóch kolumnach.
- Telefon: zakładki „Wydatki / Rozliczenie” oraz przycisk dodawania przy dolnej
  krawędzi. Szczegóły podziałów są domyślnie zwinięte.
- Ręczne wklejenie zaproszenia ma opis wyjaśniający, że zwykłe kliknięcie linku
  otwiera zaproszenie bez wklejania; przeglądarka komunikatora może mieć osobną sesję.
- Dostarczone pliki ikon skopiowano bez „(1)” do `public/pwa-192x192.png`
  i `public/pwa-512x512.png`. Manifest, favicon i apple-touch-icon używają nowych ikon.

## Procenty

Nowa pozycja domyślnie wybiera podział procentowy i wszystkich bieżących uczestników.
Przy dodaniu/usunięciu osoby zaznaczone udziały są od nowa wypełniane równymi,
pełnymi procentami: 3 → 33%, 2 → 50%, 4 → 25%. Pola można ręcznie zmienić.
Zmiana zestawu osób nadpisuje wcześniejsze ręczne udziały nowymi równymi wartościami.
Przy przełączeniu na procenty wyliczane są domyślne wartości wybranych osób.
Pozostałość trafia do „Inni” lub jest przypisywana przez „Wyrównaj”, jak wcześniej.
Edycja zapisanego podziału rozpoczyna się od zapisanych wartości.

## Autor a płacący

- `Expense.AuthorUserId` jest nowym, stałym identyfikatorem twórcy pozycji.
  `PaidByParticipantId` pozostaje identyfikatorem osoby, która zapłaciła.
- Migracja uzupełnia autora z audytu `CreatedByUserId`, a przy jego braku z
  dotychczasowego płacącego. Dopiero potem zakłada FK do Users.
- Każdy uczestnik dodaje własną pozycję, domyślnie płacąc samemu.
- Tylko autor wydarzenia może wskazać inną osobę płacącą przy tworzeniu pozycji
  lub zmienić płacącego w istniejącej pozycji. Osoba musi być aktywnym uczestnikiem.
- Zmiana płacącego nie zmienia autora, całej kwoty, udziałów ani historii audytu.
  Nadal tylko autor pozycji poprawia jej podział. Wyrównanie remisu nadal trafia
  do autora pozycji, zgodnie z wcześniejszą regułą.
- Rozliczenie nalicza zapłacone kwoty i należność „Inni” płacącemu, a nie twórcy wpisu.
- Zmiana płacącego wymaga aktywnego wydarzenia i aktualnego SQL rowversion.
- Nowy endpoint `PUT /api/events/{id}/expenses/{expenseId}/payer` przyjmuje
  `{ paidByUserId, rowVersion }`. POST pozycji przyjmuje opcjonalne `paidByUserId`.
  Backend ponownie egzekwuje wszystkie uprawnienia i przynależność do wydarzenia.

## Prywatne oznaczenia przelewów

- Oznaczać można tylko przelewy z aktualnego, zamkniętego rozliczenia.
  Oznaczenia nie zmieniają wyliczonych sald ani kwot przelewów.
- Nadawca może zaznaczać/cofać wyłącznie „Wysłane”, odbiorca wyłącznie „Odebrane”.
  Każda strona widzi oba statusy, ale drugi checkbox jest dla niej nieaktywny.
- Pozostali uczestnicy, także autor wydarzenia niebędący stroną przelewu,
  nie widzą statusów. Odpowiedź JSON w ogóle nie zawiera dla nich pola
  `acknowledgement`, a zapytanie repozytorium odczytuje wyłącznie oznaczenia stron.
- `TransferConfirmations` przechowuje kwotę, strony, numer wersji, daty oznaczeń
  i audyt. Unikalny indeks: wydarzenie + wersja + nadawca + odbiorca.
- `PUT /api/events/{id}/settlement/confirmation` przyjmuje wersję, strony,
  kwotę, `side` (`Sent`/`Received`) i `value` (bool). Backend sprawdza dokładny
  przelew wyliczony przez serwer oraz uprawnienia zalogowanej osoby.
- Zapis jest transakcyjny i nie nadpisuje znacznika drugiej strony.
- Ponowne otwarcie nie usuwa oznaczeń: przechodzą do prywatnego `previousPayments`.
  Kolejna wersja ma własne, początkowo puste checkboxy. Uczestnik widzi ostrzeżenie
  w swojej historii. Inne osoby nie otrzymują jego historii przez API.
- To oznaczenia użytkowników, nie integracja bankowa ani księgowanie płatności.
  Nowy plan po otwarciu nie odejmuje automatycznie przelewów z poprzednich wersji;
  trzeba sprawdzić historię przed wykonaniem kolejnego przelewu. Ostrzeżenie jest
  również w potwierdzeniu otwarcia wydarzenia.

## Poranny start i iPhone

W taniej konfiguracji Azure API nie ma Always On, a baza ma auto-pause.
Dokumentacja App Service opisuje usunięcie aplikacji z pamięci po 20 minutach
bezczynności; Azure SQL serverless zwykle potrzebuje około minuty na wznowienie.
Są to ograniczenia infrastruktury. Nie zmieniano planów ani kosztów.

Frontend po 6 sekundach pokazuje komunikat o wybudzaniu. Żądania kończą się
kontrolowanym błędem po 25 sekundach, z przyciskiem ponowienia połączenia.
Timeout nie usuwa tokenu sesji ani zapamiętanego gościa. Po błędzie zapisu
komunikat zaleca odświeżenie przed ponowieniem, bo serwer mógł zapisać dane.

Na iPhonie strona nie może otworzyć androidowego `beforeinstallprompt`.
Przycisk pokazuje instrukcję „Udostępnij → Dodaj do ekranu początkowego”.
Nowe ikony i wpis `apple-touch-icon` są przygotowane dla tego sposobu instalacji.

Źródła:

- https://learn.microsoft.com/en-us/azure/app-service/configure-common
- https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-auto-pause-resume
- https://webkit.org/blog/17333/webkit-features-in-safari-26-0/
- https://web.dev/learn/pwa/installation-prompt

## Weryfikacja

- API Release: bez błędów i ostrzeżeń. 48 testów .NET: 17 jednostkowych,
  31 integracyjnych. Nowe testy sprawdzają autora/płacącego, uprawnienia,
  prywatność JSON, obie strony oznaczeń, cofanie i rozdzielenie wersji.
- React: lint i build PWA przechodzą.
- Chrome + LocalDB: cztery niezależne konta, menu użytkownika, procenty
  33 → 50 → 33 → 25, ręczna edycja, utworzenie pozycji z innym płacącym,
  zmiana płacącego z zachowaniem autora/rowversion, prywatne checkboxy,
  zakładki mobilne, przycisk dodawania i historia po ponownym otwarciu.
- Test wolnej odpowiedzi: komunikat o wybudzaniu, timeout po 25 s, zachowany
  token i poprawny powrót do konta po „Ponów połączenie”.
- Widoki 1440 px i 390 px: bez poziomego przewijania; brak błędów JavaScript.
- Hash obu ikon w `public` jest identyczny z dostarczonymi plikami.

Opisane testy lokalne pozostawiły konta i wydarzenia wyłącznie w LocalDB.
Lokalne serwery testowe są zatrzymane. Osobne, zlecone wdrożenie Azure i jego
weryfikację opisano w [AZURE-DEVELOPMENT.md](AZURE-DEVELOPMENT.md).
