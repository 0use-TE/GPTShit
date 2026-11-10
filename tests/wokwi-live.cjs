// Requires: npm install --prefix work playwright; serve the published Web folder at localhost:8765.
const { chromium } = require('../work/node_modules/playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage();
  await page.addInitScript(() => {
    window.messages = [];
    window.invokeCSharpAction = body => {
      const m = JSON.parse(body); window.messages.push(m);
      if (m.type === 'telemetry') window.latest = m;
      if (m.type === 'error') console.error('DEVICE', m.message);
    };
  });
  page.on('pageerror', e => console.log('PAGE', e.message));
  page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning' || m.text().includes('chip')) console.log(m.type(),m.text()); });
  let sequence = 0;
  async function command(c) {
    c.id = String(++sequence);
    await page.evaluate(c => window.controlLab.send(c), c);
    await page.waitForFunction(id => window.messages.some(m => m.type === 'ack' && m.id === id), c.id, { timeout: 12000 });
  }
  async function until(predicate, timeout = 120000) { await page.waitForFunction(predicate, null, { timeout }); }
  try {
    await page.goto('http://localhost:8765');
    await until(() => window.messages.some(m => m.type === 'connected'));
    await page.evaluate(() => window.controlLab.start());
    await until(() => window.messages.some(m => m.type === 'ready'));
    await until(() => window.latest);
    assert.equal((await page.evaluate(() => window.latest.fault)), ''); assert(Math.abs((await page.evaluate(() => window.latest.temperature)) - 25) < 0.2);
    console.log('PASS MCU + custom thermal model + I2C temperature');
    await command({ command: 'configure', target: 50, kp: 8, ki: 0.7, kd: 4 });
    await command({ command: 'enable', enabled: true });
    for (let boundary = 10; boundary <= 70; boundary += 10) {
      await page.waitForFunction(t => window.latest?.uptime >= t * 1000, boundary, { timeout: 60000 });
      console.log('HEATING', JSON.stringify(await page.evaluate(() => window.latest)));
    }
    let last = await page.evaluate(() => window.latest);
    assert(Math.abs(last.temperature - 50) < 0.8, 'Must settle near 50 C');
    console.log('PASS setpoint tracking at 50 C');
    const before = last.temperature;
    await command({ command: 'door', duration: 5 });
    await until(() => window.latest.door === true, 12000);
    const opened = await page.evaluate(() => window.latest.uptime);
    await until(() => window.latest.door === false, 20000);
    const closedAt = await page.evaluate(() => window.latest.uptime);
    const cooled = await page.evaluate(() => window.latest.temperature);
    assert(cooled < before - 3, 'Door must change the physical model temperature');
    assert((await page.evaluate(() => window.latest.uptime)) - opened >= 4000);
    console.log('PASS door disturbance', { before, cooled });
    await until(() => Math.abs(window.latest.temperature - 50) < 0.8, 90000);
    await page.waitForFunction(t => window.latest.uptime >= t + 35000, closedAt, { timeout: 90000 });
    const stable = await page.evaluate(() => window.messages.filter(m => m.type === 'telemetry' && m.uptime >= window.latest.uptime - 5000));
    assert(stable.every(m => Math.abs(m.temperature - 50) < 0.8), 'Recovery must remain stable for the final five seconds');
    console.log('PASS sustained recovery after door closes', stable.at(-1).temperature);
    await command({ command: 'enable', enabled: false });
    await until(() => !window.latest.enabled && window.latest.output === 0, 12000);
    console.log('PASS stop cuts heating PWM');
    await command({ command: 'fault', value: true });
    await until(() => window.latest.fault === 'Sensor fault', 12000);
    assert.equal((await page.evaluate(() => window.latest.output)), 0);
    console.log('PASS sensor fault cuts heating');
    await command({ command: 'fault', value: false });
    await command({ command: 'enable', enabled: true });
    await until(() => window.latest.enabled && window.latest.fault === '', 12000);
    console.log('PASS explicit recovery after sensor repair');
    await command({ command: 'enable', enabled: false });
    const telemetry = await page.evaluate(() => window.messages.filter(m => m.type === 'telemetry'));
    fs.writeFileSync('work/live-thermal-telemetry.json', JSON.stringify(telemetry, null, 2));
    await page.screenshot({ path: 'work/thermal-wokwi.png' });
  } catch (error) {
    console.error(error);
    console.log('LAST', JSON.stringify(await page.evaluate(() => window.messages.slice(-15))));
    await page.screenshot({ path: 'work/thermal-wokwi-error.png' });
    process.exitCode = 1;
  } finally { await browser.close(); }
})();
