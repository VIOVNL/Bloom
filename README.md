# Bloom

A radial petal launcher for Windows. A floating button on your desktop opens a circle of petals — apps, folders, commands, system actions, and keyboard shortcuts — fanning out around the click point.

Website: [bloom.viov.nl](https://bloom.viov.nl)

## Features

- **5 item types** — Software, Folders, Commands, System Actions (28 built-in), Keyboard Shortcuts
- **1000+ Lucide icons**, plus auto-extracted icons from `.exe` files and custom PNGs
- **52-color palette** plus any custom hex
- **Smart layout** — single ring up to 5 items, multi-ring above; edge-aware on screen boundaries
- **Animations** — staggered bloom open, mouse-repel, hover scale
- **Auto-update** via Velopack
- **Start with Windows**, draggable bloom button with position persistence
- Dark / Light themes

See [`BloomFront/info.md`](BloomFront/info.md) for the full feature list.

## Repository layout

| Folder | Contents |
| --- | --- |
| `Bloom/` | Avalonia UI desktop app (.NET 10, C#) |
| `BloomFront/` | Marketing site + admin / submissions API (Vue 3 + Vite, PHP) |
| `.github/workflows/` | Release pipelines (frontend, backend, full) |

## Building the desktop app

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build Bloom/Bloom.csproj -c Release
dotnet run --project Bloom/Bloom.csproj
```

To produce a Velopack-packaged installer, install the CLI and run:

```bash
dotnet tool install -g vpk
dotnet publish Bloom/Bloom.csproj -c Release -r win-x64 --no-self-contained -o publish
vpk pack --packId Bloom --packVersion 1.0.0 --packDir publish --mainExe Bloom.exe --framework net10.0-x64-desktop --icon Bloom/Assets/bloom.ico
```

(The full release flow is in [`.github/workflows/release.yml`](.github/workflows/release.yml).)

## Building the frontend

Requires Node 22+.

```bash
cd BloomFront
npm install
npm run dev      # local dev server
npm run build    # production build into dist/
```

## Running the API

The PHP API in `BloomFront/api/` powers the bug/feature submission form and the admin dashboard. It uses SQLite (auto-created in `BloomFront/data/`).

**Required environment variable:**

```bash
BLOOM_ADMIN_PASSWORD=<a strong password>
```

The API exits with a 500 if this is not set. Pick something long and random — it gates the admin panel.

The `BloomFront/api/` directory must be served by PHP 8.0+ with the GD extension (used to strip metadata from uploaded images). The included `.htaccess` rewrites and the auto-generated `data/.htaccess` and `uploads/.htaccess` files block direct access to user data.

## Releasing

Three GitHub Actions workflows handle releases:

- **Release: Frontend** — builds `BloomFront/` and uploads to FTP
- **Release: Backend** — bumps version, builds the .NET app, packages with Velopack, uploads installer + delta updates to FTP
- **Release: Full** — runs both end-to-end

All require these GitHub Action secrets:

- `FTP_SERVER`
- `FTP_USERNAME`
- `FTP_PASSWORD`

## License

[MIT](LICENSE)
