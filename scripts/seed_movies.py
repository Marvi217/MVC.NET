import requests, json, pyodbc, argparse, time

CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=(localdb)\\mssqllocaldb;"
    "DATABASE=CinePlexDb;"
    "Trusted_Connection=yes;"
)
TMDB_BASE   = "https://api.themoviedb.org/3"
POSTER_BASE = "https://image.tmdb.org/t/p/w500"
DATE_FROM   = "2026-05-01"
DATE_TO     = "2026-08-31"

GENRE_MAP = {
    28: 0, 35: 1, 27: 2, 878: 3, 18: 4,  53: 5,
    16: 6, 99: 7, 10749: 8, 12: 9, 10751: 6, 14: 9,
    80: 5, 36: 4, 10402: 1,  9648: 5, 10752: 0, 37: 9,
}
AGE_MAP = {"G": 0, "PG": 1, "PG-13": 2, "NR": 0, "UR": 0, "NC-17": 4, "R": 3, "": 0}

HARDCODED = [
    ("Straszny film 6",                          "Scary Movie 6",                       2026, "2026-06-07",  2, 2, 1273221),
    ("Władcy wszechświata",                      "Masters of the Universe",              2026, "2026-06-07",  0, 1, None),
    ("Sabotażysta 5",                            "Sabotażysta 5",                        2026, "2026-06-07",  5, 2, None),
    ("The Amazing Digital Circus: The Last Act", "The Amazing Digital Circus Last Act",  2026, "2026-06-07",  6, 0, None),
    ("Podziemny krąg - Powrót do kin",           "Fight Club",                           1999, "2026-06-07",  5, 3, None),
    ("Kosmiczny mecz",                           "Space Jam",                            1996, "2026-06-07",  1, 0, None),
    ("Diabeł ubiera się u Prady 2",              "The Devil Wears Prada 2",              2026, "2026-06-07",  1, 1, None),
    ("Drama",                                    "Drama",                                2026, "2026-06-07",  4, 2, None),
    ("Drzewo magii",                             "The Magic Tree",                       2026, "2026-06-07",  9, 0, None),
    ("Erupcja",                                  "Eruption",                             2026, "2026-06-07",  0, 2, None),
    ("Gwiezdne wojny: Mandalorian i Grogu",      "The Mandalorian and Grogu",            2026, "2026-06-07",  3, 1, None),
    ("La Traviata Verdiego z Arena di Verona",   "La Traviata",                          2026, "2026-06-07",  7, 1, None),
    ("Michael",                                  "Michael",                              2026, "2026-06-07",  4, 2, None),
    ("Normal",                                   "Normal",                               2026, "2026-06-07",  4, 2, None),
    ("Obsesja",                                  "Obsession",                            2026, "2026-06-07",  5, 3, None),
    ("Ojczyzna",                                 "Heimat",                               2026, "2026-06-07",  4, 2, None),
    ("Pasażer",                                  "The Passenger",                        2026, "2026-06-07",  5, 2, None),
    ("Projekt Hail Mary",                        "Project Hail Mary",                    2026, "2026-06-07",  3, 1, None),
    ("Werdykt",                                  "The Verdict",                          2026, "2026-06-07",  5, 2, None),
    ("Toy Story 5",                              "Toy Story 5",                          2026, "2026-06-18",  6, 0, None),
    ("Backrooms. Bez wyjścia",                   "Backrooms",                            2026, "2026-06-18",  2, 3, None),
    ("Straszny film",                            "Scary Movie",                          2000, "2000-07-07",  2, 2, None),
    ("Straszny film 2",                          "Scary Movie 2",                        2001, "2001-07-04",  2, 2, None),
    ("Straszny film 3",                          "Scary Movie 3",                        2003, "2003-10-24",  2, 2, None),
    ("Straszny film 4",                          "Scary Movie 4",                        2006, "2006-04-14",  2, 2, None),
    ("Straszny film 5",                          "Scary Movie 5",                        2013, "2013-04-12",  2, 2, None),
    ("Minionki i straszydła",                    "Minions Monsters",                     2026, "2026-07-01",  6, 0, None),
    ("Zaproszenie",                              "The Invite",                           2026, "2026-07-03",  5, 2, None),
    ("Wypadek fortepianowy",                     "L'accident de piano",                  2025, "2026-07-03",  4, 2, None),
    ("Wędrówka na północ",                       "The North",                            2025, "2026-07-03",  9, 1, None),
    ("Vaiana",                                   "Moana",                                2026, "2026-07-10",  6, 0, None),
    ("Martwe zło: Ogień",                        "Evil Dead Burn",                       2026, "2026-07-10",  2, 3, None),
    ("Wielki Łuk",                               "L'inconnu de la Grande Arche",         2025, "2026-07-10",  4, 2, None),
    ("Mistrzynice",                              "Sieger sein",                          2024, "2026-07-10",  4, 1, None),
    ("Nowy pamiętnik Pauliny P.",                "Drugi dnevnik Pauline P.",             2025, "2026-07-10",  4, 2, None),
    ("Przekleństwa niewinności",                 "The Virgin Suicides",                  1999, "2026-07-10",  4, 3, None),
    ("Odyseja",                                  "The Odyssey",                          2026, "2026-07-17",  9, 2, None),
    ("Cut Off",                                  "Cut Off",                              2026, "2026-07-17",  5, 3, None),
    ("Ostatni konsjerż",                         "The Souffleur",                        2025, "2026-07-17",  5, 2, None),
    ("Hokum",                                    "Hokum",                                2026, "2026-07-24",  2, 2, None),
    ("Jej piekło",                               "Her Private Hell",                     2026, "2026-07-24",  5, 3, None),
    ("Dziecko nocy",                             "Yön lapsi",                            2026, "2026-07-24",  4, 2, None),
    ("Wyschnięci",                               "Arnie Barney",                         2026, "2026-07-24",  1, 1, None),
    ("Ekipa zwierzaków",                         "Spiked",                               2025, "2026-07-24",  1, 1, None),
    ("Spider-Man: Całkiem nowy dzień",           "Spider-Man Brand New Day",             2026, "2026-07-31",  0, 1, None),
    ("Pompei: Below the Clouds",                 "Pompei Below the Clouds",              2025, "2026-07-31",  4, 2, None),
    ("Młody Waszyngton",                         "Young Washington",                     2026, "2026-07-31",  4, 1, None),
    ("Ice Cream Man",                            "Ice Cream Man",                        2026, "2026-08-07",  2, 3, None),
    ("Psi Patrol i dinozaury",                   "PAW Patrol Dino Movie",                2026, "2026-08-07",  6, 0, None),
    ("Super Troopers 3",                         "Super Troopers 3",                     2026, "2026-08-07",  1, 2, None),
    ("Koniec ulicy Dębowej",                     "The End of Oak Street",                2026, "2026-08-14",  5, 2, None),
    ("Tylko jedna noc",                          "One Night Only",                       2026, "2026-08-14",  8, 2, None),
    ("Superfutrzak i złośliwa wiewiórka",        "Supermarsu suuri huijaus",             2025, "2026-08-14",  6, 0, None),
    ("Nowa fala",                                "Nouvelle vague",                       2025, "2026-08-21",  4, 2, None),
    ("Buntownik",                                "Mutiny",                               2026, "2026-08-21",  0, 2, None),
    ("Naznaczony: Wyjście z mrocznego wymiaru",  "Insidious Out of the Further",         2026, "2026-08-21",  2, 3, None),
    ("Księga pustyni",                           "L'enfant du desert",                   2026, "2026-08-21",  9, 1, None),
    ("Gwiazdozbiór Psa",                         "The Dog Stars",                        2026, "2026-08-28",  3, 2, None),
    ("Historie równoległe",                      "Histoires paralleles",                 2026, "2026-08-28",  4, 2, None),
    ("Gorzkie święta",                           "Amarga Navidad",                       2026, "2026-08-28",  5, 2, None),
    ("Cliffhanger",                              "Cliffhanger",                          2026, "2026-08-28",  0, 2, None),
]


