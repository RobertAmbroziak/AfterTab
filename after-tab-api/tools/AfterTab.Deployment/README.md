# Konfiguracja narzędzia wdrożeniowego

Polecenia poniżej uruchamiaj z głównego katalogu repozytorium AfterTab.
Narzędzie jest oddzielne od działającego API i jego ustawień w Azure App Service.

## Jednorazowa konfiguracja komputera

Connection string przechowuj w lokalnym magazynie .NET User Secrets. Nie trafia on
do Git ani do paczki publikowanego API. Magazyn jest lokalnym plikiem użytkownika,
nie jest szyfrowany i nie służy do przechowywania konfiguracji produkcyjnego procesu.

```powershell
$deploymentProject = 'after-tab-api/tools/AfterTab.Deployment/AfterTab.Deployment.csproj'
dotnet user-secrets set 'Deployment:SqlConnectionString' 'Server=tcp:<serwer>.database.windows.net,1433;Database=AfterTabDb;Encrypt=True;TrustServerCertificate=False;Connect Timeout=90;' --project $deploymentProject
```

Zastąp `<serwer>` nazwą swojego serwera Azure SQL. Na komputerze, na którym
wprowadzono tę zmianę, dotychczasowa wartość została już przeniesiona do User Secrets.
Na nowym komputerze trzeba ustawić ją ponownie. Polecenie wpisane ręcznie może
pozostać w historii terminala.

Alternatywnie ustaw `AFTERTAB_SQL_CONNECTION_STRING` w środowisku procesu.
Ta zmienna ma pierwszeństwo przed User Secrets. W przyszłym pipeline CI/CD
zapisz wartość w magazynie sekretów pipeline i przekaż ją jako tę zmienną.
Pliki `.env` nie są automatycznie wczytywane przez narzędzie.

Connection string nie może zawierać hasła, loginu, `Integrated Security` ani
ustawienia `Authentication`: narzędzie korzysta z osobno przekazanego tokenu Entra.
Wymusza bazę `AfterTabDb`, szyfrowanie i weryfikację certyfikatu.

## Kontrola połączenia przed wdrożeniem

Zalogowane konto Azure musi mieć dostęp do bazy, a adres komputera musi być
dopuszczony przez istniejące reguły sieciowe Azure SQL.

```powershell
$deploymentProject = 'after-tab-api/tools/AfterTab.Deployment/AfterTab.Deployment.csproj'
az login # tylko jeśli nie masz aktywnej sesji
if ($LASTEXITCODE -ne 0) { throw 'Logowanie do Azure nie powiodło się.' }
try {
    $env:AFTERTAB_SQL_ACCESS_TOKEN = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($env:AFTERTAB_SQL_ACCESS_TOKEN)) {
        throw 'Nie udało się pobrać tokenu Azure SQL.'
    }
    dotnet run --project $deploymentProject
    if ($LASTEXITCODE -ne 0) { throw 'Kontrola bazy nie powiodła się.' }
} finally {
    Remove-Item Env:AFTERTAB_SQL_ACCESS_TOKEN -ErrorAction SilentlyContinue
}
```

Uruchomienie bez argumentów odczytuje stan migracji i liczniki danych;
nie stosuje migracji ani nie zmienia uprawnień. Token pobieraj na czas operacji,
nie zapisuj go w Git ani na stałe w User Secrets.

## Migracje i publikacja

Dotychczasowy tryb `--bootstrap` nadal działa: po pobraniu tokenu jak wyżej ustaw
`AFTERTAB_API_PRINCIPAL_ID` na identyfikator obiektu managed identity właściwego API
i uruchom `dotnet run --project $deploymentProject -- --bootstrap`.
Ten tryb stosuje migracje i nadaje tożsamości API uprawnienia do danych;
wymaga konta z odpowiednimi uprawnieniami administracyjnymi w tej bazie.

Budowanie i publikowanie API oraz frontendu pozostaje bez zmian. Ta zmiana nie
wymaga nowych ustawień w Azure: API już odczytuje własny connection string przez
`ConnectionStrings:AfterTab` (np. zmienną `ConnectionStrings__AfterTab`).
Nie zastępuj jego konfiguracji connection stringiem narzędzia: API może mieć
własne ustawienie `Authentication` do logowania przez managed identity.

Zmiana usuwa adres serwera z bieżącego kodu, ale nie z wcześniejszych commitów.
