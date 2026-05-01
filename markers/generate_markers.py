#!/usr/bin/env python3
"""
Generate 3 Vuforia-friendly AR markers for the 3DTVR coursework project.

Design principles for high Vuforia ratings (4-5 stars):
  - DENSE feature distribution — no large empty regions
  - High contrast (pure black on white)
  - Many sharp corners (rectangles, text, dots)
  - Asymmetric layout (no rotational ambiguity)
  - Distinct theme per marker (motherboard / GPU / PSU)
  - No periodic/repetitive patterns

Output: 3 PNG files at 1500x1500 px, 24-bit RGB (no alpha — Vuforia requirement).
Print at ~12cm wide for ideal AR tracking distance.
"""

import math
import os
import random
import string

from PIL import Image, ImageDraw, ImageFont

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
SIZE = 1500
MARGIN = 50

FONT_BOLD = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
FONT_REGULAR = "/System/Library/Fonts/Supplemental/Courier New.ttf"


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, size)


# ─── Background noise: feature-rich filler that boosts Vuforia rating ──────
def fill_background_noise(draw: ImageDraw.ImageDraw, rng: random.Random,
                           y_top: int, y_bot: int) -> None:
    """Random small features scattered across the area — dots, ticks, tiny text."""
    width = SIZE - 2 * MARGIN
    height = y_bot - y_top
    # Density: ~1 feature per 4000 px²
    n_features = (width * height) // 4000
    for _ in range(n_features):
        x = rng.randint(MARGIN + 30, SIZE - MARGIN - 30)
        y = rng.randint(y_top + 10, y_bot - 10)
        kind = rng.random()
        if kind < 0.30:  # tiny dot
            r = rng.randint(2, 4)
            draw.ellipse([x - r, y - r, x + r, y + r], fill="black")
        elif kind < 0.55:  # tiny rectangle
            w, h = rng.randint(4, 12), rng.randint(4, 12)
            draw.rectangle([x, y, x + w, y + h], fill="black")
        elif kind < 0.75:  # tick line
            ang = rng.choice([0, 45, 90, 135])
            length = rng.randint(8, 20)
            dx = int(length * math.cos(math.radians(ang)))
            dy = int(length * math.sin(math.radians(ang)))
            draw.line([x, y, x + dx, y + dy], fill="black", width=2)
        elif kind < 0.92:  # tiny text fragment
            chars = ''.join(rng.choices(string.ascii_uppercase + string.digits, k=rng.randint(2, 4)))
            font = load_font(rng.randint(14, 22), bold=rng.random() > 0.5)
            draw.text((x, y), chars, font=font, fill="black")
        else:  # small open circle
            r = rng.randint(5, 10)
            draw.ellipse([x - r, y - r, x + r, y + r], outline="black", width=2)


# ─── Corner brackets: 8 sharp 90° corners, 4 of them ─────────────────────────
def draw_corner_brackets(draw: ImageDraw.ImageDraw) -> None:
    L, T = 200, 26  # bracket length, thickness
    # top-left
    draw.rectangle([MARGIN, MARGIN, MARGIN + L, MARGIN + T], fill="black")
    draw.rectangle([MARGIN, MARGIN, MARGIN + T, MARGIN + L], fill="black")
    # top-right
    draw.rectangle([SIZE - MARGIN - L, MARGIN, SIZE - MARGIN, MARGIN + T], fill="black")
    draw.rectangle([SIZE - MARGIN - T, MARGIN, SIZE - MARGIN, MARGIN + L], fill="black")
    # bottom-left
    draw.rectangle([MARGIN, SIZE - MARGIN - T, MARGIN + L, SIZE - MARGIN], fill="black")
    draw.rectangle([MARGIN, SIZE - MARGIN - L, MARGIN + T, SIZE - MARGIN], fill="black")
    # bottom-right
    draw.rectangle([SIZE - MARGIN - L, SIZE - MARGIN - T, SIZE - MARGIN, SIZE - MARGIN], fill="black")
    draw.rectangle([SIZE - MARGIN - T, SIZE - MARGIN - L, SIZE - MARGIN, SIZE - MARGIN], fill="black")


