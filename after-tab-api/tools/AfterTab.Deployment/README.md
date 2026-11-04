# Migracje własnego wdrożenia

**Polski** | **[English](README.en.md)**

Wszystkie polecenia uruchamiaj z głównego katalogu repozytorium. Wartości
w `<...>` zastąp danymi własnych usług. Narzędzie nie wymaga konkretnej domeny,
subskrypcji ani nazwy bazy. Obsługuje SQL Server/Azure SQL i PostgreSQL.

## Pierwsze wdrożenie i aktualizacje

Wersja z kluczami liczbowymi wymaga pustej bazy i migracji `InitialNumericKeys`.
Przed aktualizacją wcześniejszej instalacji przeczytaj [instrukcję resetu](../../docs/database.md).
Narzędzie nie kasuje danych i odmawia migracji starej lub obcej historii.

Utwórz własną bazę i konto migracyjne z uprawnieniami do schematu. Dla API użyj
oddzielnego konta z uprawnieniami do danych oraz sekwencji HiLo, zgodnie z instrukcją bazy.
API nie wykonuje migracji przy starcie. Nie potrzebuje uprawnień administracyjnych.

## Konfiguracja narzędzia

Ustaw zmienne w środowisku procesu uruchamiającego migracje:

| Zmienna | Wartość |
| --- | --- |
| `Database__Provider` | `SqlServer` albo `PostgreSql`; domyślnie `SqlServer` |
| `ConnectionStrings__AfterTab` | Connection string do własnej bazy, z kontem migracyjnym |

Możesz też ustawić `Database:Provider` i `ConnectionStrings:AfterTab` w .NET User Secrets
projektu narzędzia. Zmienne środowiskowe mają pierwszeństwo. Narzędzie nie wczytuje
konfiguracji API ani plików `.env`. User Secrets są poza Git, ale nie są szyfrowane.
W pipeline korzystaj z magazynu sekretów swojego dostawcy CI/CD.

### SQL Server

```powershell
$env:Database__Provider = 'SqlServer'
$env:ConnectionStrings__AfterTab = 'Server=<sql-host>;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False'
dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
```

### Azure SQL

Dla własnego serwera Azure SQL wybierz `SqlServer` i podaj jego connection string:

```text
Server=tcp:<sql-server>.database.windows.net,1433;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False
```

To przykład uwierzytelniania SQL. Wybierz metodę dostępu obsługiwaną przez własny
serwer i konto migracyjne; rzeczywiste poświadczenia przechowuj poza repozytorium.
Dopuść połączenie z komputera lub runnera w konfiguracji sieciowej swojej bazy.
Jeśli działające API używa managed identity, utwórz jej użytkownika w docelowej
bazie i nadaj dostęp do danych oraz sekwencji. Ustawienia tożsamości i jej identyfikator
pochodzą z Twojego zasobu API. `--migrate` nie tworzy użytkowników i nie nadaje uprawnień.

### PostgreSQL

```powershell
$env:Database__Provider = 'PostgreSql'
$env:ConnectionStrings__AfterTab = 'Host=<postgres-host>;Port=5432;Database=<database>;Username=<migration-user>;Password=<password>;SSL Mode=VerifyFull'
dotnet run --project after-tab-api/tools/AfterTab.Deployment -- --migrate
```

Ten przykład zakłada TLS z zaufanym certyfikatem dla nazwy hosta. Dostosuj konfigurację
certyfikatu do własnego serwera. Lokalny przykład i wariant Docker Compose są opisane
w głównym README oraz [instrukcji mikr.us/VPS](../../../deploy/mikrus/README.md).

## Odczyt stanu i alternatywa EF CLI

Przy tych samych ustawieniach uruchom narzędzie bez `--migrate`, aby odczytać
historię migracji i liczniki danych w przygotowanej bazie. Nie modyfikuje wtedy schematu.

Alternatywnie w katalogu `after-tab-api` wykonaj `dotnet tool restore`, a następnie
`dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API`
dla SQL Server albo wybierz `src/AfterTab.DAL.Migrations.PostgreSql` dla PostgreSQL.
Ustaw provider i connection string zgodne z wybranym silnikiem.

Nie używaj `--bootstrap` z powyższymi ustawieniami: jest to odrębny tryb zgodności,
a nie ogólna konfiguracja własnej instancji. Dla własnego wdrożenia użyj `--migrate`
i nadaj uprawnienia konta API niezależnie od migracji.

## Publikacja

Zbuduj i opublikuj API oraz frontend na wybranym hostingu. Ustaw osobno connection
string działającego API, adres frontendu, CORS, SMTP i własnych klientów OAuth.
Przykłady domen i callbacków są w [głównym README](../../../README.md).
Push na GitHub nie publikuje aplikacji ani nie migruje bazy.
