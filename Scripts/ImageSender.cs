using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.IO;

public enum DepthUploadEncoding
{
    Jpg8Bit,
    ExrFloat
}

[RequireComponent(typeof(Camera))]
public class ImageSender : MonoBehaviour
{
    [Header("撮影に使うRenderTexture")]
    public RenderTexture rgbTexture;
    public RenderTexture depthTexture;

    [Header("深度画像生成用Shader")]
    public Shader depthCaptureShader;

    [Header("深度送信設定")]
    public DepthUploadEncoding depthUploadEncoding = DepthUploadEncoding.Jpg8Bit;
    public bool useHighPrecisionDepthTexture = true;

    [Header("Canvas上の確認用モニター")]
    public RawImage monitorRGB;
    public RawImage monitorDepth;

    [Header("Ubuntu PCのIPアドレス")]
    public string serverUrl = "http://100.77.168.49:5000/predict";

    [Header("確認用設定")]
    public bool previewEveryFrame = true;
    public bool saveImagesOnEnter = false;

    [Header("通信設定")]
    public int requestTimeoutSeconds = 120;

    private Camera simCamera;
    private RenderTexture highPrecisionDepthTexture;

    public Camera SimCamera
    {
        get { return simCamera; }
    }

    private RenderTexture ActiveDepthTexture
    {
        get
        {
            if (useHighPrecisionDepthTexture && highPrecisionDepthTexture != null)
            {
                return highPrecisionDepthTexture;
            }

            return depthTexture;
        }
    }

    void Start()
    {
        simCamera = GetComponent<Camera>();

        simCamera.depthTextureMode = DepthTextureMode.Depth;
        simCamera.enabled = false;

        EnsureHighPrecisionDepthTexture();
        AssignMonitorTextures();

        if (rgbTexture == null)
        {
            Debug.LogError("rgbTexture が未設定です。");
        }

        if (depthTexture == null)
        {
            Debug.LogError("depthTexture が未設定です。");
        }

        if (depthCaptureShader == null)
        {
            Debug.LogError("depthCaptureShader が未設定です。DepthCapture.shader を割り当ててください。");
        }
    }

    void OnDestroy()
    {
        ReleaseHighPrecisionDepthTexture();
    }

    void Update()
    {
        if (previewEveryFrame)
        {
            CaptureRGBAndDepth();
        }

        // デバッグ用：Enterキーでも送信できる
        // if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        // {
        //     SendImagesToAI(null, null);
        // }
    }

    public void SendImagesToAI(Action<string> onSuccess, Action onFailed = null)
    {
        // AI送信用は、必ず送信直前に最新状態を撮影する
        CaptureRGBAndDepth();

        if (!previewEveryFrame)
        {
            CaptureRGBAndDepth();
        }

        if (saveImagesOnEnter)
        {
            SaveDebugImages();
        }

        StartCoroutine(UploadDualImages(onSuccess, onFailed));
    }

    void EnsureHighPrecisionDepthTexture()
    {
        if (!useHighPrecisionDepthTexture || depthTexture == null)
        {
            ReleaseHighPrecisionDepthTexture();
            return;
        }

        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
        {
            Debug.LogWarning("ARGBFloat RenderTexture is not supported. Falling back to assigned depthTexture.");
            ReleaseHighPrecisionDepthTexture();
            return;
        }

        bool needsRecreate =
            highPrecisionDepthTexture == null ||
            highPrecisionDepthTexture.width != depthTexture.width ||
            highPrecisionDepthTexture.height != depthTexture.height;

        if (!needsRecreate)
        {
            return;
        }

        ReleaseHighPrecisionDepthTexture();

        highPrecisionDepthTexture = new RenderTexture(
            depthTexture.width,
            depthTexture.height,
            depthTexture.depth,
            RenderTextureFormat.ARGBFloat
        );

        highPrecisionDepthTexture.name = depthTexture.name + "_HighPrecisionRuntime";
        highPrecisionDepthTexture.filterMode = depthTexture.filterMode;
        highPrecisionDepthTexture.wrapMode = depthTexture.wrapMode;
        highPrecisionDepthTexture.useMipMap = false;
        highPrecisionDepthTexture.autoGenerateMips = false;
        highPrecisionDepthTexture.Create();
    }

    void ReleaseHighPrecisionDepthTexture()
    {
        if (highPrecisionDepthTexture == null)
        {
            return;
        }

        highPrecisionDepthTexture.Release();
        Destroy(highPrecisionDepthTexture);
        highPrecisionDepthTexture = null;
    }

