# Imiona, wydarzenia i zaproszenia — zmiany lokalne

Te zmiany opublikowano na Azure 25.10.2026 na wyraźną prośbę użytkownika. Zgodnie z jego preferencją
każdy kolejny deploy wymaga osobnej, wyraźnej prośby. Nie uruchamiać automatycznie
publikacji ani migracji Azure po zakończeniu pracy lokalnej.

## Imię

`Users.DisplayName` pozostaje wspólną nazwą dla gościa oraz przyszłych kont lokalnych,
Google i Facebook. Nowe pole `DisplayNameConfirmed` rozróżnia nazwę zatwierdzoną
przez użytkownika od placeholdera/propozycji dostawcy. Nie ma unikalnego indeksu
na imieniu: identyfikacja, uczestnictwo i uprawnienia używają `UserId`.

- Nowy gość podaje imię przed rozpoczęciem logowania. Jest ono przenoszone w
  krótkotrwałym `GuestLoginRequest.ProposedDisplayName` i używane przy tworzeniu konta.
- Przywrócenie zapamiętanego konta nie nadpisuje istniejącego imienia propozycją z formularza.
- Stary gość bez potwierdzonego imienia uzupełnia je przed tworzeniem lub dołączaniem
  do wydarzeń. Kontrolę egzekwuje również backend.
- `PUT /api/profile/display-name` pozwala dowolnemu aktywnemu, zalogowanemu
  użytkownikowi zmienić imię. Walidacja: 1–100 znaków po obcięciu zewnętrznych spacji,
  bez znaków sterujących. Zmiana nie tworzy nowego konta ani uczestnictwa.
- `UserDto.RequiresDisplayName` steruje formularzem pierwszego potwierdzenia.
  Przyszła rejestracja e-mail powinna potwierdzać imię wpisane w formularzu;
  pierwsze logowanie z Google/Facebook powinno proponować nazwę dostawcy przy
  `DisplayNameConfirmed=false`. Interfejs potrafi ją wstępnie wypełnić.
- Integracje logowania Google/Facebook i rejestracja e-mail pozostają osobnym etapem.

Migracja `20261025163751_ConfirmDisplayName` dodaje dwa pola i oznacza istniejące,
niepuste nazwy inne niż `Gość` jako potwierdzone. Została zastosowana
do lokalnej bazy `AfterTab` w LocalDB oraz do `AfterTabDb` w Azure, z zachowaniem danych.

## Wydarzenia

`POST /api/events` przyjmuje nazwę (1–150 znaków) i opcjonalny opis (do 2000 znaków).
Wybór waluty został usunięty; nowe wydarzenia mają PLN. `OwnerUserId` i audyt pochodzą z sesji,
a nie z DTO. Jeden `SaveChanges` atomowo tworzy wydarzenie, autora jako aktywnego
uczestnika i pierwsze zaproszenie. Odpowiedź 201 zawiera szczegóły oraz surowy token
zaproszenia przeznaczony do skopiowania.

- `GET /api/events` zwraca wydarzenia autora i aktywnego uczestnika, posortowane po nazwie.
- `GET /api/events/{id}` pokazuje szczegóły i aktualne imiona uczestników wyłącznie
  autorowi lub aktywnemu uczestnikowi. Obca osoba otrzymuje 404.
- Autor i zaproszony uczestnik widzą wydarzenie na swojej liście; lista i szczegóły
  mają przycisk „Odśwież”. Szczegóły wydarzenia i rozliczenie odświeżają się również
  co 15 sekund na widocznym ekranie i po powrocie do okna. Nie ma WebSocketów.
- Nazwy użytkowników są odczytywane z `Users`, więc ich zmiana jest widoczna po
  odświeżeniu listy/szczegółów, z zachowaniem tych samych powiązań.

## Zaproszenie

Link ma postać `https://<adres-PWA>/#/join/<64-znakowy-losowy-token>`.
Token to 256-bitowy losowy sekret. W `EventInvites` przechowujemy tylko SHA-256.
Nie ma go w ścieżce ani query początkowego żądania do hostingu PWA.
Podgląd i dołączenie przekazują token w ciele POST, nie w URL API:

- `POST /api/invites/preview` — publiczny podgląd: nazwa wydarzenia, autor, waluta
  i informacja o obecnym uczestnictwie. Nie dodaje uczestnika i nie ujawnia jego listy.