# ─── Component-specific overlay patterns ────────────────────────────────────
def overlay_motherboard(draw: ImageDraw.ImageDraw, rng: random.Random,
                         y_top: int, y_bot: int) -> None:
    """Large chip silhouettes + PCB traces + solder pads — overlaid on noise background."""
    # Big main chip in upper-center (CPU socket)
    cx, cy = SIZE // 2, y_top + 280
    draw.rectangle([cx - 220, cy - 180, cx + 220, cy + 180], outline="black", width=8)
    draw.rectangle([cx - 200, cy - 160, cx + 200, cy + 160], outline="black", width=3)
    # CPU pins (grid of dots inside the socket)
    for px in range(cx - 180, cx + 180, 22):
        for py in range(cy - 140, cy + 140, 22):
            draw.ellipse([px - 3, py - 3, px + 3, py + 3], fill="black")
    # Socket text label
    font = load_font(36, bold=True)
    draw.text((cx - 80, cy - 20), "CPU", font=font, fill="white")
    draw.rectangle([cx - 90, cy - 25, cx + 90, cy + 25], outline="black", width=3)
    draw.text((cx - 80, cy - 20), "CPU", font=font, fill="black")
    # RAM slots (4 vertical rectangles on right)
    for i in range(4):
        rx = SIZE - MARGIN - 220 + i * 38
        draw.rectangle([rx, y_top + 80, rx + 22, y_top + 480], outline="black", width=3)
        # tick marks on the slot
        for ty in range(y_top + 100, y_top + 470, 12):
            draw.line([rx + 2, ty, rx + 20, ty], fill="black", width=1)
    # PCIe slots (long horizontal rectangles in lower area)
    for i in range(3):
        py = y_bot - 280 + i * 60
        draw.rectangle([MARGIN + 80, py, MARGIN + 700, py + 30], outline="black", width=3)
        # Pin teeth
        for tx in range(MARGIN + 90, MARGIN + 700, 14):
            draw.line([tx, py + 4, tx, py + 26], fill="black", width=1)
    # Capacitors (filled circles with rings)
    for _ in range(20):
        x = rng.randint(MARGIN + 100, SIZE - MARGIN - 100)
        y = rng.randint(y_top + 50, y_bot - 50)
        r_outer = rng.randint(18, 28)
        draw.ellipse([x - r_outer, y - r_outer, x + r_outer, y + r_outer], outline="black", width=4)
        draw.ellipse([x - 4, y - 4, x + 4, y + 4], fill="black")


def overlay_gpu(draw: ImageDraw.ImageDraw, rng: random.Random,
                 y_top: int, y_bot: int) -> None:
    """GPU shroud lines + fan + memory chips + display ports — dense overlay."""
    # Fan in upper-right (with detailed blades)
    fan_cx = SIZE - MARGIN - 320
    fan_cy = y_top + 320
    fan_r = 250
    draw.ellipse([fan_cx - fan_r, fan_cy - fan_r, fan_cx + fan_r, fan_cy + fan_r], outline="black", width=8)
    draw.ellipse([fan_cx - fan_r + 20, fan_cy - fan_r + 20, fan_cx + fan_r - 20, fan_cy + fan_r - 20],
                  outline="black", width=3)
    # Center hub
    draw.ellipse([fan_cx - 35, fan_cy - 35, fan_cx + 35, fan_cy + 35], fill="black")
    # 7 blades — asymmetric (prime number prevents rotational symmetry)
    for blade in range(7):
        angle_start = blade * (360 / 7)
        for r_step in range(45, fan_r - 25, 12):
            angle_offset = (r_step - 45) * 0.4  # twist for blade curve
            a = math.radians(angle_start + angle_offset)
            x = fan_cx + r_step * math.cos(a)
            y = fan_cy + r_step * math.sin(a)
            draw.ellipse([x - 5, y - 5, x + 5, y + 5], fill="black")
    # Heatsink fins (vertical lines on upper-left)
    fin_x = MARGIN + 80
    fin_top = y_top + 80
    fin_bot = y_top + 520
    for i in range(28):
        x = fin_x + i * 18
        draw.rectangle([x, fin_top, x + 6, fin_bot], fill="black")
        # Notches in the fins (irregular, asymmetric)
        if i % 3 == 0:
            ny = fin_top + (fin_bot - fin_top) // 3
            draw.rectangle([x - 3, ny, x + 9, ny + 30], fill="white")
            draw.rectangle([x - 3, ny, x + 9, ny + 30], outline="black", width=2)
    # Memory chips (8 small rectangles around the GPU)
    chip_positions = [
        (MARGIN + 120, y_bot - 380), (MARGIN + 240, y_bot - 380),
        (MARGIN + 360, y_bot - 380), (MARGIN + 480, y_bot - 380),
        (MARGIN + 600, y_bot - 380), (MARGIN + 720, y_bot - 380),
        (MARGIN + 840, y_bot - 380), (MARGIN + 960, y_bot - 380),
    ]
    font = load_font(20, bold=True)
    for cx, cy in chip_positions:
        draw.rectangle([cx, cy, cx + 100, cy + 70], outline="black", width=4)
        draw.rectangle([cx + 5, cy + 5, cx + 95, cy + 65], outline="black", width=1)
        draw.text((cx + 10, cy + 22), "GDDR6", font=font, fill="black")
    # PCIe connector teeth at bottom
    teeth_y = y_bot - 220
    for i in range(50):
        x = MARGIN + 80 + i * 22
        if i == 30:  # asymmetric notch
            continue
        draw.rectangle([x, teeth_y, x + 14, teeth_y + 32], fill="black")
    # Display ports at lower-right (HDMI + DP + DP)
    ports = [(SIZE - MARGIN - 360, y_bot - 130, 80, 30, "HDMI"),
             (SIZE - MARGIN - 250, y_bot - 130, 70, 30, "DP"),
             (SIZE - MARGIN - 160, y_bot - 130, 70, 30, "DP"),
             (SIZE - MARGIN - 70, y_bot - 130, 50, 30, "USB")]
    for px, py, pw, ph, label in ports:
        draw.rectangle([px, py, px + pw, py + ph], outline="black", width=4)
        draw.rectangle([px + 3, py + 3, px + pw - 3, py + ph - 3], fill="black")
        font_p = load_font(18, bold=True)
        draw.text((px, py - 24), label, font=font_p, fill="black")


