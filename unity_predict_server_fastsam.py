from flask import Flask, request, jsonify
import struct
import os
os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")
import time
import cv2
import numpy as np
import torch
import requests
from ultralytics import FastSAM


app = Flask(__name__)

# =========================
# 基本設定
# =========================
SAVE_DIR = "/home/okura/analysis/unity_input"
MODEL_PATH = "FastSAM-x.pt"

HOST = "0.0.0.0"
PORT = 5000

# Dex-Netサーバー
DEXNET_SERVER_URL = "http://127.0.0.1:5001/grasp"

os.makedirs(SAVE_DIR, exist_ok=True)

# =========================
# FastSAM設定
# =========================
FASTSAM_CONF = 0.5
FASTSAM_IOU = 0.9

# FastSAMで抽出する物体候補数
TOP_N_OBJECTS = 3

# Unityへ返す最終把持候補数
TOP_N_GRASPS = 3

# =========================
# ROI / フィルタ設定
# 画像サイズに対する割合で指定
# =========================
ROI_X_MIN_RATIO = 0.30
ROI_X_MAX_RATIO = 0.70
ROI_Y_MIN_RATIO = 0.20
ROI_Y_MAX_RATIO = 0.75

AREA_MIN = 300
AREA_MAX = 20000

BORDER_MARGIN = 5
MAX_ASPECT_RATIO = 4.0

# 今のDepth画像は「近いほど白」なので、暗すぎる領域は背景寄りとして除外
DEPTH_GRAY_MIN = 10
MIN_VALID_DEPTH_RATIO = 0.3

# =========================
# Unity Depth仮変換設定
# =========================
# DepthCapture.shader の MinDepth / MaxDepth と合わせる
UNITY_DEPTH_MIN_M = 0.10
UNITY_DEPTH_MAX_M = 0.60

# Unity SimCamera の「カメラ」→「視野角」と合わせる
UNITY_CAMERA_FOV_Y_DEG = 60.0


# =========================
# モデル読み込み
# =========================
print("Loading FastSAM model...")
fastsam_model = FastSAM(MODEL_PATH)
device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"FastSAM device: {device}")


# =========================
# Unity通信データのデコード
# =========================

def get_float_header(name, default_value):
    try:
        return float(request.headers.get(name, default_value))
    except (TypeError, ValueError):
        return float(default_value)


def print_array_stats(label, array):
    arr = np.asarray(array, dtype=np.float32)
    finite = arr[np.isfinite(arr)]

    if finite.size == 0:
        print(f"{label} stats: no finite values", flush=True)
        return

    print(
        f"{label} stats: "
        f"shape={arr.shape}, "
        f"min={float(np.min(finite)):.6f}, "
        f"max={float(np.max(finite)):.6f}, "
        f"mean={float(np.mean(finite)):.6f}, "
        f"p1={float(np.percentile(finite, 1)):.6f}, "
        f"p50={float(np.percentile(finite, 50)):.6f}, "
        f"p99={float(np.percentile(finite, 99)):.6f}",
        flush=True
    )
def read_int_from_bytes(data, offset):
    """
    UnityのBinaryWriter.Write(int) は4バイト little-endian。
    """
    value = struct.unpack_from("<i", data, offset)[0]
    return value, offset + 4


