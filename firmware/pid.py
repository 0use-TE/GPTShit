"""Discrete PID with derivative on measurement and conditional integration."""
class PID:
    def __init__(self, kp=8.0, ki=0.7, kd=4.0):
        self.configure(kp, ki, kd)
        self.reset()

    def configure(self, kp, ki, kd):
        self.kp, self.ki, self.kd = kp, ki, kd

    def reset(self):
        self.integral = 0.0
        self.previous = None
        self.derivative = 0.0

    def update(self, target, measured, dt):
        error = target - measured
        rate = 0.0 if self.previous is None else (measured - self.previous) / dt
        self.previous = measured
        self.derivative += 0.25 * (rate - self.derivative)
        candidate = self.integral + self.ki * error * dt
        trial = self.kp * error + candidate - self.kd * self.derivative
        if 0.0 <= trial <= 100.0 or (trial > 100.0 and error < 0) or (trial < 0 and error > 0):
            self.integral = max(-100.0, min(100.0, candidate))
        output = self.kp * error + self.integral - self.kd * self.derivative
        return max(0.0, min(100.0, output))
