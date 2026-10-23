"""Diagnostic rasterization of exported native triangles; never edits source PNGs."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
output = root / '.local/visualharness/companion-behavior'
samples = json.loads((output / 'wink-blink.json').read_text())
textures = {i: np.asarray(Image.open(root / 'assets' / name).convert('RGBA'), dtype=float) / 255
    for i, name in [(2, 'wink-motion.png'), (3, 'companion-house.png'), (4, 'companion-grab-hand.png')]}
def render(sample, width, height):
    target = np.zeros((height, width, 4), dtype=float)
    vertices = sample['vertices']
    for triangle in sample['triangles']:
        points = [vertices[i] for i in triangle['indices']]
        p = np.array([v['p'] for v in points])
        left, top = np.maximum(0, np.floor(p.min(axis=0)).astype(int))
        right, bottom = np.minimum([width, height], np.ceil(p.max(axis=0)).astype(int))
        if left >= right or top >= bottom:
            continue
        x, y = np.meshgrid(np.arange(left, right) + .5, np.arange(top, bottom) + .5)
        area = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(area) < 1e-8:
            continue
        a = ((p[1, 1] - p[2, 1]) * (x - p[2, 0]) + (p[2, 0] - p[1, 0]) * (y - p[2, 1])) / area
        b = ((p[2, 1] - p[0, 1]) * (x - p[2, 0]) + (p[0, 0] - p[2, 0]) * (y - p[2, 1])) / area
        c = 1 - a - b
        mask = (a >= 0) & (b >= 0) & (c >= 0)
        weights = np.stack([a, b, c], axis=-1)
        colors = np.array([[(v['color'] >> shift) & 255 for shift in [0, 8, 16, 24]] for v in points], dtype=float) / 255
        source = weights @ colors
        if triangle['texture'] in textures:
            sheet = textures[triangle['texture']]
            uv = weights @ np.array([v['uv'] for v in points])
            tx = np.clip((uv[..., 0] * sheet.shape[1]).astype(int), 0, sheet.shape[1] - 1)
            ty = np.clip((uv[..., 1] * sheet.shape[0]).astype(int), 0, sheet.shape[0] - 1)
            source *= sheet[ty, tx]
        alpha = source[..., 3:4] * mask[..., None]
        destination = target[top:bottom, left:right]
        destination[..., :3] = source[..., :3] * alpha + destination[..., :3] * (1 - alpha)
        destination[..., 3:4] = alpha + destination[..., 3:4] * (1 - alpha)
    # The accumulation above is premultiplied; convert for PIL alpha compositing.
    target[..., :3] /= np.maximum(target[..., 3:4], 1e-8)
    return Image.fromarray(np.uint8(np.clip(target, 0, 1) * 255))

canvas = Image.new('RGBA', (3 * 224, 2 * 244), (25, 30, 38, 255))
for index, sample in enumerate(samples):
    rendered = render(sample, 192, 192)
    x, y = index % 3 * 224, index // 3 * 244
    canvas.alpha_composite(rendered, (x + 16, y + 24))
    ImageDraw.Draw(canvas).text((x + 12, y + 220), f"closed {sample['closure']:.1f}; mirror {sample['left']}", fill='white')
canvas.save(output / 'wink-blink-native.png')
print('Rendered six native eye overlays on unchanged source art.')

canvas = Image.new('RGBA', (640, 880), (25, 30, 38, 255))
for i, sample in enumerate(json.loads((output / 'home-hand.json').read_text())):
    canvas.alpha_composite(render(sample, 640, 440), (0, i * 440))
    ImageDraw.Draw(canvas).text((16, i * 440 + 16), 'Native home / floating companion / open hover hand: ' + ('right' if sample['rightSide'] else 'left'), fill='white')
canvas.save(output / 'home-hand-native.png')
