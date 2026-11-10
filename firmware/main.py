"""Runs inside the simulated ESP32, not in the host application."""
from machine import Pin, UART
import time
import ujson

led = Pin(2, Pin.OUT, value=0)
uart = UART(2, baudrate=115200, tx=17, rx=16)
buffer = bytearray()

def send(message):
    uart.write(ujson.dumps(message) + "\n")

send({"type": "ready", "device": "ESP32", "led": False})
last_heartbeat = time.ticks_ms()
while True:
    if uart.any():
        data = uart.read(1)
        if data:
            if data == b"\n":
                try:
                    command = ujson.loads(buffer.decode())
                    op = command.get("command")
                    if op == "led":
                        led.value(1 if command.get("value") else 0)
                    elif op != "ping":
                        raise ValueError("Unknown command")
                    send({"type": "ack", "id": command.get("id", ""),
                          "led": bool(led.value()), "uptime": time.ticks_ms()})
                except Exception as error:
                    send({"type": "error", "message": str(error)})
                buffer = bytearray()
            elif len(buffer) < 512:
                buffer.extend(data)
            else:
                buffer = bytearray()
                send({"type": "error", "message": "Command too long"})
    now = time.ticks_ms()
    if time.ticks_diff(now, last_heartbeat) >= 1000:
        send({"type": "heartbeat", "led": bool(led.value()), "uptime": now})
        last_heartbeat = now
    time.sleep_ms(1)
