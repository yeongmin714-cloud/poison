#!/usr/bin/env python3
"""Classify table shape by analyzing top-surface vertex angle distribution."""
import json, struct, os, sys, math

DIR = "/mnt/c/Unity/code/Assets/Resources/Models/Restaurant"

def load_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    clen, _ = struct.unpack("<II", data[12:20])
    js = json.loads(data[20:20+clen].decode("utf-8"))
    off = 20 + clen
    blen, btype = struct.unpack("<II", data[off:off+8])
    bin_data = data[off+8:off+8+blen]
    return js, bin_data

def read_positions(js, bin_data, acc_idx):
    acc = js["accessors"][acc_idx]
    bv = js["bufferViews"][acc["bufferView"]]
    start = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    n = acc["count"]
    comp = {"VEC3": 3, "VEC2": 2, "SCALAR": 1}[acc["type"]]
    fmt = {5126: "f", 5125: "I", 5123: "H"}[acc["componentType"]]
    sz = struct.calcsize(fmt)
    stride = bv.get("byteStride") or comp*sz
    out = []
    for i in range(n):
        b = bin_data[start + i*stride : start + i*stride + comp*sz]
        out.append(struct.unpack("<"+fmt*comp, b))
    return out

def analyze(path):
    js, bin_data = load_glb(path)
    ys = []
    pts = []
    accs = js["accessors"]
    for mesh in js.get("meshes", []):
        for prim in mesh.get("primitives", []):
            pa = prim.get("attributes", {}).get("POSITION")
            if pa is None: continue
            pts.extend(read_positions(js, bin_data, pa))
    if not pts:
        return None
    ymin = min(p[1] for p in pts); ymax = max(p[1] for p in pts)
    h = ymax - ymin
    # top slab: y > ymax - 0.12*h
    thr = ymax - 0.12*h
    top = [(p[0], p[2]) for p in pts if p[1] >= thr]
    if len(top) < 8:
        return None
    cx = sum(t[0] for t in top)/len(top); cz = sum(t[1] for t in top)/len(top)
    bins = [0]*36
    for t in top:
        a = math.atan2(t[1]-cz, t[0]-cx)
        bins[int((a+math.pi)/(2*math.pi)*36) % 36] += 1
    occupied = sum(1 for b in bins if b > 0)
    return h, occupied, len(top)

for name in sys.argv[1:]:
    r = analyze(os.path.join(DIR, name + ".glb"))
    if r:
        print(f"{name}\th={r[0]:.3f}\ttopVerts={r[2]}\tangleBins={r[1]}/36")
    else:
        print(f"{name}\tNO-DATA")
