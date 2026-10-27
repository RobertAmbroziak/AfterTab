# Zapamiętany gość

Konto `Users` jest trwałe; sesja `Sessions` oznacza tylko aktualne zalogowanie.
Wylogowanie unieważnia sesję i usuwa jej token z PWA. Następne wejście jako gość
korzysta z zapamiętania przeglądarki i wydaje nową sesję dla tego samego `UserId`.

## Zachowanie użytkowe

- Pierwsze logowanie tworzy konto gościa i zapamiętuje je w przeglądarce.
- Kolejne logowanie po wylogowaniu przywraca to samo konto i jego powiązania z wydarzeniami.
- Przycisk `Zapomnij gościa` wymaga świadomego potwierdzenia, unieważnia zapamiętanie
  i wszystkie powiązane z nim sesje. Nie usuwa użytkownika ani historii rozliczeń.
- Po zapomnieniu kolejne wejście jako gość tworzy nowe konto.
- Aktywna sesja sprzed tej zmiany jest przenoszona z zachowaniem `UserId`.
  Nie da się automatycznie odzyskać kont, do których przed aktualizacją utracono już
  jedyny ważny token. Kont nie łączymy po nazwie, adresie IP ani modelu urządzenia.
- Zapamiętanie dotyczy przeglądarki/profilu, nie fizycznego telefonu. Tryb prywatny,
  inna przeglądarka, wygaśnięcie lub usunięcie danych mogą oznaczać utratę dostępu.
  Późniejsze połączenie gościa z kontem powinno zachować ten sam `UserId`.

## Ciasteczko i osobne domeny

Trwały klucz jest losowym sekretem 256-bitowym w ciasteczku
`__Host-aftertab_guest` na domenie API, z `HttpOnly`, `Secure`, `SameSite=Lax`,
ścieżką `/` i bez atrybutu `Domain`. JavaScript nie odczytuje tego klucza.
W `GuestDevices` zapisujemy tylko SHA-256 sekretu, użytkownika, ważność i unieważnienie.
Ważność wynosi 365 dni i jest przedłużana przy wejściu przez logowanie gościa.
Nie jest to gwarancja przechowania: przeglądarka może wcześniej usunąć swoje dane.

Frontend i API mają różne domeny Azure. Dlatego logowanie używa krótkiej wizyty
pierwszoplanowej na domenie API, zamiast polegać na ciasteczkach w żądaniach third-party:

1. PWA tworzy losowy weryfikator i `state`, przechowuje je tymczasowo w `sessionStorage`
   i wysyła skrót weryfikatora do `POST /api/auth/guest/prepare`.
   Serwer akceptuje tylko dozwolony `ReturnOrigin` zgodny z nagłówkiem `Origin`.
2. PWA przechodzi do `GET /api/auth/guest/continue`. Weryfikator jest we fragmencie
   adresu (nie w query ani logach żądań). Strona usuwa go z adresu i przesyła
   w formularzu POST do `/api/auth/guest/authorize` na tej samej domenie API.
3. Dopiero po sprawdzeniu weryfikatora serwer odczytuje/ustawia ciasteczko i przygotowuje
   jednorazowy kod powrotu. Sama znajomość uchwytu żądania z URL nie pozwala
   utworzyć klucza powrotu do istniejącego konta.
4. PWA otrzymuje kod we fragmencie adresu, sprawdza `state` i wymienia kod wraz
   z weryfikatorem przez `POST /api/auth/guest/exchange`.
5. Serwer atomowo konsumuje żądanie i wydaje nową sesję albo wykonuje zapomnienie.

Żądanie i kod są ważne pięć minut, wymagają losowego weryfikatora i nie mogą być
ponownie użyte. W bazie są tylko skróty uchwytu i kodu oraz skrót weryfikatora.
Token sesji i trwały sekret ciasteczka nigdy nie trafiają do URL.
Strona pośrednia ma restrykcyjne CSP i dopuszcza formularz/powrót tylko do API
oraz skonfigurowanych originów PWA. Endpointy przeglądarkowe mają limit 40
żądań/minutę/IP, czyli około dziesięciu pełnych przepływów.

## Dane i zgodność

Migracja `RememberGuestBrowser` dodaje tabele `GuestDevices`, `GuestLoginRequests`
oraz opcjonalne `Sessions.GuestDeviceId`. Jest addytywna i zachowuje starsze rekordy.
`Version` w żądaniu jest tokenem współbieżności, aby równoległa wymiana nie wydała
dwóch sesji z tego samego kodu. Unieważnione urządzenie blokuje również autoryzację
jego sesji, nawet przy wyścigu z wylogowaniem/zapomnieniem.

Mechanizm klucza gościa odmawia przywrócenia użytkownika nieaktywnego lub takiego,
który ma już `UserIdentity`. Przyszłe połączenie z kontem musi też unieważniać
klucze gościa, aby nie zastępowały logowania do konta.

Pozostały krótkotrwały token sesji jest nadal przechowywany w dotychczasowym
localStorage. Zmiana przenosi do HttpOnly trwały klucz powrotu; nie zastępuje całego
obecnego mechanizmu Bearer. Flaga `aftertab.guestRemembered` jest wyłącznie
wskazówką dla interfejsu i nigdy nie służy do uwierzytelnienia.

## Weryfikacja

20 testów .NET obejmuje poprzednie funkcje oraz powrót po wylogowaniu, zapomnienie,
wygaśnięcie sesji, utratę ciasteczka, migrację starego gościa, nieprawidłowy weryfikator,
ponowne użycie kodu i odmowę przywrócenia konta zarejestrowanego/zablokowanego.
Test Chrome sprawdza cały przepływ i atrybuty ciasteczka, powrót z samego ciasteczka
bez localStorage, nową sesję ze stałym `UserId` oraz nowego gościa po zapomnieniu.
Fizyczny iPhone i instalowana PWA na iOS wymagają osobnego sprawdzenia na urządzeniu.