def tmdb_get(session, url, params):
    r = session.get(url, params=params, timeout=15)
    r.raise_for_status()
    return r.json()


def map_genre(genre_ids):
    for gid in (genre_ids or []):
        if gid in GENRE_MAP:
            return GENRE_MAP[gid]
    return 0


def get_certification(session, api_key, tmdb_id):
    try:
        data = tmdb_get(session, f"{TMDB_BASE}/movie/{tmdb_id}/release_dates",
                        {"api_key": api_key})
        for entry in data.get("results", []):
            if entry["iso_3166_1"] in ("US", "GB", "PL"):
                for rd in entry.get("release_dates", []):
                    cert = rd.get("certification", "")
                    if cert:
                        return cert
    except Exception:
        pass
    return ""


def get_trailer(session, api_key, tmdb_id):
    try:
        data = tmdb_get(session, f"{TMDB_BASE}/movie/{tmdb_id}/videos",
                        {"api_key": api_key, "language": "en-US"})
        for v in data.get("results", []):
            if v.get("site") == "YouTube" and v.get("type") in ("Trailer", "Teaser"):
                return f"https://www.youtube.com/watch?v={v['key']}"
    except Exception:
        pass
    return None


def get_credits(session, api_key, tmdb_id):
    try:
        data = tmdb_get(session, f"{TMDB_BASE}/movie/{tmdb_id}/credits",
                        {"api_key": api_key})
        crew = data.get("crew", [])
        director = next((c for c in crew if c.get("job") == "Director"), None)
        cast = [
            {
                "name": c["name"],
                "character": c.get("character", ""),
                "profileUrl": ("https://image.tmdb.org/t/p/w185" + c["profile_path"])
                              if c.get("profile_path") else None,
            }
            for c in data.get("cast", [])[:8]
        ]
        return director, cast
    except Exception:
        return None, []