def overlay_psu(draw: ImageDraw.ImageDraw, rng: random.Random,
                 y_top: int, y_bot: int) -> None:
    """PSU body + cable bundles + connector grids + warning labels."""
    # Main PSU body outline (large rectangle in center)
    body_x1, body_y1 = MARGIN + 200, y_top + 100
    body_x2, body_y2 = SIZE - MARGIN - 200, y_bot - 250
    draw.rectangle([body_x1, body_y1, body_x2, body_y2], outline="black", width=8)
    draw.rectangle([body_x1 + 15, body_y1 + 15, body_x2 - 15, body_y2 - 15], outline="black", width=3)
    # Big fan in PSU body (bottom-facing)
    fan_cx = (body_x1 + body_x2) // 2
    fan_cy = (body_y1 + body_y2) // 2
    fan_r = 280
    draw.ellipse([fan_cx - fan_r, fan_cy - fan_r, fan_cx + fan_r, fan_cy + fan_r], outline="black", width=6)
    draw.ellipse([fan_cx - fan_r + 30, fan_cy - fan_r + 30, fan_cx + fan_r - 30, fan_cy + fan_r - 30],
                  outline="black", width=2)
    # Fan grille (radial lines)
    for i in range(24):
        a = math.radians(i * 15)
        x1 = fan_cx + 35 * math.cos(a)
        y1 = fan_cy + 35 * math.sin(a)
        x2 = fan_cx + (fan_r - 20) * math.cos(a)
        y2 = fan_cy + (fan_r - 20) * math.sin(a)
        draw.line([x1, y1, x2, y2], fill="black", width=2)
    # Hub
    draw.ellipse([fan_cx - 30, fan_cy - 30, fan_cx + 30, fan_cy + 30], fill="black")
    # 5 fan blades (asymmetric)
    for blade in range(5):
        a = math.radians(blade * 72)
        for r_step in range(40, fan_r - 30, 10):
            offset = (r_step - 40) * 0.3
            ar = math.radians(blade * 72 + offset)
            x = fan_cx + r_step * math.cos(ar)
            y = fan_cy + r_step * math.sin(ar)
            draw.ellipse([x - 6, y - 6, x + 6, y + 6], fill="black")
    # 24-pin ATX connector (top-left)
    grid_x = MARGIN + 30
    grid_y = y_top + 80
    for row in range(2):
        for col in range(12):
            x = grid_x + col * 14
            y = grid_y + row * 22
            draw.rectangle([x, y, x + 10, y + 18], outline="black", width=2)
            draw.rectangle([x + 3, y + 4, x + 7, y + 14], fill="black")
    font = load_font(16, bold=True)
    draw.text((grid_x, grid_y - 22), "ATX 24-PIN", font=font, fill="black")
    # 8-pin EPS connector (top-right)
    grid2_x = SIZE - MARGIN - 180
    grid2_y = y_top + 80
    for row in range(2):
        for col in range(4):
            x = grid2_x + col * 36
            y = grid2_y + row * 26
            draw.rectangle([x, y, x + 30, y + 22], outline="black", width=2)
            draw.line([x + 6, y + 6, x + 24, y + 16], fill="black", width=2)
            draw.line([x + 24, y + 6, x + 6, y + 16], fill="black", width=2)
    draw.text((grid2_x, grid2_y - 22), "EPS 8-PIN", font=font, fill="black")
    # 6+2 PCIe connectors (bottom row, asymmetric — 3 of them)
    pcie_y = y_bot - 180
    for slot in range(3):
        sx = MARGIN + 150 + slot * 220
        for col in range(8):
            x = sx + col * 14
            draw.rectangle([x, pcie_y, x + 10, pcie_y + 18], outline="black", width=2)
            draw.rectangle([x + 3, pcie_y + 4, x + 7, pcie_y + 14], fill="black")
        draw.text((sx, pcie_y + 24), "PCIe", font=font, fill="black")
    # Voltage rails text (right side, prominent)
    font_big = load_font(54, bold=True)
    draw.text((SIZE - MARGIN - 200, y_bot - 130), "12V", font=font_big, fill="black")
    # Warning triangle (left)
    tri = [(MARGIN + 80, y_bot - 60), (MARGIN + 200, y_bot - 60), (MARGIN + 140, y_bot - 170)]
    draw.polygon(tri, outline="black", width=6)
    draw.line([MARGIN + 140, y_bot - 155, MARGIN + 140, y_bot - 110], fill="black", width=8)
    draw.ellipse([MARGIN + 134, y_bot - 100, MARGIN + 146, y_bot - 88], fill="black")
    # Watts badge (center-bottom)
    draw.rectangle([fan_cx - 90, body_y2 - 80, fan_cx + 90, body_y2 - 30], outline="black", width=4)
    draw.text((fan_cx - 78, body_y2 - 70), "850W", font=load_font(36, bold=True), fill="black")


