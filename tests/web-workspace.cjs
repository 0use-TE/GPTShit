const { chromium } = require('../work/node_modules/playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    await page.addInitScript({ path: 'src/ControlLab/ControlLab/Assets/Web/wokwi-theme.js' });
    await page.addInitScript(() => { window.invokeCSharpAction = body => { const m = JSON.parse(body); if(m.type === 'telemetry') window.latest = m; if(m.type === 'connected') window.simConnected = true; }; });
    await page.goto('http://localhost:8765');
    await page.waitForFunction(() => window.simConnected);
    const frame = page.frames().find(f => f.url().startsWith('https://wokwi.com/experimental/embed'));
    const icon = frame.locator('button[aria-label="toggle dark mode"] svg');
    await icon.waitFor();
    for (const dark of [false, true]) {
      await page.evaluate(dark => controlLab.setTheme(dark), dark);
      await frame.waitForFunction(dark => [...document.querySelector('[class*=simulation_container]').classList].some(name => name.includes('simulation_darkMode')) === dark, dark);
      assert.equal(await page.locator('html').getAttribute('data-theme'), dark ? 'dark' : 'light');
    }
    await page.getByRole('tab', {name:'main.py',exact:true}).click();
    await page.waitForFunction(() => document.querySelectorAll('#source-code .token.keyword').length > 5);
    assert.match(await page.locator('#source-code').textContent(), /ESP32 thermostat/);
    assert.ok(await page.locator('#source-code .code-line').count() > 100);
    await page.getByRole('tab', {name:'pid.py',exact:true}).click();
    await page.waitForFunction(() => document.querySelector('#source-code').textContent.includes('class PID'));
    assert.ok(await page.locator('#source-code .token.keyword').count() > 5);
    await page.getByRole('tab', {name:'电路',exact:true}).click();
    await page.evaluate(() => controlLab.start());
    await page.waitForFunction(() => window.latest?.uptime > 2000, null, { timeout: 60000 });
    const before = await page.evaluate(() => latest.uptime);
    await page.evaluate(() => controlLab.setTheme(false));
    await frame.waitForFunction(() => document.querySelector('[data-testid="LightModeIcon"]'));
    await page.getByRole('tab', {name:'main.py',exact:true}).click();
    await page.waitForFunction(ms => window.latest?.uptime > ms + 1000, before);
    assert.equal(await page.locator('#source').isVisible(), true);
    await page.getByRole('tab', {name:'电路',exact:true}).click();
    assert.equal(await frame.locator('[data-testid="LightModeIcon"]').count(), 1);
    // Reconnect while light: reload the parent with the same persisted host theme.
    await page.addInitScript(() => { window.addEventListener('DOMContentLoaded', () => { const timer=setInterval(()=>{if(window.controlLab){controlLab.setTheme(false);clearInterval(timer);}},10); }); });
    await page.reload();
    await page.waitForFunction(() => window.simConnected);
    const reconnected = page.frames().find(f => f.url().startsWith('https://wokwi.com/experimental/embed'));
    await reconnected.waitForFunction(() => document.querySelector('[data-testid="LightModeIcon"]'));
    console.log('PASS web code highlighting / both themes / live MCU preserved / theme restored on reconnect');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
