# CinePlex – skrypty bazodanowe

## Wymagania

```
pip install -r requirements.txt
```

Wymagany ODBC Driver 17 for SQL Server oraz uruchomiona instancja `(localdb)\mssqllocaldb` z bazą `CinePlexDb`.

## seed_movies.py

Pobiera metadane filmów z TMDB API i importuje je do tabeli `Movies` w CinePlexDb.

Zawiera:
- 61 filmów hardcodowanych z repertuaru (czerwiec–sierpień 2026) oraz klasyki z serii Straszny film

### Uruchomienie

```
py seed_movies.py --api-key TWOJ_KLUCZ_TMDB
```

Klucz TMDB: https://www.themoviedb.org/settings/api
