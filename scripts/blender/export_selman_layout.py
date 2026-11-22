#!/usr/bin/env python3
"""
Converts real OpenStreetMap GPS data (scripts/blender/selman_sokagi_osm.json)
around Selman Sokağı, Hamidiye Mahallesi, Sultanbeyli, İstanbul (40.9537317, 29.2908124)
into a Unity-ready layout JSON: Assets/GRIDROAM/Resources/Maps/SelmanSokagiLayout.json
"""

import json
import math
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OSM_PATH = os.path.join(SCRIPT_DIR, "selman_sokagi_osm.json")
OUT_PATH = os.path.join(PROJECT_ROOT, "Assets", "GRIDROAM", "Resources", "Maps", "SelmanSokagiLayout.json")

LAT0 = 40.9537317
LON0 = 29.2908124
COS_LAT = math.cos(math.radians(LAT0))
CLIP_BOUND = 160.0


def project(lat, lon):
    x = (lon - LON0) * 111320.0 * COS_LAT
    z = (lat - LAT0) * 110540.0
    return x, z


def clip_segment_to_box(x0, z0, x1, z1, bound):
    """Liang-Barsky segment clipping to [-bound, bound] x [-bound, bound]."""
    dx = x1 - x0
    dz = z1 - z0
    t0, t1 = 0.0, 1.0
    checks = [
        (-dx, x0 - (-bound)),
        (dx, bound - x0),
        (-dz, z0 - (-bound)),
        (dz, bound - z0),
    ]
    for p, q in checks:
        if abs(p) < 1e-9:
            if q < 0:
                return None
        else:
            r = q / p
            if p < 0:
                if r > t1:
                    return None
                if r > t0:
                    t0 = r
            else:
                if r < t0:
                    return None
                if r < t1:
                    t1 = r
    nx0 = x0 + t0 * dx
    nz0 = z0 + t0 * dz
    nx1 = x0 + t1 * dx
    nz1 = z0 + t1 * dz
    if math.hypot(nx1 - nx0, nz1 - nz0) < 1.5:
        return None
    return (nx0, nz0, nx1, nz1)


def point_seg_dist(px, pz, x0, z0, x1, z1):
    dx = x1 - x0
    dz = z1 - z0
    l2 = dx * dx + dz * dz
    if l2 < 1e-6:
        return math.hypot(px - x0, pz - z0)
    t = max(0.0, min(1.0, ((px - x0) * dx + (pz - z0) * dz) / l2))
    qx = x0 + t * dx
    qz = z0 + t * dz
    return math.hypot(px - qx, pz - qz)


def compute_obb(pts):
    """Compute oriented bounding box (cx, cz, width, depth, yawDeg) from polygon points."""
    if len(pts) < 3:
        return None
    # Remove closing duplicate
    if math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) < 0.05:
        pts = pts[:-1]
    if len(pts) < 3:
        return None
    # Pick dominant edge direction as primary axis
    best_len = 0.0
    best_ang = 0.0
    for i in range(len(pts)):
        x0, z0 = pts[i]
        x1, z1 = pts[(i + 1) % len(pts)]
        el = math.hypot(x1 - x0, z1 - z0)
        if el > best_len:
            best_len = el
            best_ang = math.atan2(x1 - x0, z1 - z0)
    ca = math.cos(-best_ang)
    sa = math.sin(-best_ang)
    # Rotate into local frame where dominant edge is along +Z
    min_u, max_u = 1e9, -1e9
    min_v, max_v = 1e9, -1e9
    for x, z in pts:
        u = x * ca - z * sa
        v = x * sa + z * ca
        min_u = min(min_u, u)
        max_u = max(max_u, u)
        min_v = min(min_v, v)
        max_v = max(max_v, v)
    w = max_u - min_u
    d = max_v - min_v
    cu = (min_u + max_u) * 0.5
    cv = (min_v + max_v) * 0.5
    # Rotate center back
    ca2 = math.cos(best_ang)
    sa2 = math.sin(best_ang)
    cx = cu * ca2 - cv * sa2
    cz = cu * sa2 + cv * ca2
    yaw_deg = math.degrees(best_ang)
    return (cx, cz, w, d, yaw_deg)


