# Rozliczenia i zamykanie wydarzeń — lokalnie

Aktualizacja lokalna z 27.10.2026: rozliczamy osobno wskazanego płacącego, a zamknięte
przelewy mają prywatne oznaczenia wysłania i odbioru. Szczegóły i zmiana zachowania
przy ponownym otwarciu: [DARK-MODE-AND-PAYMENTS.md](DARK-MODE-AND-PAYMENTS.md).

Wdrożono na Azure 25.10.2026 na wyraźną prośbę użytkownika. Migracja
`20261025174432_EventSettlementLifecycle` dodaje `Events.ClosedAtUtc`
i `Events.SettlementVersion`. Zastosowano ją do lokalnej bazy
`AfterTab` na `(localdb)\MSSQLLocalDB` oraz do Azure `AfterTabDb`, z zachowaniem danych.
Kolejne wdrożenie wymaga osobnej prośby użytkownika.

## Przyjęty przebieg

1. W otwartym wydarzeniu podsumowanie jest podglądem zapisanych pozycji.
   Przelicza się po zapisie/zmianie podziału, po „Odśwież”, co 15 sekund
   na widocznym ekranie oraz po powrocie do aplikacji. Niezatwierdzony
   formularz nie wchodzi do rozliczenia i blokuje przycisk zamknięcia w tym oknie.
2. Tabela „Kto komu oddaje” pokazuje uproszczony plan przelewów.
   Rozwijane „Salda uczestników” pokazują zapłacone kwoty, przypisane koszty
   oraz wynik netto. Udział własny płacącego nie tworzy długu.
3. „Inni” pozostaje osobnym, nieprzypisanym kosztem. Nie tworzymy fikcyjnego
   dłużnika ani nie obciążamy tym automatycznie pozostałych osób.
   Podgląd jest wtedy oznaczony jako niepełny, a zamknięcie jest blokowane.
4. Autor każdej pozycji może wybrać „Popraw podział”, przypisać nowe osoby
   lub użyć „Wyrównaj”. Nazwa, cała kwota i płacący pozostają bez zmian.
   Autor wydarzenia nie może poprawiać cudzych pozycji.
5. Tylko autor wydarzenia może kliknąć „Zamknij i rozlicz”. Wszystkie pozycje
   muszą mieć pełny podział, bez „Inni”. Użycie „Wyrównaj” nie jest obowiązkowe,
   jeśli suma jest już poprawna. Puste wydarzenie ma puste, poprawne rozliczenie.
6. Zamknięcie blokuje tworzenie pozycji, zmianę podziałów i dołączanie.
   Istniejący uczestnicy nadal widzą pozycje i zatwierdzony plan.
7. „Otwórz ponownie” odblokowuje wydarzenie i zmienia wynik na podgląd.
   Po uzupełnieniu danych kolejne zamknięcie zwiększa wersję rozliczenia.
   Powtórzenie tego samego zamknięcia/otwarcia jest idempotentne.

Zamknięcie zatwierdza plan, nie potwierdza wykonania płatności. Rejestrowanie
przelewów nie jest jeszcze zaimplementowane. Ponowne otwarcie wymaga świadomego
potwierdzenia, szczególnie jeśli ktoś już zapłacił na podstawie wcześniejszego planu.
Wersja to numer zatwierdzenia; nie archiwizujemy osobnych historycznych planów.

## Kompensacja

Wszystko liczymy na serwerze w całkowitych groszach:

`saldo = zapłacono - przypisany koszt - własna należność od „Inni”`.

„Inni” jest wyłączone z bieżących przelewów, więc suma znanych sald wynosi zero.
Po pełnym przypisaniu kosztów saldo jest po prostu `zapłacono - koszt`.
Algorytm łączy największe saldo do oddania z największą należnością;
każdy krok zeruje co najmniej jedno saldo. Remisy rozstrzyga `UserId`,
więc wynik nie zależy od imienia, kolejności pozycji ani kolejności ich odczytu.

Przykłady:

