// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2025 CodeMagic LTD

import { byteArrayToBase64, base64ToByteArray } from './base64.js';

/**
 * @typedef {Object} IEventData
 * @property {string} name - Event name (e.g. serial-monitor:data)
 * @property {number} nanos - Simulation timestamp when the event happened
 * @property {boolean} paused - Whether the simulator is paused
 */

/**
 * @callback APIEventListener
 * @param {*} payload
 * @param {IEventData} eventData
 */

/**
 * @typedef {Object} APICommand
 * @property {string} type
 * @property {string} command
 * @property {*} params
 * @property {string} id
 */

/**
 * @typedef {Object} APIHello
 * @property {string} type
 */

/**
 * @typedef {Object} APIEvent
 * @property {string} type
 * @property {string} event
 * @property {*} payload
 * @property {number} nanos
 * @property {boolean} paused
 */

/**
 * @typedef {Object} APIResponse
 * @property {string} type
 * @property {string} id
 * @property {*} result
 * @property {boolean} [error]
 */

/**
 * @typedef {Object} APIResultError
 * @property {number} code
 * @property {string} message
 */

export class WokwiClient extends EventTarget {
  lastId = 0;
  pendingCommands = new Map();

  /**
   * @param {import('./message-port-transport.js').MessagePortTransport} transport
   */
  constructor(transport) {
    super();
    this.transport = transport;
    this.transport.onMessage = (message) => {
      this.processMessage(message);
    };
  }

  /**
   * Upload a file to the simulator
   * 
   * @param {string} name
   * @param {string|ArrayBuffer} content
   * @returns {Promise<void>}
   */
  async fileUpload(name, content) {
    if (typeof content === 'string') {
      return this.sendCommand('file:upload', { name, text: content });
    } else {
      return this.sendCommand('file:upload', {
        name,
        binary: byteArrayToBase64(content),
      });
    }
  }

  /**
   * Download a file from the simulator
   * 
   * @param {string} name 
   * @returns {Promise<string|Uint8Array>} The file content as a string or Uint8Array
   */
  async fileDownload(name) {
    const result = await this.sendCommand('file:download', { name });
    if (typeof result.text === 'string') {
      return result.text;
    } else {
      return new base64ToByteArray(result.binary);
    }
  }

  /**
   * @param {Object} params
   */
  async simStart(params) {
    return this.sendCommand('sim:start', params);
  }

  async simPause() {
    return this.sendCommand('sim:pause');
  }

  /**
   * @param {number} [pauseAfter]
   */
  async simResume(pauseAfter) {
    return this.sendCommand('sim:resume', { pauseAfter });
  }

  /**
   * @param {{ pause?: boolean }} [opts]
   */
  async simRestart({ pause } = {}) {
    return this.sendCommand('sim:restart', { pause });
  }

  /**
   * @returns {Promise<{ running: boolean, nanos: number }>}
   */
  async simStatus() {
    return this.sendCommand('sim:status');
  }

  async serialMonitorListen() {
    return this.sendCommand('serial-monitor:listen');
  }

  /**
   * @param {number[]|Uint8Array} bytes
   */
  async serialMonitorWrite(bytes) {
    return this.sendCommand('serial-monitor:write', { bytes: Array.from(bytes) });
  }

  /**
   * @returns {Promise<{ pins: string[] }>}
   */
  async gpioList() {
    return this.sendCommand('gpio:list');
  }

  /**
   * @param {string} pin
   * @param {boolean} value
   */
  async gpioWrite(pin, value) {
    return this.sendCommand('gpio:write', { pin, value });
  }

  /**
   * @param {string} part
   * @param {string} pin
   * @returns {Promise<{ pin: string, direction: ('input'|'output'), value: boolean, pullUp: boolean, pullDown: boolean }>}
   */
  async pinRead(part, pin) {
    return this.sendCommand('pin:read', { part, pin });
  }

  /**
   * @param {string} command
   * @param {*} [params]
   */
  async sendCommand(command, params) {
    return new Promise((resolve, reject) => {
      const id = this.lastId++;
      this.pendingCommands.set(id.toString(), [resolve, reject]);
      const message = { type: 'command', command, params, id: id.toString() };
      this.transport.send(message);
    });
  }

  /**
   * @param {APIHello|APIEvent|APIResponse} message
   */
  processMessage(message) {
    switch (message.type) {
      case 'hello':
        this.dispatchEvent(new CustomEvent('wokwi:connected', { detail: message }));
        break;

      case 'event':
        this.processEvent(message);
        break;

      case 'response':
        this.processResponse(message);
        break;
    }
  }

  /**
   * @param {APIEvent} message
   */
  processEvent(message) {
    this.dispatchEvent(new CustomEvent(message.event, { detail: message.payload }));
  }

  /**
   * @param {APIResponse} message
   */
  processResponse(message) {
    const id = message.id ?? '';
    const [resolve, reject] = this.pendingCommands.get(id) ?? [];
    if (resolve && reject) {
      this.pendingCommands.delete(id);
      if (message.error) {
        const { result } = message;
        reject(new Error(`Error ${result.code}: ${result.message}`));
      } else {
        resolve(message.result);
      }
    } else {
      console.error('Unknown response', message);
    }
  }
}
