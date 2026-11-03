# Logowanie backendu

API używa Seriloga przez standardowy `ILogger<T>`. Domyślnie zapisuje `Warning`,
`Error` i `Fatal` do konsoli oraz `logs/api-YYYYMMDD.log` względem katalogu
roboczego procesu. Zmiana hostingu nie wymaga zmiany kodu.

Pliki są rotowane codziennie i po przekroczeniu 10 MiB. Zachowywane jest
14 ostatnich plików, a nie gwarantowane 14 dni historii. Katalog `logs/`
i pliki `.log` są ignorowane przez Git.

## Konfiguracja środowiska

Ustawienia serwera podaje się poza repozytorium jako zmienne środowiskowe
(w Azure App Service: Environment variables / App settings):

| Zmienna | Znaczenie |
| --- | --- |
| `Serilog__WriteTo__1__Args__path` | Pełna ścieżka do pliku, zakończona np. `api-.log`; indeks 1 oznacza sink File. |
| `Serilog__MinimumLevel__Default` | Domyślnie `Warning`; `Error` pozostawia błędy i awarie krytyczne. |
| `Serilog__WriteTo__1__Args__fileSizeLimitBytes` | Limit rozmiaru pojedynczego pliku. |
| `Serilog__WriteTo__1__Args__retainedFileCountLimit` | Liczba zachowywanych plików. |

Nie dodawaj do Git konfiguracji konkretnej instancji ani jej logów.
Katalog docelowy musi być zapisywalny i trwały. W kontenerze użyj wolumenu;
przy wielu instancjach najlepiej używać osobnych plików lub centralnego odbiornika.

W Azure App Service włącz logowanie aplikacji do systemu plików i otwórz
**Log stream**. Dla aplikacji Linux docelowy plik ustaw w katalogu logów
App Service, zgodnie z konfiguracją hostingu. Zmianę zmiennych zastosuj
przez restart aplikacji. Konsola i plik zawierają te same zdarzenia, więc
strumień Azure może prezentować je dwoma kanałami.

## Wyjątki

`AppExceptionHandler` obsługuje wyjątki z potoku HTTP. Nieoczekiwany wyjątek
zapisuje raz na poziomie Error: obiekt wyjątku ze stack trace, metodę HTTP,
szablon trasy i TraceId. Klient otrzymuje ogólny ProblemDetails HTTP 500
oraz `traceId`, bez szczegółów wyjątku. Dotychczasowe błędy biznesowe
400/403/404/409 zachowują komunikaty i nie generują wpisu Error w handlerze.
Inne komponenty (np. EF Core) mogą niezależnie logować swoje awarie.

W .NET 9 ExceptionHandlerMiddleware loguje także obsłużone wyjątki.
Dlatego jego kategoria ma próg Fatal w konfiguracji; zapis błędów należy
do naszego handlera. Nie usuwaj tego nadpisania bez sprawdzenia duplikatów
i logowania oczekiwanych błędów biznesowych.

Nie dodawaj try/catch do każdej metody. Własny catch jest potrzebny do
lokalnej obsługi błędu; jeśli wyjątek nie dociera dalej, zaloguj go tam
przez `logger.LogError(exception, "Opis operacji")`. Zadania w tle wymagają
obsługi na własnej granicy — handler HTTP ich nie obejmuje. Awarie startu
są zapisywane przez bootstrap logger do konsoli, a po konfiguracji również
przez skonfigurowane sinki; logger jest opróżniany przy zamknięciu procesu.

Nie zapisujemy treści requestów, query stringów, nagłówków ani wartości
parametrów trasy w naszym handlerze. Nie włączaj EF SensitiveDataLogging
w produkcji. Sam komunikat wyjątku lub log biblioteki może zawierać dane
wrażliwe — nie umieszczaj sekretów w wyjątkach i ogranicz dostęp do logów.

Logi plikowe służą do diagnostyki z ograniczoną historią. Nie zastępują
monitorowania dostępności ani alertów.
