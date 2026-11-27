#!/usr/bin/env python3
"""Parse GLB files, extract bounding box from accessor min/max (POSITION) with node transforms."""
import json, struct, os, sys, glob

DIR = sys.argv[1] if len(sys.argv) > 1 else "/mnt/c/Unity/code/Assets/Resources/Models/Restaurant"

def node_matrix(node):
    if "matrix" in node:
        m = node["matrix"]
        # column-major 4x4
        return [[m[0],m[4],m[8],m[12]],[m[1],m[5],m[9],m[13]],[m[2],m[6],m[10],m[14]],[m[3],m[7],m[11],m[15]]]
    t = node.get("translation", [0,0,0])
    r = node.get("rotation", [0,0,0,1])
    s = node.get("scale", [1,1,1])
    x,y,z,w = r
    rm = [
        [1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
        [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
        [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)],
    ]
    m = [[rm[i][j]*s[j] for j in range(3)] + [0.0] for i in range(3)]
    m.append([t[0], t[1], t[2], 1.0])
    return m

def matmul(a,b):
    return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def xform(m, v):
    return [m[i][0]*v[0]+m[i][1]*v[1]+m[i][2]*v[2]+m[i][3] for i in range(3)]

def analyze(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, ver, length = struct.unpack("<4sII", data[:12])
    if magic != b"glTF":
        return None
    clen, ctype = struct.unpack("<II", data[12:20])
    js = json.loads(data[20:20+clen].decode("utf-8"))
    nodes = js.get("nodes", [])
    scenes = js.get("scenes", [])
    roots = scenes[js.get("scene",0)]["nodes"] if scenes else [i for i in range(len(nodes))]
    # BFS accumulate world transforms per node
    world = {}
    stack = [(ri, [[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]]) for ri in roots]
    while stack:
        ni, pm = stack.pop()
        m = matmul(pm, node_matrix(nodes[ni]))
        world[ni] = m
        for c in nodes[ni].get("children", []):
            stack.append((c, m))
    gmin = [1e9]*3; gmax = [-1e9]*3
    accs = js.get("accessors", [])
    for ni, m in world.items():
        node = nodes[ni]
        if "mesh" not in node: continue
        mesh = js["meshes"][node["mesh"]]
        for prim in mesh.get("primitives", []):
            pa = prim.get("attributes", {}).get("POSITION")
            if pa is None: continue
            acc = accs[pa]
            mn = acc.get("min"); mx = acc.get("max")
            if not mn or not mx: continue
            # 8 corners
            for cx in (mn[0], mx[0]):
                for cy in (mn[1], mx[1]):
                    for cz in (mn[2], mx[2]):
                        v = xform(m, [cx,cy,cz])
                        for i in range(3):
                            gmin[i] = min(gmin[i], v[i]); gmax[i] = max(gmax[i], v[i])
    if gmin[0] > 1e8:
        return None
    size = [gmax[i]-gmin[i] for i in range(3)]
    return size, [ (gmin[i]+gmax[i])/2 for i in range(3) ]

rows = []
for p in sorted(glob.glob(os.path.join(DIR, "*.glb"))):
    r = analyze(p)
    if r:
        size, center = r
        rows.append((os.path.basename(p)[:-4], size[0], size[1], size[2], center[1]))
    else:
        rows.append((os.path.basename(p)[:-4], -1,-1,-1,-1))

for name, sx, sy, sz, cy in rows:
    print(f"{name}\tX={sx:.3f}\tY={sy:.3f}\tZ={sz:.3f}\tcenterY={cy:.3f}")
