"""Execute the actual MCU program with a deterministic clock and failing sensor."""
import json
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

firmware = Path(__file__).parents[1] / 'firmware'
sys.path.insert(0, str(firmware))


class EndSimulation(Exception):
    pass


def run_controller(failure):
    clock = SimpleNamespace(now=0)
    outputs = []

    class Pin:
        OUT = 1

        def __init__(self, number, mode=None, value=0):
            self.state = value

        def value(self, value=None):
            if value is not None:
                self.state = value
            return self.state

    class PWM:
        def __init__(self, pin, freq, duty):
            outputs.append(duty)

        def duty(self, value):
            outputs.append(value)

    class I2C:
        def __init__(self, *args, **kwargs):
            pass

        def readfrom(self, address, count):
            if clock.now >= 300 and failure == 'sensor':
                raise OSError('sensor disconnected')
            temperature = 8500 if clock.now >= 300 and failure == 'overheat' else 2500
            return temperature.to_bytes(2, 'big')

    class UART:
        def __init__(self, *args, **kwargs):
            self.input = bytearray(b'{"command":"enable","enabled":true,"id":"1"}\n')

        def any(self):
            return len(self.input)

        def read(self, size):
            value = self.input[:size]
            del self.input[:size]
            return value

        def write(self, data):
            pass

    def sleep(milliseconds):
        clock.now += milliseconds
        if clock.now >= 800:
            raise EndSimulation()

    fake_time = SimpleNamespace(ticks_ms=lambda: clock.now, ticks_diff=lambda a, b: a - b,
                                ticks_add=lambda a, b: a + b, sleep_ms=sleep)
    hardware = SimpleNamespace(Pin=Pin, PWM=PWM, I2C=I2C, UART=UART)
    namespace = {}
    with patch.dict(sys.modules, {'machine': hardware, 'time': fake_time, 'ujson': json}):
        try:
            exec(compile((firmware / 'main.py').read_text(encoding='utf-8'), 'main.py', 'exec'), namespace)
        except EndSimulation:
            pass
    return namespace, outputs


class SafetyTests(unittest.TestCase):
    def test_sensor_disconnect_cuts_previously_active_heater(self):
        program, outputs = run_controller('sensor')
        self.assertGreater(max(outputs), 0)
        self.assertEqual(outputs[-1], 0)
        self.assertFalse(program['enabled'])
        self.assertEqual(program['fault'], 'Sensor fault')

    def test_overtemperature_cuts_previously_active_heater(self):
        program, outputs = run_controller('overheat')
        self.assertGreater(max(outputs), 0)
        self.assertEqual(outputs[-1], 0)
        self.assertFalse(program['enabled'])
        self.assertEqual(program['fault'], 'Overtemperature')

    def test_invalid_parameter_update_is_atomic(self):
        program, _ = run_controller('healthy')
        with self.assertRaises(ValueError):
            program['handle']({'command': 'configure', 'target': 65, 'kp': 8, 'ki': -1, 'kd': 4})
        self.assertEqual(program['target'], 50)
        self.assertEqual(program['pid'].ki, 0.7)


if __name__ == '__main__':
    unittest.main()
