"""Builds the Chicken Chamber, the April Fools version of Oryx's Chamber.

    python tools/build_chicken_chamber.py

Writes source/Shared/resources/worlds/Dungeons/Chicken Chamber.jm from scratch. The Chicken
Chamber was RotMG's April Fools joke on Oryx 1: the Giant Oryx Chicken roosts in a copy of his
Chamber (the Wine Cellar Incantation already works there, `onlyInArea="Oryx's Chamber,Chicken
Chamber"`). Its objects (the Giant Oryx Chicken, the Oryx Chicken Minion and the Egg) are in the
prod XML, OryxChickenChamber.xml; their behaviours are in BehaviorDb.ChickenChamber.cs. From south
to north:

- the vestibule, where players arrive (Spawn region) with a Nexus Portal to leave by;
- the Coop, a wooden corridor with hay bales, chickens and eggs that hatch when players come near;
- the Henhouse, a wider room with more of them;
- the Chamber, a round room in Oryx's blue tiles with four pillars and the Giant Oryx Chicken
  asleep at its centre.

The output is generated: change this script and run it again rather than editing the map.
"""
import base64
import json
import math
import os
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "source", "Shared", "resources", "worlds", "Dungeons", "Chicken Chamber.jm")

W, H = 64, 116
CX = 32                     # the map's spine: every room is centred on this column

CHAMBER_C = (CX, 26)
CHAMBER_R = 18.5
HENHOUSE = (19, 52, 45, 70)  # x0, y0, x1, y1 of the floor (inclusive)
COOP = (CX - 4, 71, CX + 4, 96)
VESTIBULE = (25, 97, 39, 107)

QUAD = "Blue Quad"
CLOSED = "Blue Closed"
WOOD = "Broken Wood Square"
WALL = "Blue Wall"
TORCH = "Blue Torch Wall"
HAY = "Arena Hay Stack"

BOSS = "TestChicken 2"       # Giant Oryx Chicken
CHICKEN = "TestChicken Small"  # Oryx Chicken Minion
EGG = "Test Egg"             # Egg


class Grid:
    def __init__(self):
        self.ground = [[None] * W for _ in range(H)]
        self.obj = [[None] * W for _ in range(H)]
        self.region = [[None] * W for _ in range(H)]

    def floor(self, x, y, ground):
        self.ground[y][x] = ground

    def is_floor(self, x, y):
        return 0 <= x < W and 0 <= y < H and self.ground[y][x] is not None

    def place(self, x, y, obj):
        assert self.is_floor(x, y), (obj, x, y)
        assert self.obj[y][x] is None, (obj, x, y, self.obj[y][x])
        self.obj[y][x] = obj


def rect(g, x0, y0, x1, y1, ground):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            g.floor(x, y, ground(x, y) if callable(ground) else ground)


