# Poker Server

Multiplayer Poker & Blackjack Game Server built with ASP.NET Core and SignalR.

## 🌐 Live Server
**Server URL:** https://poker-server-vi5l.onrender.com

**Connect with:** `https://poker-server-vi5l.onrender.com/gamehub`

## Features
- Texas Hold'em Poker
- Blackjack
- Real-time multiplayer
- Room system

## How to Play
### In a browser
Open the deployed server's main address. Enter a name, create or join a table,
and share the invitation link with friends. Poker requires two players; blackjack
can be played alone. Chips are for play only. Refreshing disconnects your seat;
rejoin from the lobby. Guest balances are not saved.

The web client is served by the same ASP.NET application, with no separate frontend
build or hosting. After committing and pushing the changes to the branch connected
to Render, deploy the latest commit in Render (or let automatic deployment run).
The existing Render URL then serves the game. Health checks use `/health`.

The server embeds the page, CSS and JavaScript in its assembly and delivers them
as one HTML response, so the game does not depend on separate asset requests or
the process working directory. SignalR 8.0.29 is bundled locally under `wwwroot/vendor`
(MIT license alongside it); the browser can fall back to HTTP Long Polling when
WebSockets are unavailable. Connection failures show a retry button.

### Browser regression checks
Start the server, then run `npm ci` and `npx playwright install chromium` once.
Run `npm run test:browser`; the default target is `http://127.0.0.1:8080`.
Set `POKER_TEST_URL` to use another running test server and `CHROMIUM_PATH` to
use an existing Chromium installation. These checks create temporary guest tables:
use a development instance. They cover blocked asset requests, two-player poker,
invitations, chat, blackjack, mobile layout, failed connection/retry, and HTTP fallback.
Node.js is needed only for these checks, not for building or hosting the application.

### Desktop client
1. Download the client (PokerClient.zip)
2. Run poker.exe
3. Login or play as Guest
4. Enter server address: `https://poker-server-vi5l.onrender.com/gamehub`
5. Click CONNECT
6. Create or join a room
7. Play!

## Environment Variables
- `ASPNETCORE_URLS`: http://+:8080
