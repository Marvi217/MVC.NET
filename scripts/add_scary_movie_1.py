import requests, json, pyodbc, argparse, time

CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=(localdb)\\mssqllocaldb;"
    "DATABASE=CinePlexDb;"
    "Trusted_Connection=yes;"
)
TMDB_BASE   = "https://api.themoviedb.org/3"
POSTER_BASE = "https://image.tmdb.org/t/p/w500"

def tmdb_get(session, url, params):
    r = session.get(url, params=params, timeout=15)
    r.raise_for_status()
    return r.json()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-key", required=True)
    key = parser.parse_args().api_key

    s = requests.Session()
    s.headers["Accept"] = "application/json"

    results = tmdb_get(s, f"{TMDB_BASE}/search/movie",
                       {"api_key": key, "query": "Scary Movie", "year": 2000})["results"]
    if not results:
        print("Nie znaleziono w TMDB."); return
    r = results[0]
    tmdb_id = r["id"]
    print(f"TMDB: [{tmdb_id}] {r['title']} ({r.get('release_date','?')[:4]})")

    details = tmdb_get(s, f"{TMDB_BASE}/movie/{tmdb_id}",
                       {"api_key": key, "language": "pl-PL"})
    time.sleep(0.1)

    credits = tmdb_get(s, f"{TMDB_BASE}/movie/{tmdb_id}/credits", {"api_key": key})
    director = next((c for c in credits.get("crew", []) if c.get("job") == "Director"), None)
    cast = [
        {"name": c["name"], "character": c.get("character",""),
         "profileUrl": ("https://image.tmdb.org/t/p/w185" + c["profile_path"]) if c.get("profile_path") else None}
        for c in credits.get("cast", [])[:8]
    ]
    time.sleep(0.1)

    videos = tmdb_get(s, f"{TMDB_BASE}/movie/{tmdb_id}/videos",
                      {"api_key": key, "language": "en-US"})["results"]
    trailer = next(
        (f"https://www.youtube.com/watch?v={v['key']}"
         for v in videos if v.get("site") == "YouTube" and v.get("type") in ("Trailer", "Teaser")),
        None
    )

    title       = "Straszny film"
    description = (details.get("overview") or "Film: Straszny film")[:2000]
    duration    = details.get("runtime") or 88
    release     = details.get("release_date") or "2000-07-07"
    poster      = (POSTER_BASE + details["poster_path"]) if details.get("poster_path") else None
    genre       = 2
    age_rating  = 2

    if director:
        parts = director["name"].split(" ", 1)
        dfirst, dlast = parts[0], (parts[1] if len(parts) > 1 else "")
    else:
        dfirst, dlast = "Keenen Ivory", "Wayans"

    conn = pyodbc.connect(CONN_STR)
    conn.autocommit = False
    cur = conn.cursor()

    cur.execute("SELECT MovieId FROM Movies WHERE TmdbId=?", tmdb_id)
    if cur.fetchone():
        print("Film już istnieje w bazie."); conn.close(); return

    cur.execute("SELECT DirectorId FROM Directors WHERE FirstName=? AND LastName=?", dfirst, dlast)
    row = cur.fetchone()
    if row:
        director_id = row[0]
    else:
        cur.execute(
            "INSERT INTO Directors (FirstName,LastName,Nationality,Biography) "
            "OUTPUT INSERTED.DirectorId VALUES (?,?,?,?)",
            dfirst, dlast, "American", f"{dfirst} {dlast}"
        )
        director_id = cur.fetchone()[0]

    cur.execute(
        "INSERT INTO Movies "
        "(Title,Description,Duration,ReleaseDate,Genre,AgeRating,"
        " PosterUrl,TrailerUrl,CastJson,TmdbId,DirectorId) "
        "OUTPUT INSERTED.MovieId VALUES (?,?,?,?,?,?,?,?,?,?,?)",
        title, description, duration, release,
        genre, age_rating, poster, trailer,
        json.dumps(cast, ensure_ascii=False), tmdb_id, director_id
    )
    movie_id = cur.fetchone()[0]
    conn.commit()
    conn.close()
    print(f"Dodano: [{movie_id}] {title} (reżyser: {dfirst} {dlast})")

if __name__ == "__main__":
    main()
