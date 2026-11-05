const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const base = process.env.POKER_TEST_URL || 'http://127.0.0.1:8080';

async function online(page) {
  await page.getByText('● Онлайн', { exact: true }).waitFor();
}

async function gameFlow(browser) {
  const alice = await browser.newPage();
  const bob = await browser.newPage();
  const errors = [];
  for (const page of [alice, bob]) {
    page.on('pageerror', error => errors.push(error.message));
    // Reproduce a deployment where only the HTML document is reachable.
    await page.route(/\.(css|js)(\?|$)/, route => route.abort());
  }
  try {
    await alice.goto(base);
    await online(alice);
    assert.equal(await alice.locator('link[rel=stylesheet], script[src]').count(), 0);
    assert.equal(await alice.evaluate(() => getComputedStyle(document.documentElement).backgroundColor), 'rgb(12, 23, 20)');
    await alice.locator('#name').fill('Alice');
    await alice.locator('#roomName').fill('Browser poker smoke');
    await alice.locator('#create button').click();
    await alice.locator('#game').waitFor({ state: 'visible' });
    await bob.goto(alice.url());
    await online(bob);
    await bob.locator('#name').fill('Bob');
    await bob.getByRole('button', { name: 'Присоединиться к приглашению' }).click();
    await alice.getByRole('button', { name: 'Готов к раздаче' }).click();
    await bob.getByRole('button', { name: 'Готов к раздаче' }).click();
    for (const page of [alice, bob]) {
      await page.waitForFunction(() => document.querySelector('#phase').textContent === 'Ход игрока');
    }
    assert.equal(await alice.locator('.player.me .card:not(.back)').count(), 2);
    assert.equal(await alice.locator('.player:not(.me) .card.back').count(), 2);
    const acting = await alice.getByRole('button', { name: 'Пас', exact: true }).isEnabled() ? alice : bob;
    await acting.getByRole('button', { name: 'Пас', exact: true }).click();
    await alice.waitForFunction(() => document.querySelector('#phase').textContent === 'Раунд завершён');
    for (const page of [alice, bob]) await page.locator('summary').click();
    await alice.locator('#message').fill('<b>Hello</b>');
    await alice.locator('#chat button').click();
    await bob.locator('#messages').getByText('Alice: <b>Hello</b>', { exact: true }).waitFor();
    assert.equal(await bob.locator('#messages b').count(), 0);
    await alice.locator('#leave').click();
    await bob.locator('#leave').click();
    await alice.locator('#mode').selectOption('0');
    await alice.locator('#minBet').fill('50');
    await alice.locator('#roomName').fill('Browser blackjack smoke');
    await alice.locator('#create button').click();
    assert.equal(await alice.getByRole('spinbutton', { name: 'Размер ставки' }).inputValue(), '50');
    await alice.getByRole('button', { name: 'Поставить', exact: true }).click();
    await alice.waitForFunction(() => ['Ход игрока', 'Ход дилера', 'Раунд завершён'].includes(document.querySelector('#phase').textContent));
    if (await alice.getByRole('button', { name: 'Хватит', exact: true }).isEnabled()) {
      await alice.getByRole('button', { name: 'Хватит', exact: true }).click();
    }
    await alice.waitForFunction(() => document.querySelector('#phase').textContent === 'Раунд завершён', null, { timeout: 20000 });
    await alice.setViewportSize({ width: 390, height: 844 });
    assert.equal(await alice.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
    if (process.env.POKER_TEST_SCREENSHOT) await alice.screenshot({ path: process.env.POKER_TEST_SCREENSHOT, fullPage: true });
    assert.deepEqual(errors, []);
    await alice.locator('#leave').click();
    console.log('PASS: embedded UI with blocked assets; two-player poker; invitations; chat; blackjack; mobile layout');
  } finally {
    await alice.close();
    await bob.close();
  }
}

async function failedConnection(browser) {
  const page = await browser.newPage();
  try {
    await page.route('**/gamehub/negotiate?*', route => route.fulfill({ status: 503, body: 'Unavailable' }));
    await page.goto(base);
    await page.getByText('Нет соединения с сервером', { exact: true }).waitFor();
    assert.equal(await page.locator('#create button').isDisabled(), true);
    await page.getByText('Не удалось подключиться к игре.', { exact: false }).waitFor();
    await page.unroute('**/gamehub/negotiate?*');
    await page.getByRole('button', { name: 'Подключиться снова' }).click();
    await online(page);
    assert.equal(await page.locator('#create button').isEnabled(), true);
    console.log('PASS: failed connection shows error; retry restores a usable lobby');
  } finally { await page.close(); }
}

async function httpFallback(browser) {
  const page = await browser.newPage();
  let polls = 0;
  try {
    await page.addInitScript(() => {
      window.WebSocket = undefined;
      window.EventSource = undefined;
    });
    page.on('request', request => {
      if (request.method() === 'GET' && new URL(request.url()).pathname === '/gamehub') polls++;
    });
    await page.goto(base);
    await online(page);
    await page.locator('#name').fill('HTTP player');
    await page.locator('#mode').selectOption('0');
    await page.locator('#roomName').fill('HTTP fallback smoke');
    await page.locator('#create button').click();
    await page.locator('#game').waitFor({ state: 'visible' });
    await page.getByText('HTTP player · вы', { exact: true }).waitFor();
    assert.ok(polls > 0, 'Expected Long Polling requests');
    await page.locator('#leave').click();
    console.log('PASS: HTTP Long Polling connects and joins a table without WebSocket or SSE');
  } finally { await page.close(); }
}

(async () => {
  const browser = await chromium.launch({
    executablePath: process.env.CHROMIUM_PATH || undefined,
    headless: true,
    args: ['--no-sandbox']
  });
  try {
    await gameFlow(browser);
    await failedConnection(browser);
    await httpFallback(browser);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