def decode_unity_dual_images(raw_data, depth_encoding="jpg", depth_min_m=UNITY_DEPTH_MIN_M, depth_max_m=UNITY_DEPTH_MAX_M):
    """
    Unity ImageSender binary format:
    [rgbLength(int)][rgbBytes][depthLength(int)][depthBytes]
    RGB is JPG. Depth is either JPG or EXR float depending on X-Depth-Encoding.
    """
    offset = 0

    rgb_length, offset = read_int_from_bytes(raw_data, offset)
    if rgb_length <= 0:
        raise ValueError("Invalid RGB image byte length")

    rgb_bytes = raw_data[offset:offset + rgb_length]
    offset += rgb_length

    depth_length, offset = read_int_from_bytes(raw_data, offset)
    if depth_length <= 0:
        raise ValueError("Invalid depth image byte length")

    depth_bytes = raw_data[offset:offset + depth_length]
    offset += depth_length

    rgb_array = np.frombuffer(rgb_bytes, dtype=np.uint8)
    depth_array = np.frombuffer(depth_bytes, dtype=np.uint8)

    rgb_image = cv2.imdecode(rgb_array, cv2.IMREAD_COLOR)

    if rgb_image is None:
        raise ValueError("Failed to decode RGB image")

    if depth_encoding == "exr_float":
        depth_raw = cv2.imdecode(depth_array, cv2.IMREAD_UNCHANGED)

        if depth_raw is None:
            raise ValueError("Failed to decode EXR depth image")

        if depth_raw.ndim == 3:
            depth01 = depth_raw[:, :, 0]
        else:
            depth01 = depth_raw

        depth01 = np.nan_to_num(depth01.astype(np.float32), nan=0.0, posinf=1.0, neginf=0.0)
        depth01 = np.clip(depth01, 0.0, 1.0)
        print_array_stats("depth01", depth01)
        depth_meters = depth01_to_meters(depth01, depth_min_m, depth_max_m)
        print_array_stats("depth_meters_from_exr", depth_meters)

        depth_gray = (depth01 * 255.0).astype(np.uint8)
        depth_image = cv2.cvtColor(depth_gray, cv2.COLOR_GRAY2BGR)
    else:
        depth_image = cv2.imdecode(depth_array, cv2.IMREAD_COLOR)

        if depth_image is None:
            raise ValueError("Failed to decode JPG depth image")

        depth_gray = cv2.cvtColor(depth_image, cv2.COLOR_BGR2GRAY)
        print_array_stats("depth_gray", depth_gray)
        depth_meters = depth_gray_to_meters(depth_gray, depth_min_m, depth_max_m)
        print_array_stats("depth_meters_from_jpg", depth_meters)

    return rgb_image, depth_image, depth_meters, rgb_bytes, depth_bytes

# =========================
# ROI / 候補フィルタ
# =========================
def get_roi_bounds(image_width, image_height):
    roi_x_min = int(image_width * ROI_X_MIN_RATIO)
    roi_x_max = int(image_width * ROI_X_MAX_RATIO)
    roi_y_min = int(image_height * ROI_Y_MIN_RATIO)
    roi_y_max = int(image_height * ROI_Y_MAX_RATIO)

    return roi_x_min, roi_x_max, roi_y_min, roi_y_max