- Jarek winien Tomkowi 3 zł, Tomek Jarkowi 12 zł → Tomek oddaje Jarkowi 9 zł.
- Adam winien Jarkowi 15 zł, Jarek Tomkowi 15 zł → Adam oddaje Tomkowi 15 zł.
- Zamknięty cykl równych zobowiązań → brak przelewów.

Każda osoba kończy z dokładnie swoim saldem. Nie ma wzajemnych par ani cykli
przelewów. Algorytm daje najwyżej `n - 1` przelewów dla `n` osób z niezerowymi
saldami, ale nie gwarantuje globalnie najmniejszej możliwej liczby przelewów.
Kompensacja może wskazać odbiorcę innego niż pierwotny autor konkretnej pozycji.

## API i spójność

- `GET /api/events/{id}/settlement` — podgląd/zatwierdzony wynik tylko dla
  aktywnego uczestnika. Kwoty, salda, przelewy i lista pozycji z „Inni”.
- `POST /api/events/{id}/close` — tylko autor wydarzenia; odpowiedź zawiera
  wynik, wersję i czas zamknięcia. Niepełny podział daje 409.
- `POST /api/events/{id}/reopen` — tylko autor wydarzenia; wynik znów jest podglądem.
- `PUT /api/events/{id}/expenses/{expenseId}/split` — tylko autor pozycji
  w aktywnym wydarzeniu. Ciało: `splitMethod`, `shares`, `balance`, `rowVersion`.
  `rowVersion` to Base64 z odpowiedzi GET/POST pozycji; nieaktualna wersja daje 409.

Podgląd odczytuje wydarzenie, pozycje i podziały w jednej transakcji Serializable,
więc nie miesza stanów sprzed i po równoczesnym zapisie. Zamykanie/otwieranie,
tworzenie i zmiana podziałów także używają Serializable oraz ponownie sprawdzają
stan wydarzenia w transakcji. Strategia EF ponawia przejściowe błędy SQL Server;
tracker jest czyszczony przed próbą, żeby nie ponawiać operacji na starym stanie.
Aktualizacja podziału dotyka również nadrzędnej pozycji, aktualizując SQL rowversion.

Nie ma dodatkowej tabeli kopii rozliczeń. Kwoty zatwierdzonego wyniku wynikają
z zablokowanych pozycji i deterministycznego algorytmu; zmiana imienia zmienia
wyłącznie etykiety. Otwarcie jawnie odblokowuje dane, unieważniając ostateczność
poprzedniej wersji. Nie uruchamia się żadnych faktycznych przelewów bankowych.

## Weryfikacja

- 46 testów .NET: 17 jednostkowych i 29 integracyjnych.
- W tym wzajemne długi, łańcuchy i cykle, koszt własny, „Inni”, te same imiona,
  dokładne salda dla 500 pozycji/30 osób, deterministyczność i brak wzajemnych par.
- Integracyjne testy uprawnień, blokad, poprawiania podziału, niepełnego wyniku,
  wersji zamknięcia i odrzucenia zapisu przygotowanego przed zamknięciem.
- API Release oraz lint/build PWA przechodzą.
- Chrome + prawdziwy SQL Server LocalDB: 3 zł kontra 12 zł → 9 zł; uzupełnienie
  „Inni”; zmiana SQL rowversion i odrzucenie starej wersji; blokada tworzenia,
  edycji i dołączania po zamknięciu; ponowne otwarcie i wersja 2 zmieniająca
  kierunek przelewu; automatyczne odświeżenie drugiego uczestnika.
- Równoczesne zamykanie i tworzenie pozycji na LocalDB, także z „Inni”:
  nie powstało zamknięte wydarzenie z nieprzypisanym kosztem.
- Ekran 390 px, odczyt po ponownym otwarciu strony, brak błędów JavaScript.
- Powtórzone testy regresji pozycji oraz imion/logowania/zaproszeń.

Opisane testy lokalne tworzą konta, wydarzenia i pozycje wyłącznie w LocalDB.
Lokalne procesy API i Vite są zatrzymane. Osobne, zlecone wdrożenie Azure z migracjami
opisano w [AZURE-DEVELOPMENT.md](AZURE-DEVELOPMENT.md).
