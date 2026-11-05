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