def mask_to_contour(binary_mask):
    cnts, _ = cv2.findContours(binary_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not cnts:
        return None
    return max(cnts, key=cv2.contourArea)


def is_candidate_valid(binary_mask, depth_gray):
    """
    Unity画像用の簡易フィルタ。
    ハンドの指、箱の側面、背景をなるべく除外する。
    """
    h_img, w_img = binary_mask.shape
    roi_x_min, roi_x_max, roi_y_min, roi_y_max = get_roi_bounds(w_img, h_img)

    area = int(np.count_nonzero(binary_mask))
    if area < AREA_MIN or area > AREA_MAX:
        return False, "area"

    x, y, w, h = cv2.boundingRect(binary_mask)

    if x <= BORDER_MARGIN or y <= BORDER_MARGIN:
        return False, "border"
    if x + w >= w_img - BORDER_MARGIN or y + h >= h_img - BORDER_MARGIN:
        return False, "border"

    cx = x + w / 2.0
    cy = y + h / 2.0

    if not (roi_x_min <= cx <= roi_x_max and roi_y_min <= cy <= roi_y_max):
        return False, "roi_center"

    roi_margin = 20
    if x < roi_x_min - roi_margin or x + w > roi_x_max + roi_margin:
        return False, "roi_bbox_x"
    if y < roi_y_min - roi_margin or y + h > roi_y_max + roi_margin:
        return False, "roi_bbox_y"

    aspect = max(w / max(h, 1), h / max(w, 1))
    if aspect > MAX_ASPECT_RATIO:
        return False, "aspect"

    depth_values = depth_gray[binary_mask > 0]
    if len(depth_values) == 0:
        return False, "no_depth"

    valid_depth_values = depth_values[depth_values > DEPTH_GRAY_MIN]
    valid_ratio = len(valid_depth_values) / len(depth_values)

    if valid_ratio < MIN_VALID_DEPTH_RATIO:
        return False, "depth_ratio"

    return True, "ok"


# =========================
# Depth JPGを仮のメートル深度へ変換
# =========================
def depth_gray_to_meters(depth_gray, depth_min_m=UNITY_DEPTH_MIN_M, depth_max_m=UNITY_DEPTH_MAX_M):
    """
    現在のUnity Depth画像は確認用JPG。
    白=近い、黒=遠いとして仮のメートル深度に変換する。
    """
    depth01 = depth_gray.astype(np.float32) / 255.0

    # 白いほど近いので、1.0 - depth01 で距離方向に変換
    depth_m = depth_min_m + (1.0 - depth01) * (depth_max_m - depth_min_m)

    return depth_m.astype(np.float32)

def depth01_to_meters(depth01, depth_min_m=UNITY_DEPTH_MIN_M, depth_max_m=UNITY_DEPTH_MAX_M):
    """
    Unity DepthCapture.shader outputs near objects as 1.0 and far objects as 0.0.
    Convert that normalized depth image to approximate meters.
    """
    depth01 = np.clip(depth01.astype(np.float32), 0.0, 1.0)
    depth_m = depth_min_m + (1.0 - depth01) * (depth_max_m - depth_min_m)
    return depth_m.astype(np.float32)


# =========================
# FastSAM候補抽出
# =========================
def extract_top_object_candidates(rgb_image, depth_image, timestamp):
    h_img, w_img = rgb_image.shape[:2]

    depth_gray = cv2.cvtColor(depth_image, cv2.COLOR_BGR2GRAY)
    rgb_for_model = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)

    results = fastsam_model(
        rgb_for_model,
        device=device,
        retina_masks=True,
        imgsz=max(w_img, h_img),
        conf=FASTSAM_CONF,
        iou=FASTSAM_IOU,
        verbose=False
    )

    candidates = []
    rejected_info = {}

    if len(results) == 0 or results[0].masks is None:
        debug_viz = make_fastsam_debug_viz(rgb_image, depth_gray, [], [], timestamp)
        return [], debug_viz

    masks_data = results[0].masks.data.cpu().numpy()

    for i, mask in enumerate(masks_data):
        if mask.shape != (h_img, w_img):
            mask = cv2.resize(mask, (w_img, h_img), interpolation=cv2.INTER_NEAREST)

        binary_mask = (mask > 0).astype(np.uint8)

        contour = mask_to_contour(binary_mask)
        if contour is None:
            continue

        valid, reason = is_candidate_valid(binary_mask, depth_gray)
        if not valid:
            rejected_info[reason] = rejected_info.get(reason, 0) + 1
            continue

        x, y, w, h = cv2.boundingRect(binary_mask)
        area = int(np.count_nonzero(binary_mask))

        depth_values = depth_gray[binary_mask > 0]
        valid_depth_values = depth_values[depth_values > DEPTH_GRAY_MIN]

        mean_depth_gray = float(np.mean(valid_depth_values))

        bbox_cx = float(x + w / 2.0)
        bbox_cy = float(y + h / 2.0)

        m = cv2.moments(binary_mask)
        if m["m00"] != 0:
            mask_cx = float(m["m10"] / m["m00"])
            mask_cy = float(m["m01"] / m["m00"])
        else:
            mask_cx = bbox_cx
            mask_cy = bbox_cy

        candidate = {
            "mask_index": int(i),
            "area": area,
            "bbox": [int(x), int(y), int(w), int(h)],
            "center_px": [mask_cx, mask_cy],
            "mean_depth_gray": mean_depth_gray,
            "fastsam_score": mean_depth_gray / 255.0,
            "mask": binary_mask,
            "contour": contour
        }

        candidates.append(candidate)

    candidates.sort(key=lambda c: c["mean_depth_gray"], reverse=True)
    top_candidates = candidates[:TOP_N_OBJECTS]

    debug_viz = make_fastsam_debug_viz(
        rgb_image=rgb_image,
        depth_gray=depth_gray,
        all_candidates=candidates,
        top_candidates=top_candidates,
        timestamp=timestamp
    )

    print("Rejected:", rejected_info)
    print(f"Valid object candidates: {len(candidates)} / Top: {len(top_candidates)}")

    return top_candidates, debug_viz


