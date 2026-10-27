# AfterTab

PWA do dzielenia wspólnych wydatków: wydarzenia, zaproszenia, użytkownicy gościnni,
podział kwotowy/procentowy i rozliczenia z prywatnymi oznaczeniami przelewów.

## Projekty

- `after-tab-app/` — frontend React + Vite, instalowalna PWA.
- `after-tab-api/` — backend .NET 9, EF Core 9, SQL Server; solucja `AfterTabApi.sln`.

## Uruchomienie lokalne

Backend (wymaga SDK .NET 9 oraz SQL Server LocalDB):

```powershell
cd after-tab-api
dotnet tool restore
dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
dotnet run --project src/AfterTab.API --launch-profile https
```

W drugim terminalu frontend:

```powershell
cd after-tab-app
npm ci
npm run dev
```

PWA: `http://localhost:5173`, API: `https://localhost:7180`.
Domyślna lokalna baza: `AfterTab` na `(localdb)\MSSQLLocalDB`, logowanie Windows.

## Weryfikacja

W folderze backendu: `dotnet test AfterTabApi.sln`.
W folderze frontendu: `npm run lint` i `npm run build`.

## Konfiguracja i publikacja

- Frontend korzysta z `VITE_API_BASE_URL`; przykład w `after-tab-app/.env.example`.
- Backend korzysta z `ConnectionStrings__AfterTab` i `Cors__AllowedOrigins__0`.
- Lokalne pliki `.env`, dane uwierzytelniające, bazy i paczki wdrożeniowe nie są wersjonowane.
- Publikacja na Azure jest wykonywana wyłącznie na wyraźne zlecenie; nie ma automatycznego deployu po pushu.
- Szczegóły backendu: [README](after-tab-api/README.md).
- Konfiguracja istniejącego Azure: [AZURE-DEVELOPMENT.md](after-tab-api/docs/AZURE-DEVELOPMENT.md).
- Aktualny interfejs i płatności: [DARK-MODE-AND-PAYMENTS.md](after-tab-api/docs/DARK-MODE-AND-PAYMENTS.md).
