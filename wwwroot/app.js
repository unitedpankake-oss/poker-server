'use strict';
const $ = id => document.getElementById(id);
let socket, connectionId, roomId, state, sequence = 0, connected = false, busy = false, roomMinBet = 10;
const pending = new Map();
const phases = ['Ожидаем игроков', 'Время ставок', 'Раздача', 'Ход игрока', 'Ход дилера', 'Вскрытие', 'Раунд завершён'];
function error(e) { $('error').textContent = e.message || String(e); }
function node(tag, text, cls) { const el = document.createElement(tag); if(text !== undefined) el.textContent = text; if(cls) el.className = cls; return el; }
function invoke(target, ...args) {
  if (!connected) return Promise.reject(new Error('Нет подключения. Обновите страницу, чтобы подключиться заново.'));
  const invocationId = String(++sequence);
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => { pending.delete(invocationId); reject(new Error('Сервер не ответил вовремя')); }, 15000);
    pending.set(invocationId, {resolve, reject, timer});
    socket.send(JSON.stringify({type:1, invocationId, target, arguments:args}) + '\x1e');
  });
}
async function run(action) { try { $('error').textContent=''; await action(); } catch(e) { error(e); } }
async function connect() {
  const response = await fetch('/gamehub/negotiate?negotiateVersion=1', {method:'POST'});
  if(!response.ok) throw new Error('Сервер недоступен');
  const info = await response.json(); connectionId = info.connectionId;
  await new Promise((resolve, reject) => {
    const url = new URL('/gamehub', location.href); url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:'; url.searchParams.set('id',info.connectionToken);
    socket = new WebSocket(url);
    const timeout = setTimeout(() => {socket.close();reject(new Error('Подключение заняло слишком много времени'));},15000);
    socket.onopen = () => socket.send('{"protocol":"json","version":1}\x1e');
    socket.onerror = () => {clearTimeout(timeout);reject(new Error('Ошибка подключения'));};
    socket.onmessage = event => {
      for(const raw of event.data.split('\x1e').filter(Boolean)) {
        const message = JSON.parse(raw);
        if(message.type === undefined) {clearTimeout(timeout);if(message.error){reject(new Error(message.error));return;} connected=true;resolve();}
        if(message.type===3) {const p=pending.get(message.invocationId);if(p){clearTimeout(p.timer);pending.delete(message.invocationId);message.error?p.reject(new Error(message.error)):p.resolve(message.result);}}
        if(message.type===1) receive(message.target,message.arguments);
        if(message.type===7) socket.close();
      }
    };
    socket.onclose = () => { clearTimeout(timeout); connected=false; $('connection').textContent='Соединение потеряно · обновите страницу'; for(const p of pending.values()){clearTimeout(p.timer);p.reject(new Error('Соединение потеряно'));} pending.clear(); renderActions(); reject(new Error('Соединение закрыто')); };
  });
  $('connection').textContent='● Онлайн';
  await refresh();
}
function receive(target,args) {
  if(target==='Connected') connectionId=args[0];
  if(target==='GameStateUpdated') {state=args[0];render();}
  if(target==='ChatMessageReceived') {const log=$('messages');log.append(node('p',`${args[0]}: ${args[1]}`));while(log.children.length>100)log.firstChild.remove();log.scrollTop=log.scrollHeight;}
  if(target==='RoomClosed') run(leave);
  if(target==='ServerMessage') error(args[0]);
}
function username(){const name=$('name').value.trim();if(!name)throw new Error('Введите ваше имя');localStorage.setItem('river-name',name);return name;}
async function refresh(){const rooms=await invoke('GetRooms');$('rooms').replaceChildren();if(!rooms.length)$('rooms').append(node('p','Пока нет столов. Создайте первый!'));for(const r of rooms){const el=node('article',undefined,'room');el.append(node('h2',r.roomName),node('p',`${r.mode===1?'Холдем':'Блэкджек'} · ${r.playerCount}/${r.maxPlayers} игроков · от ${r.minBet} фишек`));const b=node('button',r.availableSeats?'Занять место':'Смотреть');b.onclick=()=>run(()=>join(r.roomId,r.roomName,r.minBet));el.append(b);$('rooms').append(el);}}
async function join(id,title,minBet=10){roomMinBet=minBet;const name=username();roomId=id;try{if(!await invoke('JoinRoom',id,name,1000,false))throw new Error('Не удалось войти за стол');$('tableName').textContent=title||'Игровой стол';$('lobby').hidden=true;$('game').hidden=false;history.replaceState(null,'',`?room=${encodeURIComponent(id)}`);}catch(e){roomId=null;throw e;}}
async function leave(){if(roomId&&connected)await invoke('LeaveRoom',roomId);roomId=null;state=null;$('game').hidden=true;$('lobby').hidden=false;$('messages').replaceChildren();history.replaceState(null,'',location.pathname);await refresh();}
function cards(hand){const area=node('div',undefined,'cards');for(const c of hand){const hidden=!c.isFaceUp;const suit={Hearts:'♥',Diamonds:'♦',Clubs:'♣',Spades:'♠'}[c.suit]||c.suit;const val={Ace:'A',King:'K',Queen:'Q',Jack:'J'}[c.value]||c.value;const el=node('span',hidden?'♠':`${val}${suit}`,`card ${hidden?'back':/[♥♦]/.test(suit)?'red':''}`);el.setAttribute('aria-label',hidden?'Закрытая карта':`${c.value} ${c.suit}`);area.append(el);}return area;}
function render(){if(!state)return;$('gameMode').textContent=state.mode===1?'ТЕХАССКИЙ ХОЛДЕМ':'БЛЭКДЖЕК';$('phase').textContent=phases[state.phase];$('pot').textContent=state.mode===1?`Банк: ${state.pot} ◈`:`Дилер · ${state.dealerHandValue}`;$('board').replaceChildren(cards(state.mode===1?state.communityCards:state.dealerHand));$('players').replaceChildren();for(const p of state.players){const el=node('article',undefined,`player ${p.connectionId===connectionId?'me':''} ${p.connectionId===state.currentPlayerId?'active':''}`);el.append(node('h3',p.username+(p.connectionId===connectionId?' · вы':'')),cards(p.hand),node('p',`${p.balance} ◈ · Ставка ${p.currentBet}${p.isFolded?' · Пас':''}${p.isAllIn?' · Ва-банк':''}`));if(p.handDescription)el.append(node('p',p.handDescription));$('players').append(el);}$('hint').textContent=state.message||'Для покера нужны минимум два игрока. Для блэкджека достаточно одного.';renderActions();}
function renderActions(){const area=$('actions');area.replaceChildren();if(!state||!connected)return;const me=state.players.find(p=>p.connectionId===connectionId);const turn=state.phase===3&&state.currentPlayerId===connectionId;const betting=state.phase===0||state.phase===1;function button(text,method,args=[],enabled=true){const b=node('button',text);b.disabled=!enabled||busy;b.onclick=()=>run(async()=>{busy=true;renderActions();try{await invoke(method,roomId,...args);}finally{busy=false;renderActions();}});area.append(b);}if(!me){area.append(node('p','Вы наблюдаете за игрой.'));button('Занять свободное место','TakeSeat',[Array.from({length:6},(_,i)=>i).find(i=>!state.players.some(p=>p.seatPosition===i))??0]);return;}
  if(betting&&state.mode===1)button('Готов к раздаче','SetReady',[],true);
  if((betting&&state.mode===0)||(turn&&state.mode===1)){const amount=node('input');amount.type='number';amount.min='1';amount.max=String(me.balance);amount.value=String(state.mode===1?Math.max(state.minRaise,1):roomMinBet);amount.setAttribute('aria-label','Размер ставки');area.append(amount);const b=node('button',state.mode===1?'Повысить':'Поставить');b.disabled=busy;b.onclick=()=>run(async()=>{const n=Number(amount.value);if(!Number.isSafeInteger(n)||n<(state.mode===0?roomMinBet:1)||n>me.balance)throw new Error('Проверьте размер ставки');await invoke(state.mode===1?'PokerRaise':'PlaceBet',roomId,n);});area.append(b);}
  if(state.mode===1){button('Пас','PokerFold',[],turn);button('Чек','PokerCheck',[],turn&&me.totalBetThisRound>=state.currentBetToMatch);button(`Колл ${Math.max(0,state.currentBetToMatch-me.totalBetThisRound)}`,'PokerCall',[],turn);button('Ва-банк','PokerAllIn',[],turn);}else{button('Ещё карту','Hit',[],turn);button('Хватит','Stand',[],turn);}
  if(state.phase===5||state.phase===6){button('Следующий раунд','NewRound');if(state.isShowdownPending){button('Показать карты','ShowCards');button('Скрыть карты','MuckCards');}}
}
$('name').value=localStorage.getItem('river-name')||'';
$('refresh').onclick=()=>run(refresh);$('leave').onclick=()=>run(leave);
$('create').onsubmit=e=>{e.preventDefault();run(async()=>{username();const min=Number($('minBet').value);if(!Number.isSafeInteger(min)||min<1||min>100)throw new Error('Ставка должна быть от 1 до 100');const room=await invoke('CreateRoom',$('roomName').value.trim(),Number($('mode').value),min,false);if(!room)throw new Error('Не удалось создать стол');await join(room.roomId,room.roomName,room.minBet);});};
$('invite').onclick=()=>run(async()=>{await navigator.clipboard.writeText(location.href);$('hint').textContent='Приглашение скопировано. Отправьте ссылку друзьям.';});
$('chat').onsubmit=e=>{e.preventDefault();run(async()=>{await invoke('SendChatMessage',roomId,$('message').value);$('message').value='';});};
setInterval(()=>{if(connected)socket.send('{"type":6}\x1e');if(state?.turnStartTime&&state.phase===3){const remaining=Math.max(0,state.turnTimeoutSeconds-Math.floor((Date.now()-Date.parse(state.turnStartTime))/1000));$('timer').textContent=`На ход: ${remaining} сек.`;}else $('timer').textContent='';},5000);
setInterval(()=>{if(connected&&!roomId)run(refresh);},15000);
run(async()=>{await connect();const id=new URLSearchParams(location.search).get('room');if(id){$('hint').textContent='';const rooms=await invoke('GetRooms');const r=rooms.find(r=>r.roomId===id);if(r){const b=node('button','Присоединиться к приглашению');b.onclick=()=>run(()=>join(id,r.roomName,r.minBet));$('rooms').prepend(b);}else error('Стол из приглашения уже закрыт.');}});