# =========================
# Dex-Netサーバー呼び出し
# =========================
def request_dexnet_grasp(depth_path, mask_path):
    payload = {
        "depth_path": depth_path,
        "mask_path": mask_path,
        "fov_y_deg": UNITY_CAMERA_FOV_Y_DEG
    }

    response = requests.post(
        DEXNET_SERVER_URL,
        json=payload,
        timeout=60
    )

    if response.status_code != 200:
        raise RuntimeError(
            f"Dex-Net server error: {response.status_code}, {response.text}"
        )

    return response.json()


# =========================
# 可視化
# =========================
def make_fastsam_debug_viz(rgb_image, depth_gray, all_candidates, top_candidates, timestamp):
    viz = rgb_image.copy()

    h_img, w_img = rgb_image.shape[:2]
    roi_x_min, roi_x_max, roi_y_min, roi_y_max = get_roi_bounds(w_img, h_img)

    cv2.rectangle(
        viz,
        (roi_x_min, roi_y_min),
        (roi_x_max, roi_y_max),
        (255, 0, 0),
        2
    )

    for c in all_candidates:
        cv2.drawContours(viz, [c["contour"]], -1, (0, 255, 0), 1)

    colors = [
        (0, 0, 255),
        (0, 255, 255),
        (255, 0, 255),
    ]

    for rank, c in enumerate(top_candidates, start=1):
        color = colors[(rank - 1) % len(colors)]

        contour = c["contour"]
        cx, cy = c["center_px"]
        x, y, w, h = c["bbox"]

        cv2.drawContours(viz, [contour], -1, color, 2)
        cv2.circle(viz, (int(cx), int(cy)), 4, color, -1)

        text = f"Obj#{rank} d:{c['mean_depth_gray']:.1f}"
        cv2.putText(
            viz,
            text,
            (x, max(20, y - 5)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            color,
            2,
            cv2.LINE_AA
        )

    depth_bgr = cv2.cvtColor(depth_gray, cv2.COLOR_GRAY2BGR)
    combined = np.hstack([viz, depth_bgr])

    scale = 3
    combined_large = cv2.resize(
        combined,
        None,
        fx=scale,
        fy=scale,
        interpolation=cv2.INTER_NEAREST
    )

    return combined_large


def make_gqcnn_debug_viz(depth_meters, grasp_candidates):
    depth = depth_meters.copy()

    v_min = np.percentile(depth, 5)
    v_max = np.percentile(depth, 95)

    if abs(v_max - v_min) < 1e-6:
        v_min = UNITY_DEPTH_MIN_M
        v_max = UNITY_DEPTH_MAX_M

    depth_clip = np.clip(depth, v_min, v_max)
    normalized = ((depth_clip - v_min) / (v_max - v_min) * 255).astype(np.uint8)

    # 近いほど白
    viz_gray = 255 - normalized
    viz = cv2.cvtColor(viz_gray, cv2.COLOR_GRAY2BGR)

    colors = [
        (0, 0, 255),
        (0, 255, 255),
        (255, 0, 255),
    ]

    for c in grasp_candidates:
        if not c.get("success", False):
            continue

        rank = c.get("rank", 0)
        color = colors[(rank - 1) % len(colors)]

        cx = int(c["center_px"]["x"])
        cy = int(c["center_px"]["y"])
        angle = float(c["angle"])

        cv2.circle(viz, (cx, cy), 6, color, -1)

        length = 45
        p1 = (
            int(cx - length * np.sin(angle)),
            int(cy - length * np.cos(angle))
        )
        p2 = (
            int(cx + length * np.sin(angle)),
            int(cy + length * np.cos(angle))
        )

        cv2.line(viz, p1, p2, (0, 0, 0), 5)
        cv2.line(viz, p1, p2, color, 2)

        text = f"#{rank} Q:{c['q_value']:.3f}"
        cv2.putText(
            viz,
            text,
            (cx + 8, max(20, cy - 8)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            color,
            2,
            cv2.LINE_AA
        )

    scale = 3
    viz_large = cv2.resize(
        viz,
        None,
        fx=scale,
        fy=scale,
        interpolation=cv2.INTER_NEAREST
    )

    return viz_large


# =========================
# Flask API
# =========================
@app.route("/predict", methods=["POST"])
def predict():
    try:
        raw_data = request.get_data()

        if raw_data is None or len(raw_data) == 0:
            return jsonify({
                "success": False,
                "message": "受信データが空です",
                "candidates": []
            }), 400

        timestamp = time.strftime("%Y%m%d_%H%M%S")

        depth_encoding = request.headers.get("X-Depth-Encoding", "jpg")
        depth_min_m = get_float_header("X-Camera-Near-M", UNITY_DEPTH_MIN_M)
        depth_max_m = get_float_header("X-Camera-Far-M", UNITY_DEPTH_MAX_M)
        print(f"Depth decode config: encoding={depth_encoding}, near={depth_min_m:.6f}, far={depth_max_m:.6f}", flush=True)
        rgb_image, depth_image, depth_meters, rgb_bytes, depth_bytes = decode_unity_dual_images(raw_data, depth_encoding, depth_min_m, depth_max_m)

        rgb_path = os.path.join(SAVE_DIR, f"unity_rgb_{timestamp}.jpg")
        depth_ext = "exr" if depth_encoding == "exr_float" else "jpg"
        depth_path = os.path.join(SAVE_DIR, f"unity_depth_{timestamp}.{depth_ext}")
        depth_preview_path = os.path.join(SAVE_DIR, f"unity_depth_preview_{timestamp}.jpg")

        cv2.imwrite(rgb_path, rgb_image)
        if depth_encoding == "exr_float":
            with open(depth_path, "wb") as f:
                f.write(depth_bytes)
            cv2.imwrite(depth_preview_path, depth_image)
        else:
            cv2.imwrite(depth_path, depth_image)

        print("\n=== Unityから画像を受信しました ===")
        print(f"RGB:   {rgb_image.shape}, saved: {rgb_path}")
        print(f"Depth: {depth_image.shape}, encoding: {depth_encoding}, saved: {depth_path}")

        # -------------------------
        # 1. FastSAMで物体候補Top3
        # -------------------------
        top_object_candidates, fastsam_debug_viz = extract_top_object_candidates(
            rgb_image,
            depth_image,
            timestamp
        )

        fastsam_debug_path = os.path.join(SAVE_DIR, f"unity_fastsam_debug_{timestamp}.jpg")
        cv2.imwrite(fastsam_debug_path, fastsam_debug_viz)

        if len(top_object_candidates) == 0:
            return jsonify({
                "success": False,
                "message": "FastSAMで有効な物体候補がありませんでした",
                "image_width": int(rgb_image.shape[1]),
                "image_height": int(rgb_image.shape[0]),
                "fastsam_debug_image": fastsam_debug_path,
                "candidates": []
            })

        # -------------------------
        # 2. Depth JPGを仮メートル深度に変換して保存
        # -------------------------
        depth_npy_path = os.path.join(SAVE_DIR, f"unity_depth_meters_{timestamp}.npy")
        np.save(depth_npy_path, depth_meters)

        # -------------------------
        # 3. 各物体候補をDex-Netサーバーへ送る
        # -------------------------
        grasp_candidates = []

        for object_rank, obj in enumerate(top_object_candidates, start=1):
            mask_image = (obj["mask"] * 255).astype(np.uint8)
            mask_path = os.path.join(SAVE_DIR, f"candidate_{object_rank}_mask_{timestamp}.png")
            cv2.imwrite(mask_path, mask_image)

            print(f"--- Request Dex-Net for FastSAM object candidate #{object_rank} ---")

            try:
                grasp_res = request_dexnet_grasp(
                    depth_path=depth_npy_path,
                    mask_path=mask_path
                )
            except Exception as e:
                print(f"Dex-Net request failed: {e}")
                grasp_candidates.append({
                    "success": False,
                    "object_rank": object_rank,
                    "message": str(e),
                    "fastsam_score": obj["fastsam_score"],
                    "fastsam_depth_score": obj["mean_depth_gray"]
                })
                continue

            if not grasp_res.get("success", False):
                grasp_candidates.append({
                    "success": False,
                    "object_rank": object_rank,
                    "message": grasp_res.get("message", "Dex-Net failed"),
                    "fastsam_score": obj["fastsam_score"],
                    "fastsam_depth_score": obj["mean_depth_gray"]
                })
                continue

            grasp_candidates.append({
                "success": True,

                # FastSAM物体候補の順位
                "object_rank": object_rank,

                # Dex-Net / GQ-CNN把持結果
                "center_px": {
                    "x": float(grasp_res["center_x"]),
                    "y": float(grasp_res["center_y"])
                },
                "depth": float(grasp_res["depth"]),
                "angle": float(grasp_res["angle"]),
                "q_value": float(grasp_res["q_value"]),

                # FastSAM由来情報
                "fastsam_center_px": {
                    "x": float(obj["center_px"][0]),
                    "y": float(obj["center_px"][1])
                },
                "fastsam_score": float(obj["fastsam_score"]),
                "fastsam_depth_score": float(obj["mean_depth_gray"]),
                "area": int(obj["area"]),
                "bbox": {
                    "x": int(obj["bbox"][0]),
                    "y": int(obj["bbox"][1]),
                    "w": int(obj["bbox"][2]),
                    "h": int(obj["bbox"][3])
                }
            })

        # -------------------------
        # 4. Q値順に並べ替え
        # -------------------------
        valid_grasps = [c for c in grasp_candidates if c.get("success", False)]
        failed_grasps = [c for c in grasp_candidates if not c.get("success", False)]

        valid_grasps.sort(key=lambda c: c["q_value"], reverse=True)
        valid_grasps = valid_grasps[:TOP_N_GRASPS]

        for rank, c in enumerate(valid_grasps, start=1):
            c["rank"] = rank

        final_candidates = valid_grasps + failed_grasps

        gqcnn_debug_path = None

        if len(valid_grasps) > 0:
            gqcnn_debug_viz = make_gqcnn_debug_viz(depth_meters, valid_grasps)
            gqcnn_debug_path = os.path.join(SAVE_DIR, f"unity_gqcnn_debug_{timestamp}.jpg")
            cv2.imwrite(gqcnn_debug_path, gqcnn_debug_viz)

        if len(valid_grasps) == 0:
            return jsonify({
                "success": False,
                "message": "FastSAM候補はありましたが、Dex-Netが全候補で失敗しました",
                "image_width": int(rgb_image.shape[1]),
                "image_height": int(rgb_image.shape[0]),
                "fastsam_debug_image": fastsam_debug_path,
                "gqcnn_debug_image": gqcnn_debug_path,
                "candidates": final_candidates
            })

        return jsonify({
            "success": True,
            "message": "FastSAM + Dex-Net把持候補を抽出しました",
            "image_width": int(rgb_image.shape[1]),
            "image_height": int(rgb_image.shape[0]),
            "fastsam_debug_image": fastsam_debug_path,
            "gqcnn_debug_image": gqcnn_debug_path,
            "candidates": final_candidates
        })

    except Exception as e:
        print("Error:", e)
        return jsonify({
            "success": False,
            "message": str(e),
            "candidates": []
        }), 500


if __name__ == "__main__":
    print("=== Unity FastSAM Server 起動 ===")
    print(f"URL: http://{HOST}:{PORT}/predict")
    print(f"Dex-Net server: {DEXNET_SERVER_URL}")
    app.run(host=HOST, port=PORT, debug=False)