def search_tmdb(session, api_key, query, year):
    for lang in ("pl-PL", "en-US"):
        try:
            data = tmdb_get(session, f"{TMDB_BASE}/search/movie", {
                "api_key": api_key, "query": query, "year": year, "language": lang,
            })
            if data.get("results"):
                return data["results"][0]
        except Exception:
            pass
        time.sleep(0.1)
    return None



def build_entry(session, api_key, tmdb_result, pl_title, fallback_release,
                genre_override, rating_override):
    tmdb_id = tmdb_result.get("id")
    details = {}
    director_data = None
    cast = []
    trailer = None
    cert = ""

    if tmdb_id:
        try:
            details = tmdb_get(session, f"{TMDB_BASE}/movie/{tmdb_id}",
                               {"api_key": api_key, "language": "pl-PL"})
            time.sleep(0.1)
        except Exception:
            pass
        director_data, cast = get_credits(session, api_key, tmdb_id)
        trailer = get_trailer(session, api_key, tmdb_id)
        if rating_override is None:
            cert = get_certification(session, api_key, tmdb_id)
        time.sleep(0.1)

    title = pl_title or details.get("title") or tmdb_result.get("title", "Unknown")
    overview = details.get("overview") or tmdb_result.get("overview") or ""
    poster_path = details.get("poster_path") or tmdb_result.get("poster_path")
    poster = (POSTER_BASE + poster_path) if poster_path else None
    release = details.get("release_date") or tmdb_result.get("release_date") or fallback_release
    duration = details.get("runtime") or 100
    genre = genre_override if genre_override is not None else \
            map_genre(tmdb_result.get("genre_ids") or
                      [g["id"] for g in details.get("genres", [])])
    rating = rating_override if rating_override is not None else AGE_MAP.get(cert, 0)

    if director_data:
        parts = director_data["name"].split(" ", 1)
        dfirst, dlast = parts[0], (parts[1] if len(parts) > 1 else "")
    else:
        dfirst, dlast = "Nieznany", "Reżyser"

    return {
        "tmdb_id": tmdb_id,
        "title": title,
        "description": (overview or f"Film: {title}")[:2000],
        "duration": duration,
        "release_date": release,
        "genre": genre,
        "age_rating": rating,
        "poster_url": poster,
        "trailer_url": trailer,
        "cast_json": json.dumps(cast, ensure_ascii=False),
        "director_first": dfirst,
        "director_last": dlast,
    }


