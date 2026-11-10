"""ESP32 thermostat. Sensor, PID, PWM and safety all execute on the MCU."""
from machine import Pin, UART, I2C, PWM
import time
import ujson
from pid import PID

heater = PWM(Pin(23), freq=500, duty=0)
door_pin = Pin(19, Pin.OUT, value=0)
fault_pin = Pin(18, Pin.OUT, value=0)
sensor = I2C(0, scl=Pin(22), sda=Pin(21), freq=100000)
uart = UART(2, baudrate=115200, tx=17, rx=16)
pid = PID()
target = 50.0
temperature = 25.0
output = 0.0
enabled = False
fault = ""
door_until = None
buffer = bytearray()


def send(message):
    uart.write(ujson.dumps(message) + "\n")


def snapshot(kind="telemetry", identifier=None):
    result = {"type": kind, "temperature": round(temperature, 2), "target": target,
              "output": round(output, 2), "enabled": enabled, "door": bool(door_pin.value()),
              "fault": fault, "uptime": time.ticks_ms(), "kp": pid.kp, "ki": pid.ki, "kd": pid.kd}
    if identifier is not None:
        result["id"] = identifier
    return result


def number(command, name, minimum, maximum):
    value = float(command[name])
    if not minimum <= value <= maximum:
        raise ValueError(name + " out of range")
    return value


def read_temperature():
    raw = sensor.readfrom(0x48, 2)
    value = (raw[0] << 8) | raw[1]
    if value == 65535 or value > 12000:
        raise ValueError("Sensor fault")
    return value / 100.0


def stop():
    global enabled, output
    enabled = False
    output = 0.0
    heater.duty(0)
    pid.reset()


def handle(command):
    global target, enabled, fault, door_until
    op = command.get("command")
    if op == "configure":
        new_target = number(command, "target", 30, 75)
        kp = number(command, "kp", 0, 50)
        ki = number(command, "ki", 0, 10)
        kd = number(command, "kd", 0, 50)
        target = new_target
        pid.configure(kp, ki, kd)
        pid.reset()
    elif op == "enable":
        if command.get("enabled"):
            value = read_temperature()
            if value >= 80:
                raise ValueError("Overtemperature")
            fault = ""
            pid.reset()
            enabled = True
        else:
            stop()
    elif op == "door":
        seconds = number(command, "duration", 0.5, 30)
        door_pin.value(1)
        door_until = time.ticks_add(time.ticks_ms(), int(seconds * 1000))
    elif op == "fault":
        fault_pin.value(1 if command.get("value") else 0)
    elif op != "ping":
        raise ValueError("Unknown command")
    send(snapshot("ack", command.get("id", "")))


send(snapshot("ready"))
last_control = time.ticks_ms()
last_report = last_control
while True:
    now = time.ticks_ms()
    if door_until is not None and time.ticks_diff(now, door_until) >= 0:
        door_pin.value(0)
        door_until = None
    if uart.any():
        data = uart.read(min(uart.any(), 256))
        if data:
            for byte in data:
                if byte == 10:
                    try:
                        handle(ujson.loads(buffer.decode()))
                    except Exception as error:
                        send({"type": "error", "id": "", "message": str(error)})
                    buffer = bytearray()
                elif len(buffer) < 1024:
                    buffer.append(byte)
                else:
                    buffer = bytearray()
                    send({"type": "error", "message": "Command too long"})
    elapsed = time.ticks_diff(now, last_control)
    if elapsed >= 100:
        try:
            temperature = read_temperature()
            if temperature >= 80.0:
                fault = "Overtemperature"
                stop()
            elif enabled:
                output = pid.update(target, temperature, elapsed / 1000.0)
                heater.duty(int(output * 1023 / 100))
        except Exception:
            fault = "Sensor fault"
            stop()
        last_control = now
    if time.ticks_diff(now, last_report) >= 500:
        send(snapshot())
        last_report = now
    time.sleep_ms(1)
