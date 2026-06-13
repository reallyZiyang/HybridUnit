# -*- coding: utf-8 -*-
from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

W, H = 1080, 1920
ROOT = Path(__file__).resolve().parents[1]
OUT = Path(__file__).resolve().parent
SPRITES = ROOT / "Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprites"


def font(size, bold=False):
    candidates = [
        "C:/Windows/Fonts/msyhbd.ttc",
        "C:/Windows/Fonts/msyh.ttc",
        "C:/Windows/Fonts/simhei.ttf",
        "C:/Windows/Fonts/simsun.ttc",
    ]
    if not bold:
        candidates = candidates[1:] + candidates[:1]
    for path in candidates:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            pass
    return ImageFont.load_default()


F = {
    "title": font(106, True),
    "h1": font(76, True),
    "h2": font(54, True),
    "h3": font(40, True),
    "body": font(32),
    "small": font(26),
    "tiny": font(22),
}


def asset(rel):
    path = SPRITES / rel
    if path.exists():
        return Image.open(path).convert("RGBA")
    return None


def cover(im, size):
    im = im.convert("RGBA")
    sw, sh = im.size
    tw, th = size
    scale = max(tw / sw, th / sh)
    nw, nh = int(sw * scale), int(sh * scale)
    im = im.resize((nw, nh), Image.Resampling.LANCZOS)
    return im.crop(((nw - tw) // 2, (nh - th) // 2, (nw + tw) // 2, (nh + th) // 2))


def paste(base, im, xy, size=None, opacity=1.0, anchor="tl"):
    if im is None:
        return
    im = im.convert("RGBA")
    if size:
        im = im.resize(size, Image.Resampling.LANCZOS)
    if opacity < 1:
        im.putalpha(im.getchannel("A").point(lambda p: int(p * opacity)))
    x, y = xy
    if anchor == "c":
        x -= im.width // 2
        y -= im.height // 2
    elif anchor == "bc":
        x -= im.width // 2
        y -= im.height
    base.alpha_composite(im, (int(x), int(y)))


def text(draw, xy, value, fnt, fill=(255, 255, 255), stroke=(35, 42, 56), sw=3, anchor=None):
    draw.text(xy, value, font=fnt, fill=fill, stroke_width=sw, stroke_fill=stroke, anchor=anchor)


def wrap_lines(draw, value, fnt, max_width, max_lines=3):
    lines = []
    current = ""
    for ch in value:
        test = current + ch
        if current and draw.textlength(test, font=fnt) > max_width:
            lines.append(current)
            current = ch
            if len(lines) >= max_lines:
                break
        else:
            current = test
    if current and len(lines) < max_lines:
        lines.append(current)
    return lines


def panel(draw, box, radius=28, fill=(255, 249, 232, 242), outline=(57, 71, 89, 255), width=5):
    x1, y1, x2, y2 = box
    draw.rounded_rectangle((x1 + 5, y1 + 10, x2 + 5, y2 + 10), radius, fill=(0, 0, 0, 70))
    draw.rounded_rectangle(box, radius, fill=fill, outline=outline, width=width)


def button(draw, box, label, color="orange"):
    palettes = {
        "orange": ((255, 183, 64), (239, 100, 38), (118, 62, 40)),
        "blue": ((91, 198, 255), (48, 112, 218), (42, 66, 122)),
        "green": ((106, 226, 95), (45, 159, 78), (47, 92, 61)),
        "red": ((255, 110, 88), (210, 60, 58), (116, 47, 48)),
    }
    c1, c2, outline = palettes[color]
    x1, y1, x2, y2 = box
    draw.rounded_rectangle((x1 + 5, y1 + 10, x2 + 5, y2 + 10), 34, fill=(0, 0, 0, 78))
    draw.rounded_rectangle(box, 34, fill=c2, outline=outline, width=6)
    draw.rounded_rectangle((x1 + 10, y1 + 9, x2 - 10, y1 + int((y2 - y1) * 0.52)), 25, fill=c1)
    text(draw, ((x1 + x2) // 2, (y1 + y2) // 2 - 3), label, F["h2"], fill=(255, 255, 245), stroke=(75, 45, 42), sw=4, anchor="mm")


def grass_background():
    bg = asset("Demo/Demo_Background/Background_01.png")
    if bg is not None:
        return ImageEnhance.Color(cover(bg, (W, H))).enhance(0.9)
    base = Image.new("RGBA", (W, H), (95, 142, 76, 255))
    draw = ImageDraw.Draw(base)
    for y in range(0, H, 160):
        draw.rectangle((0, y, W, y + 80), fill=(88, 132, 72, 255))
    return base


def draw_core(draw, cx, cy, scale=1.0, hp=0.75):
    r = int(88 * scale)
    draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(74, 195, 255), outline=(35, 62, 95), width=max(3, int(6 * scale)))
    draw.polygon(
        [(cx, cy - int(124 * scale)), (cx + int(74 * scale), cy), (cx, cy + int(112 * scale)), (cx - int(74 * scale), cy)],
        fill=(116, 232, 255),
        outline=(32, 69, 98),
    )
    draw.ellipse((cx - int(32 * scale), cy - int(38 * scale), cx + int(32 * scale), cy + int(34 * scale)), fill=(255, 255, 255, 135))
    bw = int(230 * scale)
    draw.rounded_rectangle((cx - bw // 2, cy + int(132 * scale), cx + bw // 2, cy + int(158 * scale)), 12, fill=(39, 46, 58), outline=(255, 255, 255, 80), width=2)
    draw.rounded_rectangle((cx - bw // 2 + 5, cy + int(137 * scale), cx - bw // 2 + 5 + int((bw - 10) * hp), cy + int(153 * scale)), 8, fill=(93, 226, 94))


def draw_hero(draw, cx, cy, color=(75, 210, 175), weapon="sword", scale=1.0):
    s = scale
    draw.ellipse((cx - 42 * s, cy - 70 * s, cx + 42 * s, cy + 14 * s), fill=color, outline=(22, 34, 40), width=max(2, int(5 * s)))
    draw.ellipse((cx - 31 * s, cy - 36 * s, cx + 31 * s, cy + 26 * s), fill=(245, 199, 158), outline=(22, 34, 40), width=max(2, int(4 * s)))
    draw.ellipse((cx - 18 * s, cy - 14 * s, cx - 7 * s, cy - 3 * s), fill=(20, 24, 30))
    draw.ellipse((cx + 8 * s, cy - 14 * s, cx + 19 * s, cy - 3 * s), fill=(20, 24, 30))
    draw.rounded_rectangle((cx - 34 * s, cy + 24 * s, cx + 34 * s, cy + 92 * s), int(16 * s), fill=(70, 98, 135), outline=(22, 34, 40), width=max(2, int(4 * s)))
    if weapon == "bow":
        draw.arc((cx + 28 * s, cy - 18 * s, cx + 100 * s, cy + 88 * s), -80, 80, fill=(132, 82, 37), width=max(3, int(7 * s)))
        draw.line((cx + 66 * s, cy - 4 * s, cx + 66 * s, cy + 76 * s), fill=(240, 244, 250), width=max(2, int(3 * s)))
    elif weapon == "staff":
        draw.line((cx + 48 * s, cy - 28 * s, cx + 86 * s, cy + 86 * s), fill=(113, 75, 42), width=max(3, int(8 * s)))
        draw.ellipse((cx + 68 * s, cy - 46 * s, cx + 104 * s, cy - 10 * s), fill=(118, 213, 255), outline=(22, 34, 40), width=max(2, int(3 * s)))
    else:
        draw.polygon([(cx + 44 * s, cy - 22 * s), (cx + 104 * s, cy + 24 * s), (cx + 86 * s, cy + 42 * s), (cx + 32 * s, cy - 4 * s)], fill=(214, 232, 240), outline=(25, 37, 44))
    draw.ellipse((cx - 48 * s, cy + 86 * s, cx + 48 * s, cy + 108 * s), fill=(0, 0, 0, 55))


def draw_monster(draw, cx, cy, scale=1.0):
    s = scale
    draw.ellipse((cx - 38 * s, cy - 38 * s, cx + 38 * s, cy + 38 * s), fill=(124, 226, 130), outline=(24, 42, 30), width=max(2, int(4 * s)))
    draw.polygon([(cx - 28 * s, cy - 28 * s), (cx - 48 * s, cy - 58 * s), (cx - 10 * s, cy - 38 * s)], fill=(124, 226, 130), outline=(24, 42, 30))
    draw.polygon([(cx + 28 * s, cy - 28 * s), (cx + 48 * s, cy - 58 * s), (cx + 10 * s, cy - 38 * s)], fill=(124, 226, 130), outline=(24, 42, 30))
    draw.ellipse((cx - 18 * s, cy - 12 * s, cx - 7 * s, cy), fill=(18, 26, 25))
    draw.ellipse((cx + 8 * s, cy - 12 * s, cx + 19 * s, cy), fill=(18, 26, 25))
    draw.arc((cx - 18 * s, cy + 3 * s, cx + 18 * s, cy + 25 * s), 10, 170, fill=(18, 26, 25), width=max(2, int(4 * s)))


def draw_skill_icon(draw, cx, cy, kind):
    draw.ellipse((cx - 72, cy - 72, cx + 72, cy + 72), fill=(45, 54, 72), outline=(255, 255, 255, 120), width=5)
    if kind == "fire":
        draw.polygon([(cx, cy - 52), (cx + 42, cy + 10), (cx + 10, cy + 58), (cx - 36, cy + 24)], fill=(255, 100, 52), outline=(92, 42, 30))
        draw.polygon([(cx + 2, cy - 22), (cx + 22, cy + 18), (cx, cy + 42), (cx - 18, cy + 12)], fill=(255, 219, 82))
    elif kind == "rain":
        for i in range(-2, 3):
            x = cx + i * 22
            draw.line((x, cy - 48, x - 18, cy + 44), fill=(94, 205, 255), width=8)
            draw.polygon([(x - 18, cy + 44), (x - 30, cy + 24), (x - 6, cy + 28)], fill=(94, 205, 255))
    else:
        draw.ellipse((cx - 42, cy - 42, cx + 42, cy + 42), fill=(124, 223, 255), outline=(255, 255, 255), width=4)
        draw.line((cx - 58, cy, cx + 58, cy), fill=(255, 255, 255), width=6)
        draw.line((cx, cy - 58, cx, cy + 58), fill=(255, 255, 255), width=6)


def main_menu():
    base = grass_background()
    draw = ImageDraw.Draw(base)
    paste(base, asset("Components/Popup/Common_Popup_Glow.png"), (540, 420), (900, 900), 0.48, "c")
    text(draw, (540, 190), "核心守卫", F["title"], fill=(255, 241, 99), stroke=(43, 61, 78), sw=8, anchor="mm")
    text(draw, (540, 286), "肉鸽自动战斗原型", F["h3"], fill=(255, 255, 255), stroke=(39, 56, 70), sw=4, anchor="mm")

    panel(draw, (120, 430, 960, 1190), 38, fill=(255, 250, 230, 225))
    draw_core(draw, 540, 720, 1.55, 1.0)
    draw_hero(draw, 302, 1000, (76, 213, 180), "sword", 1.25)
    draw_hero(draw, 540, 1040, (95, 183, 255), "staff", 1.25)
    draw_hero(draw, 760, 1000, (255, 213, 95), "bow", 1.25)
    text(draw, (540, 1150), "守住核心，升级英雄，击退怪潮", F["body"], fill=(65, 71, 82), stroke=(255, 255, 255), sw=1, anchor="mm")

    button(draw, (190, 1380, 890, 1548), "开始战斗", "orange")
    text(draw, (540, 1628), "当前原型只包含战斗闭环", F["small"], fill=(255, 255, 255), stroke=(42, 52, 66), sw=3, anchor="mm")
    base.save(OUT / "01_main_menu.png")


def battle_hud():
    base = grass_background()
    draw = ImageDraw.Draw(base)
    for y in range(420, 1280, 92):
        for x in range(80 + (y // 92 % 2) * 42, 1030, 84):
            draw_monster(draw, x, y, 0.62)
    for x, y, color, weapon in [
        (356, 1320, (76, 213, 180), "sword"),
        (540, 1372, (95, 183, 255), "staff"),
        (724, 1320, (255, 213, 95), "bow"),
    ]:
        draw_hero(draw, x, y, color, weapon, 1.0)
    draw_core(draw, 540, 1582, 1.0, 0.68)

    panel(draw, (28, 24, 1052, 184), 28, fill=(32, 40, 55, 234), outline=(255, 255, 255, 90), width=3)
    text(draw, (74, 70), "核心", F["small"], fill=(255, 244, 126), stroke=(0, 0, 0), sw=2, anchor="lm")
    draw.rounded_rectangle((160, 48, 590, 88), 18, fill=(38, 46, 60))
    draw.rounded_rectangle((168, 56, 456, 80), 12, fill=(87, 225, 92))
    text(draw, (606, 68), "1840 / 2400", F["small"], fill=(255, 255, 255), stroke=(0, 0, 0), sw=2, anchor="lm")
    text(draw, (74, 132), "Lv. 5", F["small"], fill=(106, 211, 255), stroke=(0, 0, 0), sw=2, anchor="lm")
    draw.rounded_rectangle((160, 110, 728, 150), 18, fill=(38, 46, 60))
    draw.rounded_rectangle((168, 118, 536, 142), 12, fill=(75, 184, 255))
    text(draw, (744, 130), "经验 64%", F["small"], fill=(255, 255, 255), stroke=(0, 0, 0), sw=2, anchor="lm")
    draw.ellipse((942, 54, 1022, 134), fill=(255, 255, 255, 225), outline=(48, 60, 78), width=4)
    draw.ellipse((968, 78, 996, 106), outline=(55, 62, 76), width=5)
    for angle in range(0, 360, 45):
        import math
        rad = math.radians(angle)
        x1 = 982 + int(math.cos(rad) * 20)
        y1 = 92 + int(math.sin(rad) * 20)
        x2 = 982 + int(math.cos(rad) * 29)
        y2 = 92 + int(math.sin(rad) * 29)
        draw.line((x1, y1, x2, y2), fill=(55, 62, 76), width=5)

    panel(draw, (72, 204, 1008, 314), 24, fill=(255, 248, 218, 220))
    text(draw, (540, 258), "自动战斗中  ·  击败怪物获得经验并触发三选一", F["small"], fill=(63, 70, 82), stroke=(255, 255, 255), sw=1, anchor="mm")
    base.save(OUT / "02_battle_hud.png")


def upgrade_select():
    base = grass_background().filter(ImageFilter.GaussianBlur(3))
    base.alpha_composite(Image.new("RGBA", (W, H), (18, 24, 38, 164)))
    draw = ImageDraw.Draw(base)
    paste(base, asset("Components/Popup/Common_Popup_Glow.png"), (540, 360), (860, 860), 0.5, "c")
    text(draw, (540, 170), "选择强化", F["h1"], fill=(255, 241, 99), stroke=(41, 48, 66), sw=7, anchor="mm")
    text(draw, (540, 244), "三选一，选择后继续战斗", F["body"], fill=(255, 255, 255), stroke=(30, 34, 48), sw=3, anchor="mm")

    cards = [
        ("获得新英雄", "守护骑士加入", "新增一名近战英雄，优先保护核心。", "hero", "orange"),
        ("英雄获得技能", "弓手学会箭雨", "弓手获得范围箭雨，对密集怪群更强。", "rain", "blue"),
        ("英雄升级技能", "火球 Lv.2 → Lv.3", "火球伤害提升，并扩大爆炸范围。", "fire", "green"),
    ]
    xs = [88, 390, 692]
    for i, (tag, title, desc, icon, color) in enumerate(cards):
        x = xs[i]
        panel(draw, (x, 380, x + 300, 1250), 30, fill=(255, 249, 230, 246))
        draw.rounded_rectangle((x + 30, 412, x + 270, 470), 22, fill=(48, 58, 77))
        text(draw, (x + 150, 440), tag, F["small"], fill=(255, 231, 99), stroke=(0, 0, 0), sw=2, anchor="mm")
        if icon == "hero":
            draw_hero(draw, x + 150, 670, (76, 213, 180), "sword", 1.35)
        else:
            draw_skill_icon(draw, x + 150, 650, icon)
        title_lines = wrap_lines(draw, title, F["body"], 236, 2)
        for li, line in enumerate(title_lines):
            text(draw, (x + 150, 812 + li * 42), line, F["body"], fill=(61, 66, 78), stroke=(255, 255, 255), sw=1, anchor="mm")
        for li, line in enumerate(wrap_lines(draw, desc, F["small"], 238, 4)):
            text(draw, (x + 150, 912 + li * 42), line, F["small"], fill=(78, 84, 96), stroke=(255, 255, 255), sw=1, anchor="mm")
        button(draw, (x + 44, 1120, x + 256, 1210), "选择", color)
    base.save(OUT / "03_rogue_upgrade_select.png")


def result_stats():
    bg = asset("Demo/Demo_Background/Background_03.png")
    base = cover(bg, (W, H)) if bg is not None else grass_background()
    base = ImageEnhance.Brightness(base).enhance(0.88)
    draw = ImageDraw.Draw(base)
    paste(base, asset("Components/Popup/Common_Popup_Glow.png"), (540, 300), (880, 880), 0.52, "c")
    text(draw, (540, 150), "战斗结算", F["h1"], fill=(255, 241, 99), stroke=(42, 49, 68), sw=7, anchor="mm")
    panel(draw, (86, 270, 994, 1510), 36, fill=(255, 249, 232, 246))
    draw.rounded_rectangle((300, 330, 780, 430), 38, fill=(255, 189, 60), outline=(111, 73, 42), width=5)
    text(draw, (540, 378), "胜利", F["h2"], fill=(255, 255, 247), stroke=(83, 52, 38), sw=4, anchor="mm")
    text(draw, (540, 470), "失败状态：核心血量归零时显示", F["small"], fill=(151, 66, 64), stroke=(255, 255, 255), sw=1, anchor="mm")

    stats = [
        ("生存时间", "05:00"),
        ("击杀数量", "1,286"),
        ("最高等级", "Lv. 9"),
        ("核心剩余", "68%"),
        ("总伤害", "248K"),
        ("获得经验", "+320"),
    ]
    for i, (label, value) in enumerate(stats):
        x = 146 + (i % 2) * 414
        y = 570 + (i // 2) * 168
        draw.rounded_rectangle((x, y, x + 374, y + 116), 24, fill=(255, 255, 255, 235), outline=(203, 211, 218), width=3)
        text(draw, (x + 32, y + 36), label, F["small"], fill=(77, 84, 98), stroke=(255, 255, 255), sw=1, anchor="lm")
        text(draw, (x + 342, y + 76), value, F["h3"], fill=(47, 60, 82), stroke=(255, 255, 255), sw=1, anchor="rm")

    text(draw, (146, 1128), "英雄表现", F["h3"], fill=(62, 70, 86), stroke=(255, 255, 255), sw=1, anchor="lm")
    for i, (name, pct, color) in enumerate([("守护骑士", "42%", (84, 205, 146)), ("弓手", "35%", (83, 180, 255)), ("法师", "23%", (255, 122, 82))]):
        y = 1190 + i * 84
        text(draw, (152, y + 31), name, F["small"], fill=(65, 72, 84), stroke=(255, 255, 255), sw=1, anchor="lm")
        draw.rounded_rectangle((350, y + 15, 830, y + 47), 14, fill=(220, 225, 230))
        draw.rounded_rectangle((350, y + 15, 350 + int(480 * int(pct[:-1]) / 50), y + 47), 14, fill=color)
        text(draw, (884, y + 31), pct, F["small"], fill=(65, 72, 84), stroke=(255, 255, 255), sw=1, anchor="mm")

    button(draw, (190, 1640, 890, 1798), "返回主界面", "blue")
    base.save(OUT / "04_battle_result_stats.png")


def overview():
    names = ["01_main_menu.png", "02_battle_hud.png", "03_rogue_upgrade_select.png", "04_battle_result_stats.png"]
    sheet = Image.new("RGBA", (W * 2, H * 2), (235, 238, 240, 255))
    for i, name in enumerate(names):
        img = Image.open(OUT / name).convert("RGBA")
        sheet.alpha_composite(img, ((i % 2) * W, (i // 2) * H))
    sheet.resize((W, H), Image.Resampling.LANCZOS).save(OUT / "00_overview.png")


if __name__ == "__main__":
    main_menu()
    battle_hud()
    upgrade_select()
    result_stats()
    overview()