def get_or_create_director(cur, first, last):
    cur.execute("SELECT DirectorId FROM Directors WHERE FirstName=? AND LastName=?",
                first, last)
    row = cur.fetchone()
    if row:
        return row[0]
    cur.execute(
        "INSERT INTO Directors (FirstName,LastName,Nationality,Biography) "
        "OUTPUT INSERTED.DirectorId VALUES (?,?,?,?)",
        first, last, "Unknown", f"{first} {last}",
    )
    return cur.fetchone()[0]


def import_movie(cur, m, director_id):
    if m.get("tmdb_id"):
        cur.execute("SELECT MovieId FROM Movies WHERE TmdbId=?", m["tmdb_id"])
        if cur.fetchone():
            return None
    cur.execute("SELECT MovieId FROM Movies WHERE Title=?", m["title"])
    if cur.fetchone():
        return None
    cur.execute(
        "INSERT INTO Movies "
        "(Title,Description,Duration,ReleaseDate,Genre,AgeRating,"
        " PosterUrl,TrailerUrl,CastJson,TmdbId,DirectorId) "
        "OUTPUT INSERTED.MovieId VALUES (?,?,?,?,?,?,?,?,?,?,?)",
        m["title"], m["description"], m["duration"], m["release_date"],
        m["genre"], m["age_rating"], m.get("poster_url"), m.get("trailer_url"),
        m.get("cast_json"), m.get("tmdb_id"), director_id,
    )
    return cur.fetchone()[0]


def main():
    parser = argparse.ArgumentParser(
        description="Import CinePlex movies (May-Aug 2026) to CinePlexDb"
    )
    parser.add_argument("--api-key", required=True, help="TMDB API key")
    args = parser.parse_args()

    session = requests.Session()
    session.headers["Accept"] = "application/json"

    conn = pyodbc.connect(CONN_STR)
    conn.autocommit = False
    cur = conn.cursor()

    seen_tmdb_ids = set()
    queue = []

    print(f"Resolving {len(HARDCODED)} hardcoded movies via TMDB...")
    for pl_title, en_search, tmdb_year, release_date, genre, rating, tmdb_id_override in HARDCODED:
        result = None
        if tmdb_id_override:
            try:
                result = tmdb_get(session, f"{TMDB_BASE}/movie/{tmdb_id_override}",
                                  {"api_key": args.api_key, "language": "pl-PL"})
                time.sleep(0.1)
            except Exception:
                pass
        if not result:
            result = search_tmdb(session, args.api_key, en_search, tmdb_year)
            time.sleep(0.2)

        if result:
            tid = result.get("id")
            if tid and tid in seen_tmdb_ids:
                print(f"  DUP   {pl_title} (tmdb_id={tid})")
                continue
            if tid:
                seen_tmdb_ids.add(tid)
            queue.append((result, pl_title, release_date, genre, rating))
            print(f"  FOUND [{tid}] {pl_title}")
        else:
            print(f"  NOT FOUND  {pl_title}  (searched: \"{en_search}\" {tmdb_year})")
            queue.append((
                {"id": None, "title": pl_title, "poster_path": None,
                 "overview": "", "release_date": release_date, "genre_ids": []},
                pl_title, release_date, genre, rating,
            ))

    print(f"\nImporting {len(queue)} movies to CinePlexDb...")
    imported = skipped = errors = 0

    for tmdb_result, pl_title, fallback_release, genre_override, rating_override in queue:
        title_display = pl_title or tmdb_result.get("title", "?")
        try:
            m = build_entry(session, args.api_key, tmdb_result,
                            pl_title, fallback_release, genre_override, rating_override)
            did = get_or_create_director(cur, m["director_first"], m["director_last"])
            mid = import_movie(cur, m, did)
            if mid:
                imported += 1
                print(f"  IMPORTED  [{mid}] {m['title']}")
            else:
                skipped += 1
                print(f"  SKIP      {m['title']} (already exists)")
        except Exception as e:
            errors += 1
            print(f"  ERROR     {title_display}: {e}")
            try:
                conn.rollback()
            except Exception:
                pass

    conn.commit()
    conn.close()
    print(f"\nDone. Imported: {imported}, Skipped: {skipped}, Errors: {errors}")


if __name__ == "__main__":
    main()
