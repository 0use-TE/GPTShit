const source = document.querySelector('#source');
const code = document.querySelector('#source-code');
const tabs = [...document.querySelectorAll('[role=tab]')];
const files = new Map();
let selection = 0;
async function select(tab) {
  const request = ++selection;
  for (const item of tabs) item.setAttribute('aria-selected', String(item === tab));
  const name = tab.dataset.source;
  document.querySelector('#workspace').classList.toggle('has-source', Boolean(name));
  source.hidden = !name;
  if (!name) return;
  source.setAttribute('aria-label', name + ' 下位机代码');
  code.textContent = '正在加载…';
  try {
    if (!files.has(name)) files.set(name, fetch(name).then(response => {
      if (!response.ok) throw new Error('无法读取 ' + name);
      return response.text();
    }).catch(error => { files.delete(name); throw error; }));
    const text = await files.get(name);
    if (request !== selection) return;
    // Highlight a whole file first so multiline Python strings retain their token color.
    const html = Prism.highlight(text.replace(/\r\n/g, '\n'), Prism.languages.python, 'python');
    const template = document.createElement('template');
    template.innerHTML = html;
    code.replaceChildren();
    let line = document.createElement('span'); line.className = 'code-line'; code.append(line);
    function append(node, classes = []) {
      if (node.nodeType === Node.TEXT_NODE) {
        node.textContent.split('\n').forEach((part, index) => {
          if (index) { line = document.createElement('span'); line.className = 'code-line'; code.append(line); }
          const token = document.createElement('span'); token.className = classes.join(' '); token.textContent = part; line.append(token);
        });
      } else for (const child of node.childNodes) append(child, [...classes, ...node.classList]);
    }
    for (const node of template.content.childNodes) append(node);
    source.scrollTop = 0;
  } catch (error) { if (request === selection) code.textContent = error.message; }
}
for (const tab of tabs) {
  tab.addEventListener('click', () => select(tab));
  tab.addEventListener('keydown', event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const index = tabs.indexOf(tab);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
    tabs[next].focus(); select(tabs[next]);
  });
}
