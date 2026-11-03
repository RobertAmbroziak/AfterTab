# Opcjonalne wdrożenie na mikr.us / VPS

[English](README.en.md)

To jest osobna ścieżka wdrożenia. Samo dodanie tych plików ani push na GitHub
nie uruchamia kontenerów, nie publikuje Azure Static Web Apps i nie zmienia
istniejącej bazy SQL Server. Dotychczasowy build/publish Azure działa jak wcześniej;
domyślny provider API nadal to `SqlServer`. PostgreSQL wybiera wyłącznie ten Compose.

## Układ

- `frontend`: zbudowane pliki PWA w Nginx; `/api/` i callbacki OAuth trafiają do API.
- `api`: ASP.NET Core, PostgreSQL i nieadministracyjne konto `aftertab_app`.
- `postgres`: PostgreSQL 17, bez opublikowanego portu bazy.
- `migrate`: jednorazowy pomocnik stosujący migracje jako `aftertab_owner` przed API.

Frontend używa względnego `/api`, więc jego obraz nie zawiera adresu Azure ani
docelowej domeny. PostgreSQL i API są dostępne tylko w sieci Compose. Nginx ma
wewnętrzny port HTTP 8080 i domyślnie wystawia go wyłącznie na `127.0.0.1:8080` VPS.

Wymagane są Docker Engine z kontenerami Linux, Docker Compose v2 oraz **publiczna
domena z HTTPS**. HTTPS obsługuje reverse proxy hosta lub usługa proxy mikr.us,
przed frontendem Compose. PWA i bezpieczne ciasteczka wymagają HTTPS. Ten zestaw
nie instaluje certyfikatu ani nie zamawia domeny. Nie wymaga Kubernetes ani Azure.

## Konfiguracja i pierwszy start

Polecenia dla powłoki Linux, z katalogu `deploy/mikrus`:

```sh
cp .env.example .env
chmod 600 .env
openssl rand -hex 32
openssl rand -hex 32
```

Wpisz dwa różne wygenerowane hasła do `DB_OWNER_PASSWORD` i `DB_APP_PASSWORD`.
Ustaw `PUBLIC_ORIGIN` na własny adres HTTPS bez końcowego `/`, np.
`https://aftertab.example.com`. Wartości produkcyjne pozostają w ignorowanym `.env`;
nie są kopiowane do obrazów. Hasła hex nie zawierają separatorów connection stringa
ani znaków interpolacji Compose. `.env` jest odczytywany przez Compose, nie przez API.

Jeśli sieć `172.30.37.0/24` jest zajęta, zmień spójnie `DOCKER_SUBNET`,
`DOCKER_GATEWAY`, `FRONTEND_IP` i `TRUSTED_PROXY_CIDR`. API ufa nagłówkom
forwarded wyłącznie od `FRONTEND_IP`; ta obsługa jest włączona tylko w Compose.

```sh
docker compose config --quiet
docker compose up -d --build
docker compose ps -a
curl -f http://127.0.0.1:8080/api/health
```

Compose czeka na gotowość PostgreSQL, kończy migracje i uruchamia API oraz frontend.
`migrate` z kodem wyjścia 0 to prawidłowy stan. Błąd migracji blokuje start API.
Endpoint health potwierdza działanie procesu; nie sprawdza bazy. Przetestuj też
utworzenie gościa i wydarzenia przez publiczny HTTPS.

Budowanie obrazów wymaga więcej RAM niż ich uruchamianie. Na małym VPS można
budować na własnym komputerze/CI i dostarczać obrazy zamiast kompilować na serwerze.
Dobierz plan mikr.us obsługujący Docker i zasoby na API oraz bazę.

## Domena, HTTPS i adres klienta

Przy proxy Nginx na VPS przykład jest w `host-nginx.example.conf`. Wymaga własnego
certyfikatu i dostępnych portów 80/443. Domyślne `TRUSTED_PROXY_CIDR` odpowiada
bramie sieci Docker, przez którą proxy hosta łączy się z frontendem.

