import { WokwiClient } from './vendor/wokwi-client.js';
import { MessagePortTransport } from './vendor/message-port-transport.js';
const frame = document.querySelector('#simulator');
const status = document.querySelector('#status');
let client, running = false, starting = false, initialized = false, serialBuffer = '', bootTimer;
const decoder = new TextDecoder();
const pending = new Set();
function emit(message) {
  window.dispatchEvent(new CustomEvent('control-lab-message', { detail: message }));
  if (typeof window.invokeCSharpAction === 'function') window.invokeCSharpAction(JSON.stringify(message));
  else console.info('[ControlLab]', message);
}
function state(type, message) { status.textContent = message; emit({ type, message }); }
function withTimeout(promise, milliseconds = 20000) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Wokwi 接口响应超时，请重新连接')), milliseconds);
    promise.then(value => { clearTimeout(timer); resolve(value); }, error => { clearTimeout(timer); reject(error); });
  });
}
async function loadFile(path) {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`读取 ${path} 失败：${response.status}`);
  return response.text();
}
function receiveSerial(bytes) {
  serialBuffer += decoder.decode(new Uint8Array(bytes), { stream: true });
  if (serialBuffer.length > 65536) serialBuffer = serialBuffer.slice(-8192);
  let end;
  while ((end = serialBuffer.indexOf('\n')) >= 0) {
    const line = serialBuffer.slice(0, end).trim(); serialBuffer = serialBuffer.slice(end + 1);
    if (!line) continue;
    emit({ type: 'serial', message: line });
    let message;
    try { message = JSON.parse(line); } catch { continue; }
    if (['ready', 'ack', 'heartbeat', 'error'].includes(message.type)) {
      if (message.type === 'ack') pending.delete(message.id);
      if (message.type === 'ready') { clearTimeout(bootTimer); starting = false; running = true; status.textContent = 'ESP32 程序已就绪 · GPIO2 → LED'; }
      emit(message);
    }
  }
}
window.addEventListener('message', event => {
  if (event.origin !== 'https://wokwi.com' || event.source !== frame.contentWindow || !event.data?.port || client) return;
  client = new WokwiClient(new MessagePortTransport(event.data.port));
  client.addEventListener('serial-monitor:data', event => receiveSerial(event.detail.bytes));
  client.addEventListener('wokwi:connected', () => initialize().catch(fail));
});
async function initialize() {
  state('loading', '正在加载电路和下位机程序…');
  await withTimeout(client.serialMonitorListen());
  await withTimeout(client.fileUpload('main.py', await loadFile('main.py')));
  await withTimeout(client.fileUpload('diagram.json', await loadFile('diagram.json')));
  initialized = true; state('connected', '电路已加载，点击「启动仿真」');
}
function fail(error) { state('error', error.message || String(error)); }
window.controlLab = {
  setTheme(isDark) {
    document.documentElement.dataset.theme = isDark ? 'dark' : 'light';
    document.documentElement.style.colorScheme = isDark ? 'dark' : 'light';
    frame.style.colorScheme = isDark ? 'dark' : 'light';
  },
  async start() {
    try {
      if (!initialized) throw new Error('请等待 Wokwi 连接完成');
      if (running || starting) return;
      starting = true;
      serialBuffer = ''; pending.clear(); state('starting', '正在启动 ESP32 下位机程序…');
      bootTimer = setTimeout(() => { starting = false; if (!running) fail(new Error('下位机在两分钟内未就绪，请重新连接')); }, 120000);
      await withTimeout(client.simStart({ firmware: 'main.py', elf: 'main.py' }), 120000);
    } catch (error) { clearTimeout(bootTimer); starting = false; fail(error); }
  },
  async send(command) {
    try {
      if (!running) throw new Error('下位机尚未就绪');
      pending.add(command.id);
      await withTimeout(client.serialMonitorWrite(new TextEncoder().encode(JSON.stringify(command) + '\n')));
      emit({ type: 'tx', message: JSON.stringify(command) });
      setTimeout(() => {
        if (pending.delete(command.id)) emit({ type: 'error', message: `下位机未确认命令 ${command.id}（5秒超时）` });
      }, 5000);
    } catch (error) { pending.delete(command.id); fail(error); }
  },
  reconnect() { location.reload(); }
};
document.querySelector('#start').onclick = () => window.controlLab.start();
document.querySelector('#on').onclick = () => window.controlLab.send({ command: 'led', value: true, id: crypto.randomUUID() });
document.querySelector('#off').onclick = () => window.controlLab.send({ command: 'led', value: false, id: crypto.randomUUID() });
document.querySelector('#ping').onclick = () => window.controlLab.send({ command: 'ping', id: crypto.randomUUID() });
if (typeof window.invokeCSharpAction !== 'function') document.body.classList.add('browser');
try {
  const config = JSON.parse(await loadFile('config.json'));
  const url = new URL(config.embedUrl); url.searchParams.set('client_id', config.clientId); frame.src = url.href;
  setTimeout(() => { if (!initialized) state('error', 'Wokwi 尚未连接。请检查网络、client_id，或点击「重新连接」。'); }, 30000);
} catch (error) { fail(error); }
