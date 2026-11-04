# Migracje własnego wdrożenia SQL Server

**Polski** | **[English](README.en.md)**

Ta gałąź aplikacji obsługuje SQL Server/Azure SQL. Dla własnego wdrożenia użyj
standardowego EF CLI, który odczytuje `ConnectionStrings__AfterTab` i nie wymaga
konkretnego serwera, nazwy bazy, konta Azure ani tożsamości API.
Wartości w `<...>` zastąp danymi swojej bazy.

## Migracje

Z głównego katalogu repozytorium przejdź do `after-tab-api`:

```powershell
cd after-tab-api
$env:ConnectionStrings__AfterTab = 'Server=<sql-host>;Database=<database>;User ID=<migration-user>;Password=<password>;Encrypt=True;TrustServerCertificate=False'
dotnet restore
dotnet tool restore
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
```

W Azure SQL host ma postać `tcp:<sql-server>.database.windows.net,1433`.
Utwórz własną bazę i konto migracyjne z uprawnieniami do schematu. Przykład używa
uwierzytelniania SQL; wybierz metodę dostępu dopuszczoną przez Twój serwer.
Poświadczenia ustaw w środowisku procesu lub magazynie sekretów pipeline, poza Git.
Dopuść połączenie w regułach sieciowych własnego serwera.

Przed aktualizacją istniejącej bazy sprawdź connection string i wykonaj kopię.
Migracje tej gałęzi nie są migracjami kluczy liczbowych z `develop` — dokumentację
i polecenia zawsze stosuj z tej samej wersji co publikowany kod.

## Konto działającego API

Skonfiguruj connection string API niezależnie od konta migracyjnego. Konto API
potrzebuje uprawnień do danych, a nie do zmiany schematu. Jeśli używasz managed
identity w Azure, utwórz użytkownika tej tożsamości w swojej bazie i nadaj mu dostęp.
Nazwa oraz identyfikator tożsamości pochodzą z Twojego zasobu API.
API nie wykonuje migracji przy starcie.

## Ograniczenia pomocnika w tym katalogu

Program `AfterTab.Deployment` na tej gałęzi zawiera starszy tryb z dodatkowymi
ograniczeniami bazy i nazwy użytkownika. Nie jest uniwersalnym mechanizmem
konfiguracji własnej instancji. Powyższy wariant EF CLI działa niezależnie od tego
pomocnika; nie wymaga `--bootstrap`, jego ustawień ani kont oryginalnego wdrożenia.

## Publikacja

Opublikuj API i frontend na wybranym hostingu, ustaw własne adresy, CORS, SMTP
oraz klientów Google/Facebook zgodnie z [głównym README](../../../README.md).
Push na GitHub nie publikuje aplikacji ani nie migruje bazy.