Przy zewnętrznym proxy mikr.us dostosuj `HTTP_BIND` i `HTTP_PORT` do udostępnionego
portu oraz sposobu połączenia. Np. `HTTP_BIND=0.0.0.0` udostępnia port IPv4;
`HTTP_BIND=[::]` wymaga działającego mapowania IPv6 w Dockerze. Ogranicz dostęp
do tego portu do swojego proxy. Ustaw `TRUSTED_PROXY_CIDR` na rzeczywisty adres/CIDR
proxy, a nie `0.0.0.0/0`. Proxy musi zachować publiczny `Host` i ustawiać
`X-Forwarded-For` z adresem użytkownika. Bez poprawnego zaufanego proxy limity
żądań mogą traktować wszystkich użytkowników jako jeden adres.

Frontend przekazuje do API schemat `https`, ponieważ jest prywatnym hopem za
terminatorem TLS. Nie wystawiaj tego wejścia jako publicznej strony HTTP.
`PUBLIC_ORIGIN` ustawia dozwolone źródło oraz adres linków logowania.

Opcjonalnie ustaw SMTP (`MAIL_*`) dla rejestracji/resetu hasła oraz dane
Google/Facebook. Callbacki to `PUBLIC_ORIGIN/signin-google` i
`PUBLIC_ORIGIN/signin-facebook`. Ustaw je osobno w panelach dostawców. Domyślny
obraz nie publikuje APK ani powiązania Android Digital Asset Links dla nowej domeny.

## Dane, aktualizacje i logi

Nazwane wolumeny `postgres-data`, `api-logs` i `api-keys` przechowują bazę, logi
Serilog i klucze Data Protection. Przetrwają restart, wymianę obrazu oraz zwykłe
`docker compose down`. **`docker compose down -v` usuwa dane i klucze.**
Zachowaj nazwę projektu Compose (`aftertab`), żeby używać tych samych wolumenów.
Wolumeny nie są backupem — utrata VPS może oznaczać utratę danych.

Przed aktualizacją wykonaj backup. Po pobraniu zmian:

```sh
docker compose build
docker compose stop frontend api
docker compose run --rm migrate
# Kontynuuj tylko po udanej migracji:
docker compose up -d
docker compose logs --tail=100 api migrate
```

Hasła i skrypt tworzenia kont działają przy **pierwszej inicjalizacji** wolumenu.
Sama zmiana `.env` nie zmieni hasła istniejącej roli PostgreSQL. Zmieniaj je przez
`ALTER ROLE`, następnie zaktualizuj `.env` i odtwórz kontenery używające hasła.
Nie zmieniaj automatycznie głównej wersji obrazu PostgreSQL; upgrade wymaga
zaplanowanej migracji/odtworzenia backupu.

Logi: `docker compose logs -f api` i `docker compose logs migrate`. Logi Dockera
mają limit 3 plików po 10 MB na usługę; pliki Serilog mają własną rotację.
Klucze logowania pozostają w `api-keys`, więc wymiana kontenera ich nie resetuje.

Przykład backupu na Linuxie (zawiera dane użytkowników; katalog jest ignorowany):

```sh
mkdir -p backups
chmod 700 backups
docker compose exec -T postgres pg_dump -U aftertab_owner -d aftertab -Fc > backups/aftertab.dump
```

Sprawdź kod wyjścia polecenia i możliwość odtworzenia backupu; przechowuj kopię
również poza VPS. Odtworzenie wykonuj w pustej bazie zgodnego schematu, przed
uruchomieniem API. Klucze `api-keys` zabezpieczaj osobno, jeżeli chcesz zachować
ciągłość zaszyfrowanych ciasteczek. Import istniejących danych z Azure SQL do
PostgreSQL pozostaje osobną operacją — Compose go nie wykonuje.