- `POST /api/invites/join` — wymaga sesji i potwierdzonego imienia. Dołączenie jest
  jawne; ponowienie nie tworzy duplikatu. Unikalny indeks `(EventId, UserId)` oraz
  transakcja Serializable zabezpieczają zapis. Przejściowy deadlock SQL może być ponowiony.
- `POST /api/events/{id}/invite` — tylko autor; unieważnia poprzednie zaproszenia
  i wydaje nowy token w jednej transakcji.
- `DELETE /api/events/{id}/invite` — tylko autor; unieważnia zaproszenia.

Zaproszenie jest wielokrotnego użytku, domyślnie bez terminu wygaśnięcia, do
unieważnienia przez autora. Każdy posiadacz linku może dołączyć po zalogowaniu.
Backend respektuje również `ExpiresAtUtc`, zamknięte wydarzenie i usuniętych uczestników.
Unieważnienie lub rotacja nie usuwa osób już należących do wydarzenia.
Podgląd/dołączenie mają osobny limit 60 żądań/minutę/IP, niezależny od logowania.

Autor otrzymuje przyciski „Kopiuj link” i „Udostępnij”. Udostępnianie używa
systemowego okna telefonu, jeśli jest dostępne; w przeciwnym razie kopiuje link.
Gdy schowek jest zablokowany, pole pozwala zaznaczyć link i skopiować go ręcznie.
Surowy link jest dostępny w pamięci otwartego widoku po utworzeniu/rotacji.
Po przeładowaniu należy użyć zachowanej kopii lub świadomie wygenerować nowy link,
co unieważnia wcześniejszy. Nie zapisujemy sekretu zaproszenia jawnie w bazie.

## Logowanie i link z Messengera

Hash i `hashchange` pozwalają otworzyć zaproszenie w już działającej aplikacji.
Nowe okno w tej samej przeglądarce korzysta z zapisanej sesji. Sposób wyboru okna
przez system lub Messengera pozostaje zależny od urządzenia.

Zaproszenie jest zachowywane w `sessionStorage` podczas przekierowania logowania.
Po jego zakończeniu użytkownik wraca do podglądu i nadal musi nacisnąć „Dołącz”.
Jeśli już należy do wydarzenia, widzi przycisk „Otwórz wydarzenie”.

Wbudowana przeglądarka Messengera może nie mieć tej samej sesji/co PWA. Dlatego
ekran listy ma „Dołącz linkiem lub kodem”: użytkownik wkleja pełny link albo sam
64-znakowy kod. Aplikacja wyodrębnia wyłącznie token i wywołuje własne API;
nie odwiedza ani nie pobiera wklejonego zewnętrznego adresu.

## Sprawdzenia lokalne

- 46 testów .NET (łącznie z pozycjami i [rozliczeniami](SETTLEMENTS.md)): w tym uprawnienia, idempotentne dołączanie, unieważnianie/rotacja,
  odrzucenie zamkniętych/wygasłych zaproszeń i usuniętych uczestników, walidacja imienia,
  zachowanie `UserId`, rollback agregatu oraz nazwany gość i powrót do niego.
- Kompilacja API Release, lint i kompilacja PWA.
- Chrome + LocalDB: autor i dwóch uczestników, logowanie z zachowaniem zaproszenia,
  jawne dołączenie, listy, zmiana imienia, wklejenie linku, stary gość wymagający
  imienia, 100-znakowe imię na szerokości 390 px, rotacja i unieważnienie bez
  usuwania członków, wylogowanie/powrót i brak błędów JavaScript.
- Testy przeglądarkowe pozostawiły testowych użytkowników i wydarzenia tylko w LocalDB.

Po zatrzymaniu poprzedniej instancji backendu uruchom go ponownie w Visual Studio
profilem `https` (port 7180), a frontend poleceniem `npm run dev` (port 5173).
Testy używały osobnych portów 7280 i 5174 i nie zmieniały działającej starej instancji.
Nie używaj nowych ekranów z wcześniejszą wersją backendu.

Wdrożenie Azure z migracjami wykonano na wyraźną prośbę użytkownika 25.10.2026.
Szczegóły: [AZURE-DEVELOPMENT.md](AZURE-DEVELOPMENT.md). Każde kolejne wdrożenie wymaga osobnej prośby.
