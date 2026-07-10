from flask import Flask, request, jsonify
import os
import cv2
import numpy as np

from autolab_core import (
    YamlConfig, Logger, BinaryImage, CameraIntrinsics,
    ColorImage, DepthImage, RgbdImage
)
from gqcnn.grasping import CrossEntropyRobustGraspingPolicy, RgbdImageState


app = Flask(__name__)

# =========================
# 設定
# =========================
HOST = "127.0.0.1"
PORT = 5001

DEXNET_CONFIG_PATH = "/home/okura/gqcnn/cfg/examples/gqcnn_pj.yaml"

# Unity SimCamera の「カメラ」→「視野角」と合わせる
DEFAULT_CAMERA_FOV_Y_DEG = 60.0

# GQ-CNNの把持サンプリング数
GQ_NUM_GRASPS = 1000

GRASP_Z_OFFSET = 0.00

logger = Logger.get_logger(__name__)


def create_unity_camera_intrinsics(width, height, fov_y_deg):
    """
    Unityカメラの画像サイズと垂直FOVから仮の内部パラメータを作る。
    """
    fov_y_rad = np.deg2rad(fov_y_deg)

    fy = (height / 2.0) / np.tan(fov_y_rad / 2.0)
    fx = fy

    cx = width / 2.0
    cy = height / 2.0

    return CameraIntrinsics(
        frame="unity_camera",
        fx=fx,
        fy=fy,
        cx=cx,
        cy=cy,
        width=width,
        height=height
    )


def init_gqcnn_policy():
    config = YamlConfig(DEXNET_CONFIG_PATH)
    policy_config = config["policy"]

    policy_config["metric"]["min_depth"] = 0.05
    policy_config["metric"]["max_depth"] = 2.0

    if "sampling" in policy_config:
        policy_config["sampling"]["min_depth"] = 0.05
        policy_config["sampling"]["max_depth"] = 2.0
        policy_config["sampling"]["num_grasps"] = GQ_NUM_GRASPS

    policy_config["vis"]["grasp_sampling"] = False
    policy_config["vis"]["elite_grasps"] = False
    policy_config["vis"]["final_grasp"] = False

    policy = CrossEntropyRobustGraspingPolicy(policy_config)
    return policy


print("Loading GQ-CNN policy...")
gqcnn_policy = init_gqcnn_policy()
print("GQ-CNN policy loaded.")


def run_gqcnn(depth_path, mask_path, fov_y_deg):
    """
    depth.npy と mask.png を読み込んでGQ-CNNを実行する。
    depth.npy はメートル単位の float32 2D配列を想定。
    """
    if not os.path.exists(depth_path):
        raise FileNotFoundError(f"depth_path が存在しません: {depth_path}")

    if not os.path.exists(mask_path):
        raise FileNotFoundError(f"mask_path が存在しません: {mask_path}")

    depth_data = np.load(depth_path).astype(np.float32)
    mask_raw = cv2.imread(mask_path, 0)

    if mask_raw is None:
        raise ValueError(f"mask画像の読み込みに失敗しました: {mask_path}")

    if mask_raw.shape != depth_data.shape:
        mask_raw = cv2.resize(
            mask_raw,
            (depth_data.shape[1], depth_data.shape[0]),
            interpolation=cv2.INTER_NEAREST
        )

    mask_data = (mask_raw > 0).astype(np.uint8) * 255

    height, width = depth_data.shape
    camera_intr = create_unity_camera_intrinsics(width, height, fov_y_deg)

    depth_im = DepthImage(depth_data, frame=camera_intr.frame)
    segmask = BinaryImage(mask_data, frame=camera_intr.frame)

    color_im = ColorImage(
        np.zeros([height, width, 3]).astype(np.uint8),
        frame=camera_intr.frame
    )

    valid_px_mask = depth_im.invalid_pixel_mask().inverse()
    filtered_segmask = segmask.mask_binary(valid_px_mask)

    if np.count_nonzero(filtered_segmask.data) > 0:
        segmask = filtered_segmask
    else:
        print("Warning: Mask empty after depth filtering. Using raw mask.")

    rgbd_im = RgbdImage.from_color_and_depth(color_im, depth_im)
    state = RgbdImageState(rgbd_im, camera_intr, segmask=segmask)

    action = gqcnn_policy(state)
    grasp = action.grasp
    q_value = action.q_value

    final_x = float(grasp.center[0])
    final_y = float(grasp.center[1])
    final_depth = float(grasp.depth + GRASP_Z_OFFSET)
    final_angle = float(grasp.angle)

    # 元プログラムと同じ角度正規化
    if final_angle > np.pi / 2:
        final_angle -= np.pi
    elif final_angle < -np.pi / 2:
        final_angle += np.pi

    return {
        "success": True,
        "center_x": final_x,
        "center_y": final_y,
        "depth": final_depth,
        "angle": final_angle,
        "q_value": float(q_value)
    }


@app.route("/grasp", methods=["POST"])
def grasp_endpoint():
    try:
        data = request.get_json()

        if data is None:
            return jsonify({
                "success": False,
                "message": "JSONが空です"
            }), 400

        depth_path = data.get("depth_path")
        mask_path = data.get("mask_path")
        fov_y_deg = float(data.get("fov_y_deg", DEFAULT_CAMERA_FOV_Y_DEG))

        if depth_path is None or mask_path is None:
            return jsonify({
                "success": False,
                "message": "depth_path または mask_path がありません"
            }), 400

        print("\n=== Dex-Net / GQ-CNN Request ===")
        print(f"depth_path: {depth_path}")
        print(f"mask_path : {mask_path}")

        result = run_gqcnn(depth_path, mask_path, fov_y_deg)

        print(
            f"GQ-CNN Success | "
            f"x:{result['center_x']:.1f}, y:{result['center_y']:.1f}, "
            f"depth:{result['depth']:.3f}, angle:{result['angle']:.3f}, "
            f"Q:{result['q_value']:.4f}"
        )

        return jsonify(result)

    except Exception as e:
        print("GQ-CNN Error:", e)
        return jsonify({
            "success": False,
            "message": str(e)
        }), 500


if __name__ == "__main__":
    print("=== Dex-Net / GQ-CNN Server 起動 ===")
    print(f"URL: http://{HOST}:{PORT}/grasp")
    app.run(host=HOST, port=PORT, debug=False)