def main():
    with open(OSM_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    nodes = {}
    for el in data["elements"]:
        if el["type"] == "node":
            nodes[el["id"]] = project(el["lat"], el["lon"])

    roads = []
    street_signs = {}
    selman_pts_raw = []

    for el in data["elements"]:
        if el["type"] != "way":
            continue
        tags = el.get("tags", {})
        hw = tags.get("highway")
        if not hw or hw in ("footway", "steps", "path", "pedestrian"):
            continue
        name = tags.get("name", "")
        pts = [nodes[nid] for nid in el.get("nodes", []) if nid in nodes]
        if len(pts) < 2:
            continue

        is_major = hw in ("tertiary", "secondary", "primary") or name in (
            "Emre Caddesi",
            "Mürşit Caddesi",
            "Seyyid Kutup Caddesi",
        )
        is_selman = name == "Selman Sokağı"
        width = 10.2 if is_major else (8.0 if is_selman else 7.2)

        for i in range(len(pts) - 1):
            clipped = clip_segment_to_box(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], CLIP_BOUND)
            if clipped is None:
                continue
            x0, z0, x1, z1 = clipped
            roads.append(
                {
                    "name": name,
                    "x0": round(x0, 2),
                    "z0": round(z0, 2),
                    "x1": round(x1, 2),
                    "z1": round(z1, 2),
                    "width": width,
                    "isMajor": is_major,
                    "isSelman": is_selman,
                }
            )
            if is_selman:
                selman_pts_raw.append((x0, z0, x1, z1))
            if name and name not in street_signs:
                mx, mz = (x0 + x1) * 0.5, (z0 + z1) * 0.5
                if abs(mx) < 135 and abs(mz) < 135:
                    ang = math.degrees(math.atan2(x1 - x0, z1 - z0))
                    # Place sign on sidewalk side
                    ln = math.hypot(x1 - x0, z1 - z0)
                    nx, nz = -(z1 - z0) / ln, (x1 - x0) / ln
                    street_signs[name] = {
                        "name": name,
                        "x": round(mx + nx * (width * 0.5 + 1.3), 2),
                        "z": round(mz + nz * (width * 0.5 + 1.3), 2),
                        "yaw": round(ang, 1),
                    }

    # Reserved gameplay & hero landmark zones (cx, cz, radius) so OSM filler buildings never collide!
    # 48 Selman Sokağı sits on the north side of Selman Sokağı right at (-13.5, 16.5) with its yellow/brick/modern neighbors behind it!
    reserved_zones = [
        (-13.5, 22.0, 27.0),   # 48 Selman Sokağı Hero Composition (No:48 + Brick Construction + Yellow Apt + Modern Apt!)
        (-15.0, 4.5, 11.0),    # Player Spawn & Selman Sokağı Hero Road View Corridor
        (-5.5, -144.0, 26.0),  # Hz. Osman Camii (real OSM location!)
        (-62.0, 12.0, 17.0),   # Mahalle Tamirhanesi (Marco's Garage right near Selman & Çiçekçi/Gürsu)
        (-112.0, -52.0, 18.0), # Akaryakıt İstasyonu on Emre Caddesi
        (52.0, -52.0, 16.0),   # Elena Kargo / Mahalle Deposu on Abdülkadir Udeh & Selman
        (46.0, 32.0, 16.0),    # Mahalle Parkı & Havuzu (between Selman & Mürşit Cd.)
    ]

    buildings = []
    placed_centers = []
    idx = 0
    for el in data["elements"]:
        if el["type"] != "way":
            continue
        tags = el.get("tags", {})
        if not tags.get("building"):
            continue
        name = tags.get("name", "")
        if "cami" in name.lower() or tags.get("building") == "mosque":
            continue  # Hz. Osman Camii is placed explicitly as Blender Istanbul_Mosque hero!

        pts = [nodes[nid] for nid in el.get("nodes", []) if nid in nodes]
        obb = compute_obb(pts)
        if obb is None:
            continue
        cx, cz, w, d, yaw_deg = obb
        if abs(cx) > 150.0 or abs(cz) > 150.0:
            continue
        if w < 4.5 or d < 4.5 or w > 28.0 or d > 28.0:
            continue

        # Clamp visual footprint to realistic Sultanbeyli scale
        w = max(6.5, min(13.0, w))
        d = max(6.5, min(12.0, d))
        radius = math.hypot(w, d) * 0.38
        road_clear_rad = min(w, d) * 0.44

        # Check reserved zones
        skip = False
        for rx, rz, rr in reserved_zones:
            if math.hypot(cx - rx, cz - rz) < (rr + radius * 0.45):
                skip = True
                break
        if skip:
            continue

        # Check road clearance so NO building EVER blocks any real street!
        for r in roads:
            dist = point_seg_dist(cx, cz, r["x0"], r["z0"], r["x1"], r["z1"])
            if dist < (r["width"] * 0.5 + road_clear_rad + 0.9):
                skip = True
                break
        if skip:
            continue

        # Check building-to-building clearance so NO two buildings clip!
        for pcx, pcz, prad in placed_centers:
            if math.hypot(cx - pcx, cz - pcz) < (radius + prad + 0.6):
                skip = True
                break
        if skip:
            continue

        placed_centers.append((cx, cz, radius))
        # Assign authentic Sultanbeyli building archetype deterministically
        kind_roll = idx % 10
        if kind_roll in (0, 3, 6):
            kind = "mahalle_apt"
        elif kind_roll in (1, 7):
            kind = "yellow_apt"
        elif kind_roll in (2, 8):
            kind = "modern_apt"
        elif kind_roll == 4:
            kind = "brick_construction"
        else:
            kind = "enterable_house"

        buildings.append(
            {
                "cx": round(cx, 2),
                "cz": round(cz, 2),
                "w": round(w, 2),
                "d": round(d, 2),
                "yaw": round(yaw_deg, 1),
                "kind": kind,
                "floors": 3 if kind != "modern_apt" else 4,
            }
        )
        idx += 1

    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    payload = {
        "originLat": LAT0,
        "originLon": LON0,
        "neighborhood": "Hamidiye Mahallesi · Sultanbeyli, İstanbul",
        "heroStreet": "Selman Sokağı",
        "roads": roads,
        "streetSigns": list(street_signs.values()),
        "buildings": buildings,
    }
    with open(OUT_PATH, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)

    print(
        f"Exported SelmanSokagiLayout.json -> {len(roads)} road segments, "
        f"{len(street_signs)} street signs, {len(buildings)} non-overlapping real OSM buildings!"
    )


if __name__ == "__main__":
    main()
