#!/usr/bin/env python3
"""
Phase 67.3.1 — Validate all ONNX models in Resources/NeuralModels/
Checks: input name/shape, output name/shape, opset, op types
"""
import os, sys, json
import onnx

NEURAL_MODELS = "/mnt/c/Unity/code/Assets/Resources/NeuralModels"
REQUIRED_INPUT = "observation"
REQUIRED_OUTPUT = "action"

results = []
errors = []
total = 0

for fname in sorted(os.listdir(NEURAL_MODELS)):
    if not fname.endswith(".onnx"):
        continue
    total += 1
    fpath = os.path.join(NEURAL_MODELS, fname)
    try:
        model = onnx.load(fpath)
        graph = model.graph

        # Opset
        opset = {o.domain: o.version for o in model.opset_import}
        opset_version = opset.get("", "?")

        # Inputs
        inputs = []
        for inp in graph.input:
            shape_dims = [d.dim_value for d in inp.type.tensor_type.shape.dim]
            inputs.append({
                "name": inp.name,
                "shape": shape_dims,
                "elem_type": inp.type.tensor_type.elem_type
            })

        # Outputs
        outputs = []
        for out in graph.output:
            shape_dims = [d.dim_value for d in out.type.tensor_type.shape.dim]
            outputs.append({
                "name": out.name,
                "shape": shape_dims,
                "elem_type": out.type.tensor_type.elem_type
            })

        # Op types
        op_types = sorted(set(n.op_type for n in graph.node))

        # Validate
        issues = []
        inp_names = [i["name"] for i in inputs]
        out_names = [o["name"] for o in outputs]

        if REQUIRED_INPUT not in inp_names:
            issues.append(f"MISSING input '{REQUIRED_INPUT}' (have: {inp_names})")
        if REQUIRED_OUTPUT not in out_names:
            issues.append(f"MISSING output '{REQUIRED_OUTPUT}' (have: {out_names})")

        # Check NHWC shape for observation input
        for inp in inputs:
            if inp["name"] == REQUIRED_INPUT:
                shape = inp["shape"]
                if len(shape) != 4:
                    issues.append(f"Input shape should be NHWC [1,1,1,N] but got {shape}")
                elif shape[0] != 1 or shape[1] != 1 or shape[2] != 1:
                    issues.append(f"Input shape NHWC: expected [1,1,1,N] but got {shape}")

        # Check action output shape
        for out in outputs:
            if out["name"] == REQUIRED_OUTPUT:
                shape = out["shape"]
                if len(shape) != 2:
                    issues.append(f"Output shape should be [1,M] but got {shape}")
                elif shape[0] != 1:
                    issues.append(f"Output first dim should be 1 but got {shape[0]}")

        # File size
        size_kb = os.path.getsize(fpath) / 1024

        entry = {
            "file": fname,
            "size_kb": round(size_kb, 1),
            "opset": opset_version,
            "inputs": inputs,
            "outputs": outputs,
            "op_types": op_types,
            "valid": len(issues) == 0,
            "issues": issues
        }
        results.append(entry)

        status = "✅" if entry["valid"] else "❌"
        obs_dim = "?"
        act_dim = "?"
        for inp in inputs:
            if inp["name"] == REQUIRED_INPUT:
                obs_dim = inp["shape"][3] if len(inp["shape"]) == 4 else inp["shape"]
        for out in outputs:
            if out["name"] == REQUIRED_OUTPUT:
                act_dim = out["shape"][1] if len(out["shape"]) == 2 else out["shape"]
        print(f"  {status} {fname:45s} obs={obs_dim} act={act_dim} opset={opset_version} {size_kb:>6.1f}KB")
        for iss in issues:
            print(f"       ⚠️ {iss}")

    except Exception as e:
        errors.append({"file": fname, "error": str(e)})
        print(f"  ❌ {fname:45s} PARSE ERROR: {e}")

# Summary
valid_count = sum(1 for r in results if r["valid"])
print(f"\n{'='*70}")
print(f"  Total: {total}  Valid: {valid_count}  Invalid: {total - valid_count - len(errors)}  Errors: {len(errors)}")
print(f"{'='*70}")

# Save detailed report
report = {
    "total": total,
    "valid": valid_count,
    "invalid": total - valid_count - len(errors),
    "errors": len(errors),
    "models": results,
    "error_details": errors
}
report_path = "/mnt/c/Unity/code/Tools/onnx_validation_report.json"
with open(report_path, "w") as f:
    json.dump(report, f, indent=2)
print(f"\nReport saved to: {report_path}")

# Exit code
sys.exit(0 if valid_count == total else 1)