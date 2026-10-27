# Pozycje wydarzeń — zmiany lokalne

Aktualizacja lokalna z 27.10.2026: domyślny podział jest procentowy, udziały
uzupełniają się po równo, a autor i płacący są oddzieleni.
Szczegóły: [DARK-MODE-AND-PAYMENTS.md](DARK-MODE-AND-PAYMENTS.md).

Wdrożono na Azure 25.10.2026 na wyraźną prośbę użytkownika.
Migracja `20261025171442_ExpenseOthers` jest zastosowana w lokalnej bazie
`AfterTab` na `(localdb)\MSSQLLocalDB` oraz w Azure `AfterTabDb`.
Każdy kolejny deploy wymaga osobnej prośby użytkownika.

## Działanie

- Z formularza wydarzenia usunięto wybór waluty. `CreateEventDto` przyjmuje nazwę
  i opis, backend nadaje PLN. Istniejące pole bazy `Events.Currency` pozostaje
  dla zgodności struktury; nie jest już prezentowane jako wybór w aplikacji.
- Autor i każdy aktywny uczestnik mogą dodać pozycję: nazwę do 200 znaków,
  kwotę w złotych i podział kwotowy albo procentowy.
- Kolorowe ikonki z pierwszą literą imienia wybierają osoby do podziału.
  Powiązania używają `UserId`, więc te same imiona nie łączą kont.
- „Zatwierdź” zapisuje pozycję i jej ostateczny podział. Przed zatwierdzeniem
  wszystkie dane są formularzem lokalnym, a podgląd pokazuje końcowe kwoty.
- Nierozdzielona część jest zapisywana jako „Inni”, bez tworzenia fikcyjnego
  użytkownika. Można przypisać tam nawet całą kwotę.
- Późniejsze dołączenie uczestnika nie zmienia historycznych udziałów.
  Autor pozycji może teraz poprawić jej podział w otwartym wydarzeniu; szczegóły
  w [SETTLEMENTS.md](SETTLEMENTS.md). Nie ma automatycznego przepisywania udziałów.
- „Wyrównaj” dolicza pozostałą kwotę osobie z największą kwotą udziału.
  Przy remisie trafia ona do autora **pozycji**, także jeśli nie był zaznaczony.
  Gdy nie ma udziałów, całość również trafia do autora pozycji.
  Każda późniejsza zmiana kwoty, metody lub udziałów wyłącza wyrównanie.
- Lista jest wspólna dla uczestników. Przycisk wydarzenia „Odśwież” aktualizuje
  również pozycje. Zmiana imienia jest widoczna po ponownym odczycie.

## Kwoty i zaokrąglenia

Kwoty mają typ `long` w groszach. Procenty to punkty bazowe: 3300 = 33%,
3333 = 33,33%. Obsługiwane są najwyżej dwa miejsca po przecinku, w formularzu
zarówno przecinek, jak i kropka. Maksymalna kwota pozycji: 1 000 000 000 zł.

Dla udziału procentowego kwota jest zaokrąglana w dół do pełnego grosza.
Reszta pozostaje jawna w „Inni” aż do użycia „Wyrównaj”.
Przykład: 62,50 zł i 33% + 33% + 33% daje 20,62 + 20,62 + 20,62 + 0,64 zł „Inni”.
Wyrównanie daje autorowi pozycji 21,26 zł, pozostałym po 20,62 zł.

Backend ponownie liczy udziały i nie ufa podglądowi klienta. Odrzuca ujemne
kwoty, sumę powyżej kwoty pozycji, procenty powyżej 100%, duplikaty osób,
mieszanie kwot/procentów oraz udział osoby spoza wydarzenia.
Zapisywany niezmiennik:

`sum(ExpenseSplits.AmountMinor) + Expenses.OthersAmountMinor = Expenses.AmountMinor`.

`PercentageBps` zachowuje pierwotnie podany procent; po wyrównaniu ostateczna
kwota może mu nie odpowiadać. Interfejs opisuje go jako „zadano …%” i pokazuje
znacznik „Wyrównano”. Rozliczenia mają używać zapisanych kwot `AmountMinor`.

## API i zapis

- `GET /api/events/{eventId}/expenses` — lista tylko dla aktywnego uczestnika.
- `POST /api/events/{eventId}/expenses` — utworzenie pozycji z podziałem, odpowiedź 201.

Przykład ciała POST:

```json
{
  "name": "Pizza",
  "amountMinor": 6250,
  "splitMethod": "Percentage",
  "shares": [
    { "userId": "<guid uczestnika>", "percentageBps": 3300, "amountMinor": 0 }
  ],
  "balance": false
}
```

Autor, uczestnik opłacający (na tym etapie ta sama osoba), data i audyt pochodzą
z serwera/sesji. Pole `userId` w udziałach jest mapowane na istniejący
`EventParticipant.Id`, a złożone FK dodatkowo pilnują zgodności wydarzenia.
Obca osoba otrzymuje 404; zamknięte wydarzenie lub brak potwierdzonego imienia
blokują tworzenie. EF zapisuje pozycję i jej udziały w jednej transakcji.

Migracja dodaje `Expenses.OthersAmountMinor`, `Expenses.Balanced` oraz CHECK
zakresu „Inni”. Dotychczasowe tabele Expenses/ExpenseSplits są ponownie używane.
Stary endpoint kalkulatora `/api/splits/preview` pozostaje niezależnym przykładem
podziału pełnych 100%; nowy zapis pozycji korzysta z `ExpenseAllocation`.

## Weryfikacja

- 46 testów .NET: 17 jednostkowych, 29 integracyjnych; 9 testów samych pozycji obejmuje
  resztę, remisy, maksymalne kwoty, odrzucenie nadmiaru, uprawnienia, zapis,
  zmianę imienia i zachowanie „Inni” po dołączeniu nowej osoby.
- Build API Release, lint i build React/PWA — poprawne.
- Chrome + lokalny SQL Server LocalDB: autor i dwaj uczestnicy, oba rodzaje
  podziału, 33/33/33%, remis z niezaznaczonym autorem, całość „Inni”, blokady
  nadmiaru i obcych kont, ponowne otwarcie oraz dokładne sumy pięciu pozycji.
- Widok 390 px mieści formularz i listę; brak błędów JavaScript.
- Powtórzone testy logowania gościa, imion, zaproszeń, rotacji i wylogowania.
- Testy pozostawiły testowe konta, wydarzenia i pozycje wyłącznie w LocalDB.

Po testach lokalne procesy API i Vite zostały zatrzymane.
Standardowe uruchomienie: API profilem `https` (7180), React `npm run dev` (5173).
