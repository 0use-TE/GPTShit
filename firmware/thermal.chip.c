// Control object only. PID runs in the ESP32 firmware, never in this model.
#include "vendor/wokwi-api.h"

typedef struct {
  pin_t pwm, door, fault;
  double temperature, last_edge, high_nanos, last_tick;
  uint32_t high;
  uint16_t reading;
  uint8_t read_index;
  buffer_t display;
} thermal_t;
static thermal_t box;
static uint8_t pixels[200 * 100 * 4];

static void rect(int x, int y, int w, int h, uint8_t r, uint8_t g, uint8_t b) {
  for (int yy = y; yy < y + h && yy < 100; yy++)
    for (int xx = x; xx < x + w && xx < 200; xx++) {
      int i = (yy * 200 + xx) * 4;
      pixels[i] = r; pixels[i+1] = g; pixels[i+2] = b; pixels[i+3] = 255;
    }
}
static void digit(int n, int x, int y, int s) {
  static const uint16_t glyphs[] = {31599,9362,29671,29391,23497,31183,31215,29257,31727,31695};
  if (n < 0 || n > 9) return;
  uint16_t glyph = glyphs[n];
  for (int row=0; row<5; row++) for (int col=0; col<3; col++)
    if (glyph & (1 << (14-row*3-col))) rect(x+col*s,y+row*s,s-1,s-1,220,235,245);
}
static void draw(double duty, bool door) {
  rect(0,0,200,100,24,27,33);
  int t = (int)(box.temperature * 10 + 0.5);
  digit((t/100)%10,18,15,7); digit((t/10)%10,48,15,7);
  rect(75,43,4,4,220,235,245); digit(t%10,85,15,7);
  rect(122,15,4,4,117,190,255);
  rect(139,15,17,4,117,190,255); rect(139,15,4,32,117,190,255); rect(139,43,17,4,117,190,255);
  rect(18,67,164,8,51,57,65);
  rect(18,67,(int)(164*duty),8,239,146,60);
  rect(18,85,164,3,door?239:73,door?146:164,door?60:114);
  buffer_write(box.display,0,pixels,sizeof(pixels));
}
static void pwm_changed(void *data, pin_t pin, uint32_t value) {
  double now = get_sim_nanos_d();
  if (box.high) box.high_nanos += now - box.last_edge;
  box.last_edge = now; box.high = value != 0;
}
static void tick(void *data) {
  double now = get_sim_nanos_d();
  double elapsed = now - box.last_tick;
  if (box.high) box.high_nanos += now - box.last_edge;
  double duty = elapsed > 0 ? box.high_nanos / elapsed : 0;
  if (duty < 0) duty = 0; if (duty > 1) duty = 1;
  bool door = pin_read(box.door) != 0;
  // C=80 J/K, Pmax=250 W, closed loss=2.5 W/K, open loss=20 W/K.
  double loss = door ? 20.0 : 2.5;
  box.temperature += elapsed * 1e-9 * (250.0*duty - loss*(box.temperature-25.0)) / 80.0;
  box.last_tick = now; box.last_edge = now; box.high_nanos = 0;
  draw(duty,door);
}
static bool connected(void *data, uint32_t address, bool read) {
  box.read_index = 0;
  box.reading = pin_read(box.fault) ? 65535 : (uint16_t)(box.temperature * 100.0 + 0.5);
  return true;
}
static uint8_t read_byte(void *data) {
  return box.read_index++ == 0 ? box.reading >> 8 : box.reading & 255;
}
void chip_init(void) {
  box.temperature = 25.0;
  box.pwm = pin_init("PWM",INPUT_PULLDOWN);
  box.door = pin_init("DOOR",INPUT_PULLDOWN);
  box.fault = pin_init("FAULT",INPUT_PULLDOWN);
  uint32_t width,height;
  box.display = framebuffer_init(&width,&height);
  const pin_watch_config_t watch = {.edge=BOTH,.pin_change=pwm_changed};
  pin_watch(box.pwm,&watch);
  const i2c_config_t sensor = {
    .address=0x48,.scl=pin_init("SCL",INPUT_PULLUP),.sda=pin_init("SDA",INPUT_PULLUP),
    .connect=connected,.read=read_byte
  };
  i2c_init(&sensor);
  const timer_config_t timer = {.callback=tick};
  timer_start(timer_init(&timer),100000,true);
  draw(0,false);
}
