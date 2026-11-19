import { chromium } from '../.tools/browser/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const base = process.env.BABYLOG_URL || 'http://127.0.0.1:5080';
const date = '2026-11-24';
const marker = `browser-check-${Date.now()}`;
await mkdir('.artifacts/ui', { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const summary = async () => (await page.request.get(`${base}/api/summary?date=${date}`)).json();
const entries = async () => (await page.request.get(`${base}/api/activities?date=${date}`)).json();
const baseline = await summary();
const save = async () => {
  await page.getByRole('button', { name: '+ Aggiungi al diario' }).click();
  await page.getByRole('status').filter({ hasText: 'Momento aggiunto al diario.' }).waitFor();
};
try {
  const connected = page.waitForEvent('console', { predicate: message => message.text().includes('WebSocket connected'), timeout: 15000 });
  await page.goto(base);
  await connected;
  await page.getByLabel('Data del diario').fill(date);
  await page.getByLabel('Data del diario').press('Tab');
  await page.getByRole('heading', { name: 'martedì 24 novembre 2026' }).waitFor();
  await page.getByLabel('Orario · ora di Roma').fill(`${date}T08:15`);
  await page.getByLabel('Quantità di latte · ml').fill('125');
  await page.getByLabel('Una piccola nota · facoltativa').fill(`${marker}-milk`);
  await save();
  await page.getByRole('button', { name: 'Cambio', exact: true }).click();
  await page.getByLabel('Orario · ora di Roma').fill(`${date}T09:15`);
  await page.getByLabel('Una piccola nota · facoltativa').fill(`${marker}-diaper`);
  await save();
  await page.getByRole('button', { name: 'Riposo', exact: true }).click();
  await page.getByLabel('Inizio del riposo · ora di Roma').fill(`${date}T10:00`);
  await page.getByLabel('Fine del riposo · ora di Roma').fill(`${date}T09:00`);
  await page.getByRole('button', { name: '+ Aggiungi al diario' }).click();
  await page.getByRole('alert').filter({ hasText: "La fine del riposo deve essere successiva all'inizio." }).waitFor();
  await page.getByLabel('Fine del riposo · ora di Roma').fill(`${date}T11:30`);
  await page.getByLabel('Una piccola nota · facoltativa').fill(`${marker}-sleep`);
  await save();
  const after = await summary();
  assert.equal(after.milkMilliliters, baseline.milkMilliliters + 125);
  assert.equal(after.diaperCount, baseline.diaperCount + 1);
  assert.equal(after.sleepMinutes, baseline.sleepMinutes + 90);
  assert.equal((await entries()).filter(x => x.notes?.startsWith(marker)).length, 3);
  await page.screenshot({ path: '.artifacts/ui/desktop-tested.png', fullPage: true });
  const milk = page.locator('li').filter({ hasText: `${marker}-milk` });
  await milk.getByRole('button', { name: 'Elimina Poppata delle 08:15' }).click();
  await milk.getByRole('button', { name: 'Annulla', exact: true }).click();
  assert.equal((await entries()).filter(x => x.notes?.startsWith(marker)).length, 3);
  await milk.getByRole('button', { name: 'Elimina Poppata delle 08:15' }).click();
  await milk.getByRole('button', { name: 'Elimina', exact: true }).click();
  await page.getByRole('status').filter({ hasText: 'Momento eliminato.' }).waitFor();
  assert.equal((await summary()).milkMilliliters, baseline.milkMilliliters);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: '.artifacts/ui/mobile-tested.png', fullPage: true });
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Mobile horizontal overflow');
  assert.deepEqual(errors, []);
  const report = { passed: true, base, checkedAt: new Date().toISOString(), checks: ['milk create', 'diaper create', 'sleep validation/create', 'summary', 'delete cancel/confirm', 'mobile no overflow', 'no JS errors'] };
  await writeFile('.artifacts/ui/result.json', JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} catch (error) {
  await page.screenshot({ path: '.artifacts/ui/failure.png', fullPage: true });
  console.error(await page.locator('main').ariaSnapshot());
  throw error;
} finally {
  for (const entry of await entries()) {
    if (entry.notes?.startsWith(marker)) await page.request.delete(`${base}/api/activities/${entry.id}`);
  }
  await browser.close();
}
