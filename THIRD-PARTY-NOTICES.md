# Third-party notices

Single-file packages use the wrappe 1.0.6 runner from https://github.com/Systemcluster/wrappe.
Copyright (c) 2020 Christian Sdunek and the wrappe contributors; BSD-2-Clause.
The complete notice is included as `Licenses/wrappe.txt` in the packed payload;
source copy: `tools/licenses/wrappe-LICENSE.txt`.

`src/ControlLab/ControlLab/Assets/Web/vendor/` contains `base64.js`,
`message-port-transport.js`, and `wokwi-client.js` from
https://github.com/wokwi/wokwi-embed-example (retrieved 2026-11-09).

Copyright (c) 2025 CodeMagic LTD. Licensed under the MIT License.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

`firmware/vendor/wokwi-api.h` is from https://github.com/wokwi/inverter-chip,
Copyright (c) 2022 Uri Shaked, MIT license. See `firmware/vendor/LICENSE`.

`firmware/runtime/micropython.bin` is the ESP32 Generic MicroPython v1.22.0
image dated 2023-12-27, retrieved from
https://wokwi.github.io/firmware-assets/micropython/ESP32_GENERIC-20231227-v1.22.0.bin.
MicroPython is Copyright (c) 2013-2023 Damien P. George and contributors,
MIT license; see `firmware/runtime/MICROPYTHON-LICENSE`.
The firmware image also contains ESP-IDF components distributed by the
upstream MicroPython/Wokwi firmware build. Upstream source and notices:
https://github.com/micropython/micropython/tree/v1.22.0/ports/esp32 and
https://github.com/espressif/esp-idf.

The Wokwi service, simulator, hardware models, and hosted assets are separate
from this sample client code. They are loaded from Wokwi and are subject to
Wokwi's applicable terms. This project does not redistribute that simulator.

PrismJS 1.30.0 (core and Python grammar) is distributed in `Assets/Web/vendor/`.
Copyright (c) 2012 Lea Verou; MIT license. See `vendor/prism-LICENSE`.
Source: https://github.com/PrismJS/prism/tree/v1.30.0.
