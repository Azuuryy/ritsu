# Ritsu local score server

For the Windows client, run the server in a separate Windows terminal. Start it from this directory with `cargo run --locked --release`. It listens on `127.0.0.1:5123` and creates `ritsu.sqlite` in the current directory. Set `RITSU_SERVER_PORT` to change the port or `RITSU_DATABASE_URL` to use a different SQLite file.

Start Ritsu with the local score server enabled. In Windows PowerShell, from the repository root:

```powershell
$env:RITSU_SCORE_SERVER_URL = 'http://localhost:5123'
dotnet run --project client/osu.Desktop/osu.Desktop.csproj
```

For a Windows client and a server running in WSL, the server must be reachable from Windows at the configured localhost port. Running both on Windows also works. An unset environment variable keeps the usual offline behaviour. The client accepts plain HTTP on the IPv4 loopback address only and rejects redirects.

Complete a new solo play, then open **Ranking → Global** for that beatmap. The server groups scores by the difficulty's MD5 and the played ruleset. It stores the score, the local profile ID and display name, and the mods and settings. It returns score rows in descending score order. The selected-mods filter matches acronyms. Repeating a submission with the same score ID does not create another row.

Scores are stored locally even when the server is unreachable. This prototype submits new plays only, with no retry queue, replay upload, authentication or server-side score verification. The HTTP interface is intended for local testing. SeaORM handles data access and schema creation for SQLite, with a PostgreSQL driver available for a future migration.

## API documentation

Open [Swagger UI](http://localhost:5123/docs) to try score submission and ranking. The [OpenAPI document](http://localhost:5123/api-docs/openapi.json) is served by the same instance. Use your configured port if it differs from 5123.