    void AssignMonitorTextures()
    {
        if (monitorRGB != null)
        {
            monitorRGB.texture = rgbTexture;
        }

        if (monitorDepth != null)
        {
            monitorDepth.texture = ActiveDepthTexture;
        }
    }

    void CaptureRGBAndDepth()
    {
        EnsureHighPrecisionDepthTexture();

        RenderTexture activeDepthTexture = ActiveDepthTexture;

        if (simCamera == null || rgbTexture == null || activeDepthTexture == null)
        {
            return;
        }

        AssignMonitorTextures();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = simCamera.targetTexture;

        // RGB画像
        simCamera.targetTexture = rgbTexture;
        simCamera.Render();

        // Depth画像
        if (depthCaptureShader != null)
        {
            simCamera.targetTexture = activeDepthTexture;
            simCamera.RenderWithShader(depthCaptureShader, "");
        }

        simCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
    }

    IEnumerator UploadDualImages(Action<string> onSuccess, Action onFailed)
    {
        RenderTexture activeDepthTexture = ActiveDepthTexture;

        if (rgbTexture == null || activeDepthTexture == null)
        {
            Debug.LogError("rgbTexture または depthTexture が未設定です。");

            if (onFailed != null)
            {
                onFailed();
            }

            yield break;
        }

        byte[] rgbBytes = RenderTextureToJPG(rgbTexture);
        byte[] depthBytes = RenderTextureToDepthBytes(activeDepthTexture);

        byte[] finalDataToSend;

        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(rgbBytes.Length);
                bw.Write(rgbBytes);

                bw.Write(depthBytes.Length);
                bw.Write(depthBytes);
            }

            finalDataToSend = ms.ToArray();
        }

        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(finalDataToSend);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/octet-stream");
        request.SetRequestHeader("X-RGB-Encoding", "jpg");
        request.SetRequestHeader("X-Depth-Encoding", GetDepthEncodingHeaderValue());
        request.SetRequestHeader("X-Camera-Near-M", simCamera.nearClipPlane.ToString("R"));
        request.SetRequestHeader("X-Camera-Far-M", simCamera.farClipPlane.ToString("R"));
        request.timeout = requestTimeoutSeconds;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            Debug.Log("AIからの返答: " + responseText);

            if (onSuccess != null)
            {
                onSuccess(responseText);
            }
        }
        else
        {
            Debug.LogError("通信エラー: " + request.error);

            if (onFailed != null)
            {
                onFailed();
            }
        }

        request.Dispose();
    }

    string GetDepthEncodingHeaderValue()
    {
        switch (depthUploadEncoding)
        {
            case DepthUploadEncoding.ExrFloat:
                return "exr_float";
            case DepthUploadEncoding.Jpg8Bit:
            default:
                return "jpg";
        }
    }

    byte[] RenderTextureToDepthBytes(RenderTexture rt)
    {
        switch (depthUploadEncoding)
        {
            case DepthUploadEncoding.ExrFloat:
                return RenderTextureToEXR(rt);
            case DepthUploadEncoding.Jpg8Bit:
            default:
                return RenderTextureToJPG(rt);
        }
    }

    byte[] RenderTextureToJPG(RenderTexture rt)
    {
        RenderTexture previousActive = RenderTexture.active;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false, true);

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToJPG();

        Destroy(tex);

        RenderTexture.active = previousActive;

        return bytes;
    }

    byte[] RenderTextureToEXR(RenderTexture rt)
    {
        RenderTexture previousActive = RenderTexture.active;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

        Destroy(tex);

        RenderTexture.active = previousActive;

        return bytes;
    }

    void SaveDebugImages()
    {
        RenderTexture activeDepthTexture = ActiveDepthTexture;

        if (rgbTexture == null || activeDepthTexture == null)
        {
            return;
        }

        byte[] rgbBytes = RenderTextureToJPG(rgbTexture);
        byte[] depthBytes = RenderTextureToDepthBytes(activeDepthTexture);

        string folderPath = Application.persistentDataPath;

        string rgbPath = Path.Combine(folderPath, "debug_rgb.jpg");
        string depthExtension = depthUploadEncoding == DepthUploadEncoding.ExrFloat ? "exr" : "jpg";
        string depthPath = Path.Combine(folderPath, "debug_depth." + depthExtension);

        File.WriteAllBytes(rgbPath, rgbBytes);
        File.WriteAllBytes(depthPath, depthBytes);

        Debug.Log("RGB画像を保存しました: " + rgbPath);
        Debug.Log("Depth画像を保存しました: " + depthPath);
    }
}