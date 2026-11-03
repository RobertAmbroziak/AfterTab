# Klucze bazy i wdrożenie pustego schematu

[English](database.en.md)

Ta wersja **zastępuje poprzedni schemat GUID**. SQL Server/Azure SQL i PostgreSQL
mają oddzielne migracje początkowe o nazwie `InitialNumericKeys`. Nie ma konwersji
starych rekordów ani automatycznego kasowania danych. Sam push na `develop` nie resetuje bazy.

## Model kluczy

- Wszystkie 12 tabel ma klucz główny `Id: bigint` (`long` w C#). Klucze obce,
  również pola audytu, są liczbowe. `ExpenseSplits` nie przechowuje publicznego GUID-a.
- `Users`, `Sessions`, `Events`, `EventParticipants` i `Expenses` mają dodatkowy
  `PublicId` GUID/UUID z unikalnym indeksem. Adresy, pola JSON i frontend nadal używają GUID-ów.
  Na SQL Server klucz liczbowy jest klastrowanym PK, a indeks `PublicId` nieklastrowany.
- EF przydziela liczby przez sekwencję HiLo. Klucz jest dostępny po `Add`, przed
  `SaveChanges`, co pozwala zapisać konto z sesją lub wydarzenie z uczestnikiem
  i zaproszeniem w jednej transakcji. Procesy rezerwują osobne bloki liczb.
  Numery mogą mieć luki i nie muszą odpowiadać kolejności zatwierdzania transakcji.
- GUID-y `Version` operacji logowania oraz tokeny sesji i `RowVersion` wydatków
  służą innym celom i pozostają niezależne od kluczy głównych.

## Jednorazowe przejście istniejącej instalacji

Wykonaj podczas planowanego wdrożenia, osobno dla każdego środowiska:

1. Zatrzymaj API i dostęp frontendu do operacji zapisu. Zachowaj kopię starej bazy,
   jeśli chcesz mieć możliwość powrotu do poprzedniej wersji aplikacji.
2. Przygotuj **nową, pustą bazę** wybranego silnika albo świadomie usuń i odtwórz
   dotychczasową bazę aplikacji narzędziem administracyjnym. Starych tabel i starej
   tabeli `__EFMigrationsHistory` nie może być w bazie docelowej. Samo skasowanie
   historii migracji lub wyczyszczenie rekordów nie wystarczy.
3. Ustaw poza Git `Database__Provider` i `ConnectionStrings__AfterTab` dla API
   oraz narzędzia migracyjnego. Użyj konta migracyjnego z uprawnieniami do schematu.
4. Z głównego katalogu repozytorium uruchom:

   ```powershell
   dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
   ```

   Dla Azure z istniejącym trybem Entra użyj zamiast tego `--bootstrap`, zgodnie
   z [instrukcją narzędzia](../tools/AfterTab.Deployment/README.md). Po odtworzeniu
   bazy trzeba ponownie utworzyć użytkownika managed identity i nadać mu uprawnienia.
5. Opublikuj zgodną wersję API i uruchom frontend. Sprawdź utworzenie konta/gościa,
   wydarzenia i wydatku oraz odczyt rozliczenia. Każdy użytkownik zaczyna od nowa:
   konta, sesje, wydarzenia, zaproszenia, wydatki i oznaczenia płatności ze starej bazy nie działają.

Narzędzie `--migrate`/`--bootstrap` odmawia pracy przy obcej/starej historii migracji.
Nie usuwa tabel ani danych, aby naprawić niezgodność. `dotnet ef database update`
również wymaga pustej bazy do pierwszej migracji tej wersji. Do kolejnych aktualizacji
tego schematu stosuj normalne migracje, bez ponownego resetu. Powrót do starego kodu
wymaga przywrócenia kopii starej bazy, nie uruchomienia go na nowym schemacie.

## Uprawnienia do sekwencji

Konto API potrzebuje CRUD tabel oraz pobierania wartości sekwencji
`EntityFrameworkHiLoSequence`:

- SQL Server/Azure SQL: `UPDATE` na sekwencji lub schemacie `dbo`.
  Obecny bootstrap nadaje CRUD na schemacie `dbo`, co obejmuje tę operację.
- PostgreSQL: `USAGE` na schemacie `public` i sekwencji. Skrypt Compose
  `init-runtime-user.sql` nadaje domyślne `USAGE, SELECT` na sekwencjach tworzonych
  przez `aftertab_owner`, więc `aftertab_app` dostaje dostęp także do sekwencji HiLo.

Nie dawaj działającemu API uprawnień do migracji schematu. Przy ręcznym tworzeniu
ról/sekwencji sprawdź również uprawnienia, nie tylko connection string.

## Docker Compose

Na pierwszym wdrożeniu nowy wolumen PostgreSQL jest pusty i `migrate` tworzy schemat.
Istniejący wolumen ze starym schematem wymaga zaplanowanego resetu jak powyżej;
zwykłe przebudowanie obrazów go nie zmienia. Przy odtwarzaniu całej bazy trzeba
ponownie nadać dostęp i domyślne uprawnienia `aftertab_app`, ponieważ skrypt
inicjalizacyjny działa tylko przy pierwszym utworzeniu wolumenu.

`docker compose down -v` usuwa także dane bazy, logi i klucze Data Protection —
nie jest zwykłym krokiem aktualizacji. Szczegóły: [wdrożenie mikr.us](../../deploy/mikrus/README.md).
