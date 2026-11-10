import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('pid', Path(__file__).parents[1] / 'firmware/pid.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ControllerTests(unittest.TestCase):
    def test_setpoint_and_door_recovery(self):
        controller = module.PID()
        temperature = 25.0
        trace = []
        for tick in range(1600):
            output = controller.update(50, temperature, 0.1)
            loss = 20 if 800 <= tick < 850 else 2.5
            temperature += 0.1 * (2.5 * output - loss * (temperature - 25)) / 80
            trace.append(temperature)
            self.assertTrue(0 <= output <= 100)
        self.assertLess(abs(trace[790] - 50), 0.8)
        self.assertLess(min(trace[800:860]), 45)
        self.assertLess(abs(trace[-1] - 50), 0.8)
        self.assertLess(max(trace), 54)

    def test_saturation_does_not_accumulate_integral(self):
        controller = module.PID()
        for _ in range(1000):
            self.assertEqual(controller.update(75, 25, 0.1), 100)
        self.assertEqual(controller.integral, 0)
        self.assertEqual(controller.update(30, 70, 0.1), 0)
        controller.reset()
        self.assertIsNone(controller.previous)


if __name__ == '__main__':
    unittest.main()
