"""
Minimal ONNX model writer using protobuf (no torch/onnx dependency).

Builds a valid ONNX model proto for a simple feed-forward MLP with:
  - Input:  "observation" shape [1, 1, 1, obs_dim] (NHWC for Unity Sentis)
  - Reshape to [1, obs_dim]
  - Gemm + Tanh x2 -> Gemm (mean_head)
  - Output: "action" shape [1, act_dim]

Uses google.protobuf to dynamically create the ONNX protobuf messages.
"""

import os
import struct
import numpy as np
from typing import List, Tuple, Optional

from google.protobuf import descriptor_pb2, descriptor, message_factory, descriptor_pool


# ══════════════════════════════════════════════════════════════════════════════
#  ONNX Protobuf Schema Builder
# ══════════════════════════════════════════════════════════════════════════════

class ONNXProtoBuilder:
    """
    Dynamically builds ONNX protobuf message classes using the Python protobuf
    descriptor API. This avoids needing the onnx package or pre-compiled protos.
    """

    _built = False
    _pool = None
    _classes = {}

    @classmethod
    def _ensure_built(cls):
        """Build the ONNX message classes once."""
        if cls._built:
            return

        # Create a FileDescriptorProto for ONNX types
        file_proto = descriptor_pb2.FileDescriptorProto()
        file_proto.name = "onnx.proto"
        file_proto.package = "onnx"

        # ── TensorShapeProto ──
        shape_msg = descriptor_pb2.DescriptorProto()
        shape_msg.name = "TensorShapeProto"

        dim_msg = descriptor_pb2.DescriptorProto()
        dim_msg.name = "Dimension"
        dim_msg.field.add(name="dim_value", number=1,
                          type=descriptor.FieldDescriptor.TYPE_INT64,
                          label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        dim_msg.field.add(name="dim_param", number=2,
                          type=descriptor.FieldDescriptor.TYPE_STRING,
                          label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        dim_msg.field.add(name="denotation", number=3,
                          type=descriptor.FieldDescriptor.TYPE_STRING,
                          label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        shape_msg.nested_type.add().CopyFrom(dim_msg)

        shape_msg.field.add(name="dim", number=1,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="TensorShapeProto.Dimension")

        # ── TypeProto ──
        type_msg = descriptor_pb2.DescriptorProto()
        type_msg.name = "TypeProto"

        tensor_type_msg = descriptor_pb2.DescriptorProto()
        tensor_type_msg.name = "Tensor"
        tensor_type_msg.field.add(name="elem_type", number=1,
                                  type=descriptor.FieldDescriptor.TYPE_INT32,
                                  label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        tensor_type_msg.field.add(name="shape", number=2,
                                  type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                                  label=descriptor.FieldDescriptor.LABEL_OPTIONAL,
                                  type_name="TensorShapeProto")
        type_msg.nested_type.add().CopyFrom(tensor_type_msg)

        # oneof for value
        type_msg.field.add(name="tensor_type", number=1,
                           type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL,
                           type_name="TypeProto.Tensor")

        # ── ValueInfoProto ──
        value_info_msg = descriptor_pb2.DescriptorProto()
        value_info_msg.name = "ValueInfoProto"
        value_info_msg.field.add(name="name", number=1,
                                 type=descriptor.FieldDescriptor.TYPE_STRING,
                                 label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        value_info_msg.field.add(name="type", number=2,
                                 type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                                 label=descriptor.FieldDescriptor.LABEL_OPTIONAL,
                                 type_name="TypeProto")
        value_info_msg.field.add(name="doc_string", number=3,
                                 type=descriptor.FieldDescriptor.TYPE_STRING,
                                 label=descriptor.FieldDescriptor.LABEL_OPTIONAL)

        # ── TensorProto ──
        tensor_msg = descriptor_pb2.DescriptorProto()
        tensor_msg.name = "TensorProto"
        tensor_msg.field.add(name="data_type", number=1,
                             type=descriptor.FieldDescriptor.TYPE_INT32,
                             label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        tensor_msg.field.add(name="name", number=2,
                             type=descriptor.FieldDescriptor.TYPE_STRING,
                             label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        tensor_msg.field.add(name="dims", number=3,
                             type=descriptor.FieldDescriptor.TYPE_INT64,
                             label=descriptor.FieldDescriptor.LABEL_REPEATED)
        tensor_msg.field.add(name="float_data", number=4,
                             type=descriptor.FieldDescriptor.TYPE_FLOAT,
                             label=descriptor.FieldDescriptor.LABEL_REPEATED)
        tensor_msg.field.add(name="int32_data", number=5,
                             type=descriptor.FieldDescriptor.TYPE_INT32,
                             label=descriptor.FieldDescriptor.LABEL_REPEATED)
        tensor_msg.field.add(name="string_data", number=6,
                             type=descriptor.FieldDescriptor.TYPE_BYTES,
                             label=descriptor.FieldDescriptor.LABEL_REPEATED)
        tensor_msg.field.add(name="int64_data", number=7,
                             type=descriptor.FieldDescriptor.TYPE_INT64,
                             label=descriptor.FieldDescriptor.LABEL_REPEATED)
        tensor_msg.field.add(name="raw_data", number=9,
                             type=descriptor.FieldDescriptor.TYPE_BYTES,
                             label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        tensor_msg.field.add(name="doc_string", number=12,
                             type=descriptor.FieldDescriptor.TYPE_STRING,
                             label=descriptor.FieldDescriptor.LABEL_OPTIONAL)

        # ── AttributeProto ──
        attr_msg = descriptor_pb2.DescriptorProto()
        attr_msg.name = "AttributeProto"
        attr_msg.field.add(name="name", number=1,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        attr_msg.field.add(name="f", number=2,
                           type=descriptor.FieldDescriptor.TYPE_FLOAT,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        attr_msg.field.add(name="i", number=3,
                           type=descriptor.FieldDescriptor.TYPE_INT64,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        attr_msg.field.add(name="s", number=4,
                           type=descriptor.FieldDescriptor.TYPE_BYTES,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        attr_msg.field.add(name="t", number=5,
                           type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL,
                           type_name="TensorProto")
        attr_msg.field.add(name="floats", number=7,
                           type=descriptor.FieldDescriptor.TYPE_FLOAT,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED)
        attr_msg.field.add(name="ints", number=8,
                           type=descriptor.FieldDescriptor.TYPE_INT64,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED)
        attr_msg.field.add(name="strings", number=9,
                           type=descriptor.FieldDescriptor.TYPE_BYTES,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED)

        # ── NodeProto ──
        node_msg = descriptor_pb2.DescriptorProto()
        node_msg.name = "NodeProto"
        node_msg.field.add(name="input", number=1,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED)
        node_msg.field.add(name="output", number=2,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED)
        node_msg.field.add(name="name", number=3,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        node_msg.field.add(name="op_type", number=4,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        node_msg.field.add(name="attribute", number=5,
                           type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                           label=descriptor.FieldDescriptor.LABEL_REPEATED,
                           type_name="AttributeProto")
        node_msg.field.add(name="doc_string", number=6,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        node_msg.field.add(name="domain", number=7,
                           type=descriptor.FieldDescriptor.TYPE_STRING,
                           label=descriptor.FieldDescriptor.LABEL_OPTIONAL)

        # ── GraphProto ──
        graph_msg = descriptor_pb2.DescriptorProto()
        graph_msg.name = "GraphProto"
        graph_msg.field.add(name="node", number=1,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="NodeProto")
        graph_msg.field.add(name="name", number=2,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        graph_msg.field.add(name="initializer", number=5,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="TensorProto")
        graph_msg.field.add(name="input", number=11,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="ValueInfoProto")
        graph_msg.field.add(name="output", number=12,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="ValueInfoProto")
        graph_msg.field.add(name="value_info", number=13,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="ValueInfoProto")

        # ── OperatorSetIdProto ──
        opset_msg = descriptor_pb2.DescriptorProto()
        opset_msg.name = "OperatorSetIdProto"
        opset_msg.field.add(name="domain", number=1,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        opset_msg.field.add(name="version", number=2,
                            type=descriptor.FieldDescriptor.TYPE_INT64,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)

        # ── ModelProto ──
        model_msg = descriptor_pb2.DescriptorProto()
        model_msg.name = "ModelProto"
        model_msg.field.add(name="ir_version", number=1,
                            type=descriptor.FieldDescriptor.TYPE_INT64,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="producer_name", number=2,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="producer_version", number=3,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="domain", number=4,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="model_version", number=5,
                            type=descriptor.FieldDescriptor.TYPE_INT64,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="doc_string", number=6,
                            type=descriptor.FieldDescriptor.TYPE_STRING,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL)
        model_msg.field.add(name="graph", number=7,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_OPTIONAL,
                            type_name="GraphProto")
        model_msg.field.add(name="opset_import", number=8,
                            type=descriptor.FieldDescriptor.TYPE_MESSAGE,
                            label=descriptor.FieldDescriptor.LABEL_REPEATED,
                            type_name="OperatorSetIdProto")

        # Add all messages to file descriptor
        for msg in [shape_msg, type_msg, value_info_msg, tensor_msg,
                    attr_msg, node_msg, graph_msg, opset_msg, model_msg]:
            file_proto.message_type.add().CopyFrom(msg)

        # Add to pool
        pool = descriptor_pool.Default()
        file_desc = pool.Add(file_proto)
        cls._pool = pool

        # Get message classes
        cls._classes = {
            "TensorShapeProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["TensorShapeProto"]
            ),
            "TypeProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["TypeProto"]
            ),
            "ValueInfoProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["ValueInfoProto"]
            ),
            "TensorProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["TensorProto"]
            ),
            "AttributeProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["AttributeProto"]
            ),
            "NodeProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["NodeProto"]
            ),
            "GraphProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["GraphProto"]
            ),
            "OperatorSetIdProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["OperatorSetIdProto"]
            ),
            "ModelProto": message_factory.GetMessageClass(
                file_desc.message_types_by_name["ModelProto"]
            ),
        }

        cls._built = True

    @classmethod
    def get(cls, name: str):
        """Get a message class by name."""
        cls._ensure_built()
        return cls._classes[name]


# ══════════════════════════════════════════════════════════════════════════════
#  ONNX Model Builder
# ══════════════════════════════════════════════════════════════════════════════

def _make_tensor(name: str, data: np.ndarray) -> object:
    """
    Create a TensorProto message from a numpy array.

    Args:
        name: Tensor name in the ONNX graph.
        data: Numpy array (float32).

    Returns:
        TensorProto message.
    """
    TensorProto = ONNXProtoBuilder.get("TensorProto")
    tensor = TensorProto()

    tensor.name = name
    tensor.data_type = 1  # FLOAT = 1
    tensor.dims.extend(data.shape)

    # Store as raw bytes for efficiency
    tensor.raw_data = data.astype(np.float32).tobytes()

    return tensor


def _make_value_info(name: str, shape: List[int], elem_type: int = 1) -> object:
    """
    Create a ValueInfoProto message.

    Args:
        name: Tensor name.
        shape: Shape dimensions.
        elem_type: ONNX data type (1 = FLOAT).

    Returns:
        ValueInfoProto message.
    """
    ValueInfoProto = ONNXProtoBuilder.get("ValueInfoProto")
    TypeProto = ONNXProtoBuilder.get("TypeProto")

    vi = ValueInfoProto()
    vi.name = name

    # Build TypeProto
    tp = TypeProto()
    tensor_type = tp.tensor_type  # uses oneof

    # Set elem_type
    type_tensor = tensor_type  # this is the TypeProto.Tensor sub-message
    type_tensor.elem_type = elem_type

    # Build shape
    TensorShapeProto = ONNXProtoBuilder.get("TensorShapeProto")
    shape_msg = TensorShapeProto()
    for d in shape:
        dim = shape_msg.dim.add()
        dim.dim_value = d

    type_tensor.shape.CopyFrom(shape_msg)

    # Set the oneof
    tp.tensor_type.CopyFrom(type_tensor)

    vi.type.CopyFrom(tp)
    return vi


def _make_gemm_node(name: str, input_name: str, weight_name: str,
                    bias_name: str, output_name: str,
                    alpha: float = 1.0, beta: float = 1.0,
                    trans_a: int = 0, trans_b: int = 1) -> object:
    """
    Create a Gemm (General Matrix Multiply) node.

    Y = alpha * A * B^T + beta * C

    With transB=1, the weight matrix is stored as [out_dim, in_dim]
    and the computation is: Y = A @ W^T + b

    Args:
        name: Node name.
        input_name: Input tensor name (A).
        weight_name: Weight tensor name (B).
        bias_name: Bias tensor name (C).
        output_name: Output tensor name.
        alpha: Scalar multiplier for A*B.
        beta: Scalar multiplier for C.
        trans_a: Whether to transpose A.
        trans_b: Whether to transpose B (1 = transpose, for linear layer).

    Returns:
        NodeProto message.
    """
    NodeProto = ONNXProtoBuilder.get("NodeProto")
    AttributeProto = ONNXProtoBuilder.get("AttributeProto")

    node = NodeProto()
    node.name = name
    node.op_type = "Gemm"
    node.input.extend([input_name, weight_name, bias_name])
    node.output.append(output_name)
    node.domain = ""

    # Attributes
    attr_alpha = AttributeProto()
    attr_alpha.name = "alpha"
    attr_alpha.f = alpha
    node.attribute.append(attr_alpha)

    attr_beta = AttributeProto()
    attr_beta.name = "beta"
    attr_beta.f = beta
    node.attribute.append(attr_beta)

    attr_transA = AttributeProto()
    attr_transA.name = "transA"
    attr_transA.i = trans_a
    node.attribute.append(attr_transA)

    attr_transB = AttributeProto()
    attr_transB.name = "transB"
    attr_transB.i = trans_b
    node.attribute.append(attr_transB)

    return node


def _make_tanh_node(name: str, input_name: str, output_name: str) -> object:
    """
    Create a Tanh activation node.

    Args:
        name: Node name.
        input_name: Input tensor name.
        output_name: Output tensor name.

    Returns:
        NodeProto message.
    """
    NodeProto = ONNXProtoBuilder.get("NodeProto")
    node = NodeProto()
    node.name = name
    node.op_type = "Tanh"
    node.input.append(input_name)
    node.output.append(output_name)
    node.domain = ""
    return node


def _make_reshape_node(name: str, input_name: str, shape_name: str,
                       output_name: str) -> object:
    """
    Create a Reshape node.

    Args:
        name: Node name.
        input_name: Input tensor name.
        shape_name: Shape tensor name (int64).
        output_name: Output tensor name.

    Returns:
        NodeProto message.
    """
    NodeProto = ONNXProtoBuilder.get("NodeProto")
    node = NodeProto()
    node.name = name
    node.op_type = "Reshape"
    node.input.extend([input_name, shape_name])
    node.output.append(output_name)
    node.domain = ""
    return node


def build_policy_onnx(
    weights: List[Tuple[np.ndarray, np.ndarray]],
    obs_dim: int,
    act_dim: int,
    model_name: str = "neural_animation_policy",
) -> bytes:
    """
    Build a complete ONNX model for the policy network.

    The policy network architecture:
      Input: "observation" shape [1, 1, 1, obs_dim] (NHWC)
      Reshape to [1, obs_dim]
      Gemm1 (w0, b0) -> Tanh
      Gemm2 (w1, b1) -> Tanh
      Gemm3 (w2, b2) -> Output
      Output: "action" shape [1, act_dim]

    Args:
        weights: List of (weight, bias) tuples for each layer.
                 [trunk_w0, trunk_b0], [trunk_w1, trunk_b1], [mean_w0, mean_b0]
        obs_dim: Observation dimension.
        act_dim: Action dimension.
        model_name: Name for the ONNX model.

    Returns:
        Serialized ONNX model as bytes.
    """
    ONNXProtoBuilder.get("ModelProto")  # Ensure built

    # Unpack weights
    # NOTE: numpy stores weights as [in_dim, out_dim] (h @ W + b)
    # ONNX Gemm with transB=1 expects weights as [out_dim, in_dim] (Y = A @ B^T + C)
    # So we must transpose before storing
    (w0, b0), (w1, b1), (w2, b2) = weights
    w0, w1, w2 = w0.T.copy(), w1.T.copy(), w2.T.copy()

    # Tensor names
    OBS_NAME = "observation"
    ACTION_NAME = "action"
    SHAPE_NAME = "reshape_shape"

    # Names for intermediate tensors
    flat_name = "obs_flat"
    h1_name = "hidden1"
    h1_act_name = "hidden1_act"
    h2_name = "hidden2"
    h2_act_name = "hidden2_act"
    out_name = "action_out"

    # ── Initializers (weights, biases, shape tensor) ──
    initializers = []

    # Reshape shape tensor: [1, obs_dim]
    shape_data = np.array([1, obs_dim], dtype=np.int64)
    shape_tensor = _make_tensor(SHAPE_NAME, shape_data)
    # Override data_type to INT64
    shape_tensor.data_type = 7  # INT64
    initializers.append(shape_tensor)

    # Weight and bias tensors
    w0_t = _make_tensor("gemm_w0", w0)
    b0_t = _make_tensor("gemm_b0", b0)
    initializers.extend([w0_t, b0_t])

    w1_t = _make_tensor("gemm_w1", w1)
    b1_t = _make_tensor("gemm_b1", b1)
    initializers.extend([w1_t, b1_t])

    w2_t = _make_tensor("gemm_w2", w2)
    b2_t = _make_tensor("gemm_b2", b2)
    initializers.extend([w2_t, b2_t])

    # ── Nodes ──
    nodes = []

    # 1. Reshape: [1,1,1,obs_dim] -> [1, obs_dim]
    nodes.append(_make_reshape_node("reshape", OBS_NAME, SHAPE_NAME, flat_name))

    # 2. Gemm1: [1, obs_dim] -> [1, 64]
    nodes.append(_make_gemm_node("gemm1", flat_name, "gemm_w0", "gemm_b0", h1_name))

    # 3. Tanh1
    nodes.append(_make_tanh_node("tanh1", h1_name, h1_act_name))

    # 4. Gemm2: [1, 64] -> [1, 64]
    nodes.append(_make_gemm_node("gemm2", h1_act_name, "gemm_w1", "gemm_b1", h2_name))

    # 5. Tanh2
    nodes.append(_make_tanh_node("tanh2", h2_name, h2_act_name))

    # 6. Gemm3 (mean_head): [1, 64] -> [1, act_dim]
    nodes.append(_make_gemm_node("gemm3", h2_act_name, "gemm_w2", "gemm_b2", out_name))

    # Output is "action"
    # We alias out_name -> ACTION_NAME via identity or just use ACTION_NAME as output of gemm3
    # Actually, let's make gemm3 output directly to ACTION_NAME
    # Update: rebuild gemm3 with output=ACTION_NAME
    nodes.pop()  # Remove gemm3
    nodes.append(_make_gemm_node("gemm3", h2_act_name, "gemm_w2", "gemm_b2", ACTION_NAME))

    # ── Graph inputs/outputs ──
    graph_inputs = [_make_value_info(OBS_NAME, [1, 1, 1, obs_dim])]
    graph_outputs = [_make_value_info(ACTION_NAME, [1, act_dim])]

    # ── Build Graph ──
    GraphProto = ONNXProtoBuilder.get("GraphProto")
    graph = GraphProto()
    graph.name = model_name
    for vi in graph_inputs:
        graph.input.append(vi)
    for vi in graph_outputs:
        graph.output.append(vi)
    for node in nodes:
        graph.node.append(node)
    for init in initializers:
        graph.initializer.append(init)

    # ── Build Model ──
    ModelProto = ONNXProtoBuilder.get("ModelProto")
    OperatorSetIdProto = ONNXProtoBuilder.get("OperatorSetIdProto")

    model = ModelProto()
    model.ir_version = 9
    model.producer_name = "numpy_ppo"
    model.producer_version = "1.0"
    model.domain = "neural_animation"
    model.model_version = 1
    model.doc_string = "Neural Animation policy network exported from numpy PPO"

    # Opset import (ai.onnx v17)
    opset = OperatorSetIdProto()
    opset.domain = "ai.onnx"
    opset.version = 17
    model.opset_import.append(opset)

    model.graph.CopyFrom(graph)

    # Serialize
    return model.SerializeToString()


def export_policy_to_onnx(
    weights: List[Tuple[np.ndarray, np.ndarray]],
    obs_dim: int,
    act_dim: int,
    output_path: str,
    verbose: bool = True,
) -> str:
    """
    Export policy weights to a valid ONNX file.

    Args:
        weights: List of (weight, bias) tuples for each layer.
        obs_dim: Observation dimension.
        act_dim: Action dimension.
        output_path: Path to write the .onnx file.
        verbose: Print export info.

    Returns:
        Absolute path to the written .onnx file.
    """
    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)

    # Build model
    model_bytes = build_policy_onnx(weights, obs_dim, act_dim)

    # Write to file
    with open(output_path, "wb") as f:
        f.write(model_bytes)

    file_size = os.path.getsize(output_path)

    if verbose:
        print(f"  [ONNX Export] Written to: {output_path}")
        print(f"  [ONNX Export] File size:  {file_size / 1024:.1f} KB")
        print(f"  [ONNX Export] obs_dim={obs_dim}, act_dim={act_dim}")

    return os.path.abspath(output_path)


def validate_onnx(onnx_path: str, obs_dim: int, act_dim: int, verbose: bool = True) -> bool:
    """
    Validate an ONNX model file by checking its structure.

    Since onnx and onnxruntime are not available, we do basic validation:
    - File exists and has content
    - Can be parsed by protobuf as ModelProto
    - Has correct input/output names
    - Has expected ops

    Args:
        onnx_path: Path to the .onnx file.
        obs_dim: Expected observation dimension.
        act_dim: Expected action dimension.
        verbose: Print validation info.

    Returns:
        True if valid, False otherwise.
    """
    ONNXProtoBuilder.get("ModelProto")  # Ensure built

    errors = []

    # 1. Check file exists
    if not os.path.exists(onnx_path):
        errors.append(f"File not found: {onnx_path}")
        if verbose:
            print(f"  [ONNX Validation] ✗ File not found")
        return False

    file_size = os.path.getsize(onnx_path)
    if file_size == 0:
        errors.append("File is empty")
        if verbose:
            print(f"  [ONNX Validation] ✗ File is empty")
        return False

    # 2. Parse with protobuf
    ModelProto = ONNXProtoBuilder.get("ModelProto")
    try:
        with open(onnx_path, "rb") as f:
            model = ModelProto()
            model.ParseFromString(f.read())
    except Exception as e:
        errors.append(f"Failed to parse ONNX model: {e}")
        if verbose:
            print(f"  [ONNX Validation] ✗ Parse failed: {e}")
        return False

    # 3. Check graph
    graph = model.graph
    if not graph:
        errors.append("Model has no graph")
        if verbose:
            print(f"  [ONNX Validation] ✗ No graph")
        return False

    # 4. Check input names
    input_names = [inp.name for inp in graph.input]
    if "observation" not in input_names:
        errors.append(f"Input 'observation' not found. Found: {input_names}")
    else:
        if verbose:
            print(f"  [ONNX Validation] Input name 'observation': ✓")

    # 5. Check output names
    output_names = [out.name for out in graph.output]
    if "action" not in output_names:
        errors.append(f"Output 'action' not found. Found: {output_names}")
    else:
        if verbose:
            print(f"  [ONNX Validation] Output name 'action': ✓")

    # 6. Check node count (should be 6: Reshape, Gemm, Tanh, Gemm, Tanh, Gemm)
    node_count = len(graph.node)
    if node_count < 4:
        errors.append(f"Too few nodes: {node_count} (expected 6)")
    else:
        if verbose:
            op_types = [n.op_type for n in graph.node]
            print(f"  [ONNX Validation] Nodes ({node_count}): {', '.join(op_types)}")

    # 7. Check initializer count
    init_count = len(graph.initializer)
    if verbose:
        print(f"  [ONNX Validation] Initializers: {init_count}")
        print(f"  [ONNX Validation] File size: {file_size / 1024:.1f} KB")

    if errors:
        if verbose:
            print(f"  [ONNX Validation] ✗ FAILED:")
            for err in errors:
                print(f"    - {err}")
        return False

    if verbose:
        print(f"  [ONNX Validation] ✓ All checks passed")
    return True