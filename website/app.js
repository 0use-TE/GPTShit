const previews = {
  dark: { image: 'assets/dark.webp', caption: '控制、曲线、电路与日志，在同一个桌面工作区。', alt: 'ControlLab 深色工作区：控制面板、实时温度曲线、ESP32 电路与通信日志' },
  light: { image: 'assets/light.webp', caption: '切换浅色主题，Wokwi 电路画布也同步变化。', alt: 'ControlLab 浅色工作区，Avalonia 界面与 Wokwi 电路画布保持浅色主题' },
  code: { image: 'assets/code.webp', caption: '电路旁边就是 PID 源码。Python 高亮与行号，让每一步计算清晰可读。', alt: 'ControlLab 电路与下位机 PID Python 代码并排显示，代码具有语法高亮和行号' },
};
const tabs = [...document.querySelectorAll('[data-preview]')];
function selectPreview(tab) {
  const preview = previews[tab.dataset.preview];
  for (const item of tabs) {
    const selected = item === tab;
    item.setAttribute('aria-selected', String(selected));
    item.tabIndex = selected ? 0 : -1;
  }
  const image = document.querySelector('#preview-image');
  image.src = preview.image;
  image.alt = preview.alt;
  const button = document.querySelector('#preview-open');
  button.dataset.image = preview.image;
  button.dataset.caption = preview.caption;
  document.querySelector('#preview-caption').textContent = preview.caption;
  document.querySelector('#preview-panel').setAttribute('aria-labelledby', tab.id);
}
tabs.forEach((tab, index) => {
  tab.addEventListener('click', () => selectPreview(tab));
  tab.addEventListener('keydown', event => {
    let next;
    if (event.key === 'ArrowRight') next = tabs[(index + 1) % tabs.length];
    if (event.key === 'ArrowLeft') next = tabs[(index - 1 + tabs.length) % tabs.length];
    if (event.key === 'Home') next = tabs[0];
    if (event.key === 'End') next = tabs[tabs.length - 1];
    if (next) { event.preventDefault(); next.focus(); selectPreview(next); }
  });
});
const viewer = document.querySelector('#image-viewer');
for (const button of document.querySelectorAll('[data-image]')) {
  button.addEventListener('click', () => {
    const image = document.querySelector('#viewer-image');
    image.src = button.dataset.image;
    image.alt = button.dataset.caption;
    document.querySelector('#viewer-caption').textContent = button.dataset.caption;
    viewer.showModal();
  });
}
document.querySelector('#close-viewer').addEventListener('click', () => viewer.close());
viewer.addEventListener('click', event => {
  if (event.target !== viewer) return;
  const rect = viewer.getBoundingClientRect();
  if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) viewer.close();
});