def build():
    g = Grid()
    ax, ay = CHAMBER_C

    # the Chamber: Oryx's blue quads, a ring of closed tiles at the edge and round the roost
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - ax, y - ay)
            if d <= CHAMBER_R:
                g.floor(x, y, CLOSED if d > CHAMBER_R - 1.5 or 3.5 < d <= 4.5 else QUAD)

    # passage from the Henhouse into the Chamber
    rect(g, CX - 3, ay + int(CHAMBER_R) - 1, CX + 3, HENHOUSE[1] - 1, CLOSED)

    # the Henhouse: wooden floor with a blue border
    x0, y0, x1, y1 = HENHOUSE
    rect(g, x0, y0, x1, y1, lambda x, y: CLOSED if x in (x0, x1) or y in (y0, y1) else WOOD)

    # the Coop
    rect(g, *COOP, WOOD)
    # four pens off the Coop, two each side
    for py in (76, 87):
        rect(g, COOP[0] - 5, py, COOP[0] - 1, py + 5, WOOD)
        rect(g, COOP[2] + 1, py, COOP[2] + 5, py + 5, WOOD)

    # the vestibule
    vx0, vy0, vx1, vy1 = VESTIBULE
    rect(g, vx0, vy0, vx1, vy1, lambda x, y: CLOSED if x in (vx0, vx1) or y in (vy0, vy1) else QUAD)

    # walls: two tiles thick around every floor tile, a torch every few tiles of the face
    walls = {}
    for y in range(H):
        for x in range(W):
            if g.is_floor(x, y):
                continue
            near = [(dx, dy) for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2)
                    if g.is_floor(x + dx, y + dy)]
            if not near:
                continue
            face = any(abs(dx) + abs(dy) == 1 for dx, dy in near)
            walls[(x, y)] = TORCH if face and (x * 7 + y * 3) % 11 == 0 else WALL
    for (x, y), w in walls.items():
        g.obj[y][x] = w

    # --- the Chamber --------------------------------------------------------------------------
    g.place(ax, ay, BOSS)
    # four pillars to hide behind, on the diagonals
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        for ox, oy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            g.place(ax + dx * 9 + ox - (dx < 0), ay + dy * 9 + oy - (dy < 0), WALL)
    # hay round the walls
    for k in range(16):
        a = 2 * math.pi * k / 16 + math.pi / 16
        hx, hy = round(ax + math.cos(a) * (CHAMBER_R - 3)), round(ay + math.sin(a) * (CHAMBER_R - 3))
        if g.is_floor(hx, hy) and g.obj[hy][hx] is None and abs(hx - CX) > 3:
            g.place(hx, hy, HAY)

    # --- the Henhouse -------------------------------------------------------------------------
    for x, y in ((x0 + 4, y0 + 4), (x1 - 4, y0 + 4), (x0 + 4, y1 - 4), (x1 - 4, y1 - 4),
                 (CX, y0 + 9)):
        g.place(x, y, CHICKEN)
    for x, y in ((x0 + 8, y0 + 3), (x1 - 8, y0 + 3), (x0 + 3, y0 + 9), (x1 - 3, y0 + 9),
                 (x0 + 8, y1 - 3), (x1 - 8, y1 - 3)):
        g.place(x, y, EGG)
    for x in range(x0 + 2, x1 - 1, 3):
        for y in (y0 + 1, y1 - 1):
            if abs(x - CX) > 3 and g.obj[y][x] is None:
                g.place(x, y, HAY)
    for y in range(y0 + 5, y1 - 4, 4):
        for x in (x0 + 1, x1 - 1):
            g.place(x, y, HAY)

    # --- the Coop -----------------------------------------------------------------------------
    for i, py in enumerate((76, 87)):
        for px, side in ((COOP[0] - 3, -1), (COOP[2] + 3, 1)):
            g.place(px, py + 2, CHICKEN if (i + (side > 0)) % 2 == 0 else EGG)
            g.place(px, py + 4, EGG)
            g.place(px + side * 2, py, HAY)
            g.place(px + side * 2, py + 5, HAY)
    for y in (79, 84, 90):
        g.place(CX + (2 if y % 2 else -2), y, EGG)
    g.place(CX, 82, CHICKEN)

    # --- the vestibule ------------------------------------------------------------------------
    for y in range(vy0 + 2, vy1 - 1):
        for x in range(vx0 + 2, vx1 - 1):
            if abs(x - CX) >= 2:
                g.region[y][x] = "Spawn"
    g.place(CX, vy1 - 1, "Nexus Portal")
    for x in (vx0 + 1, vx1 - 1):
        for y in (vy0 + 1, vy1 - 1):
            g.place(x, y, HAY)
    return g


def save(g):
    entries, index = [], {}
    out = bytearray()
    for y in range(H):
        for x in range(W):
            t = {}
            if g.ground[y][x]:
                t["ground"] = g.ground[y][x]
            if g.obj[y][x]:
                t["objs"] = [{"id": g.obj[y][x]}]
            if g.region[y][x]:
                t["regions"] = [{"id": g.region[y][x]}]
            key = json.dumps(t, sort_keys=True)
            if key not in index:
                index[key] = len(entries)
                entries.append(t)
            i = index[key]
            out += bytes((i >> 8, i & 0xff))
    jm = {"width": W, "height": H, "dict": entries,
          "data": base64.b64encode(zlib.compress(bytes(out), 9)).decode("ascii")}
    with open(OUT, "w", encoding="utf-8", newline="") as f:
        json.dump(jm, f, separators=(",", ":"))
    print(f"wrote {OUT} ({W}x{H}, {len(entries)} tile kinds)")


def preview(g):
    """Print the map as text: # wall, h hay, B boss, c chicken, e egg, N portal, S spawn."""
    marks = {WALL: "#", TORCH: "t", HAY: "h", BOSS: "B", CHICKEN: "c", EGG: "e", "Nexus Portal": "N"}
    for y in range(H):
        row = ""
        for x in range(W):
            o = g.obj[y][x]
            row += marks.get(o, "?") if o else ("S" if g.region[y][x] else
                                                ("," if g.ground[y][x] == WOOD else
                                                 "." if g.ground[y][x] else " "))
        if row.strip():
            print(row.rstrip())


if __name__ == "__main__":
    import sys
    grid = build()
    if "--preview" in sys.argv:
        preview(grid)
    else:
        save(grid)