# ─── Top + bottom labels ─────────────────────────────────────────────────────
def add_label(draw: ImageDraw.ImageDraw, top_text: str, bottom_text: str) -> None:
    font_top = load_font(80, bold=True)
    font_bottom = load_font(54, bold=True)
    bbox = draw.textbbox((0, 0), top_text, font=font_top)
    tw = bbox[2] - bbox[0]
    draw.text(((SIZE - tw) // 2, MARGIN + 4), top_text, font=font_top, fill="black")
    bbox = draw.textbbox((0, 0), bottom_text, font=font_bottom)
    tw = bbox[2] - bbox[0]
    draw.text(((SIZE - tw) // 2, SIZE - MARGIN - 64), bottom_text, font=font_bottom, fill="black")


# ─── Marker assembly ─────────────────────────────────────────────────────────
def make_marker(name: str, top_label: str, bottom_label: str, overlay_fn, seed: int) -> str:
    img = Image.new("RGB", (SIZE, SIZE), "white")
    draw = ImageDraw.Draw(img)
    rng = random.Random(seed)
    # The pattern area is between top label and bottom label
    pattern_top = MARGIN + 110
    pattern_bot = SIZE - MARGIN - 100
    # Layer 1: dense background noise (fills empty space with feature points)
    fill_background_noise(draw, rng, pattern_top, pattern_bot)
    # Layer 2: outer thin border
    draw.rectangle([MARGIN - 6, MARGIN - 6, SIZE - MARGIN + 6, SIZE - MARGIN + 6], outline="black", width=4)
    # Layer 3: corner brackets
    draw_corner_brackets(draw)
    # Layer 4: component-specific overlay (large recognizable shapes)
    overlay_fn(draw, rng, pattern_top, pattern_bot)
    # Layer 5: top + bottom text labels
    add_label(draw, top_label, bottom_label)
    out_path = os.path.join(OUTPUT_DIR, f"{name}.png")
    img.save(out_path, "PNG", optimize=True)
    return out_path


def main() -> None:
    markers = [
        ("motherboard", "MAIN BOARD", "MOTHERBOARD", overlay_motherboard, 42),
        ("gpu", "GRAPHICS", "GPU - VIDEO CARD", overlay_gpu, 1337),
        ("psu", "POWER", "PSU - POWER SUPPLY", overlay_psu, 99),
    ]
    print(f"Generating {len(markers)} dense Vuforia AR markers in {OUTPUT_DIR}\n")
    for name, top, bottom, fn, seed in markers:
        path = make_marker(name, top, bottom, fn, seed)
        size_kb = os.path.getsize(path) // 1024
        print(f"  {name:14s}  ->  {path}  ({size_kb} KB)")
    print("\nUpload to developer.vuforia.com -> Target Manager.")
    print("Use width = 0.2 (meters) — markers will print at ~12cm wide on A4.")


if __name__ == "__main__":
    main()
