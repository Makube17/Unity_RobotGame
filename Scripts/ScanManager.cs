using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum CandidateDepthValueMode
{
    Meters,
    Normalized01,
    InvertedNormalized01
}

public class ScanManager : MonoBehaviour
{
    [Header("参照")]
    public ObjectSpawner objectSpawner;
    public GameObject graspIndicatorPrefab;

    [Header("把持位置設定")]
    public float markerOffsetY = 0.04f;

    [Header("把持方向設定")]
    public float graspAngleY = 0f;
    public float aiGraspAngleOffsetDeg = 90f;
    public float handGraspAngleOffsetDeg = 0f;
    public float simpleHandYawCorrectionDeg = -90f;

    [Header("AI通信")]
    public ImageSender imageSender;
    public bool useAICommunication = true;

    [Header("AI候補表示設定")]
    public int maxDisplayCandidates = 3;
    public bool useCameraProjection = true;
    public bool useDepthForCandidateWorldPosition = true;
    public CandidateDepthValueMode candidateDepthValueMode = CandidateDepthValueMode.Meters;
    public float depthToUnityScale = 1.0f;
    public float depthWorldOffset = 0.0f;
    public float candidateGraspSurfaceOffsetY = -0.005f;
    public Vector3 candidateGraspWorldOffset = Vector3.zero;
    public bool useFixedCandidateMarkerHeight = true;
    public float candidateMarkerFixedWorldY = 0.08f;
    public float candidateMarkerLiftY = 0.02f;
    public Vector3 candidateMarkerVisualOffset = Vector3.zero;

    [Header("仮座標変換設定")]
    public Vector3 manualOrigin = Vector3.zero;
    public float manualScale = 0.001f;
    public float manualHeight = 0.08f;

    [Header("候補色")]
    public Material rank1Material;
    public Material rank2Material;
    public Material rank3Material;

    [Header("AI候補のRaycast補正")]
    public LayerMask graspableLayerMask;
    public float raycastMaxDistance = 5.0f;
    public float raycastFallbackSurfaceOffsetY = 0.0f;
    public float sphereCastRadius = 0.025f;

    [Header("候補選択設定")]
    public float selectedScale = 1.0f;
    public float normalScale = 1.0f;

    [Header("候補クリック選択")]
    public bool enableMouseCandidateSelection = true;
    public float candidateClickRadiusPx = 48f;
    public Camera candidateSelectionCamera;
    [Header("UI表示")]
    public TMP_Text candidateInfoText;
    public TMP_Text scoreText;
    public TMP_Text guideText;

    [Header("操作ボタン（移行用）")]
    [SerializeField] private Button scanButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button graspButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [Header("結果発表パネル")]
    public GameObject resultPanel;
    public TMP_Text resultTitleText;
    public TMP_Text resultBodyText;
    public TMP_Text nextButtonText;

    [Header("最終結果パネル")]
    public GameObject finalResultPanel;
    public TMP_Text finalTitleText;
    public TMP_Text finalBodyText;

    [Header("画面遷移")]
    public UIFlowManager uiFlowManager;

    [Header("把持実行")]
    public RobotHandController robotHandController;
    public HandMoveController handMoveController;

    [Header("把持成功判定")]
    public float graspJudgeDelay = 0.8f;
    public float graspJudgeRadius = 0.035f;
    public bool useQThreshold = false;
    public float successQThreshold = 0.10f;
    [Tooltip("提出用の簡易判定。ONの場合、Transform上の指先位置を使わず、AI候補位置の近くにある物体を把持成功対象にします。")]
    public bool submissionSimpleCandidateJudge = true;
    public float submissionCandidateJudgeRadius = 0.09f;

    [Header("指接触ベース成功判定")]
    public bool useFingerContactJudge = true;
    public bool useFingerTipPointJudge = true;
    public Transform leftFingerTipPoint;
    public Transform rightFingerTipPoint;
    public bool allowLegacyCandidateJudgeFallback = false;
    public bool requireFixedBaseMotionSuccess = true;
    public bool requireFingerTipCenterNearCandidate = true;
    public float fingerTipSearchRadius = 0.09f;
    public float fingerTipCenterTolerance = 0.065f;
    public float fingerTipCandidateTolerance = 0.08f;
    public float fingerTipAxisTolerance = 0.05f;
    public float fingerTipPerpendicularTolerance = 0.055f;
    public float fingerTipLineMargin = 0.025f;
    public bool requireBothFingerContacts = true;
    public bool allowContactBoxFallback = false;
    public float fingerContactOffset = 0.03f;
    public float fingerContactRadius = 0.04f;
    public Vector3 graspContactBoxHalfExtents = new Vector3(0.08f, 0.06f, 0.08f);
    public bool failOnLiftBlockerContact = false;
    public float liftBlockerPadding = 0.005f;
    public float maxLiftRotationChangeDeg = 45f;
    public LayerMask liftBlockerLayerMask;

    [Header("把持座標デバッグ")]
    public bool logGraspCalibrationDebug = true;
    public Transform graspControlPoint;
    [SerializeField] private int lastDebugCandidateRank = 0;
    [SerializeField] private Vector2 lastDebugDexNetCenterPx = Vector2.zero;
    [SerializeField] private Vector2 lastDebugDexNetImageSize = Vector2.zero;
    [SerializeField] private float lastDebugDexNetDepth = 0f;
    [SerializeField] private float lastDebugDexNetAngleRad = 0f;
    [SerializeField] private float lastDebugDexNetQValue = 0f;
    [SerializeField] private Vector3 lastDebugCandidateSurfaceWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugCandidateGraspWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugCandidateMarkerWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugCandidateHandRawExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastDebugCandidateHandAdjustedExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastDebugLeftFingerTipWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugRightFingerTipWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugFingerTipMidWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugFingerTipMidHandExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastDebugTargetMinusTipMidExpMm = Vector3.zero;
    [SerializeField] private float lastDebugTargetMinusTipMidUnityYmm = 0f;
    [SerializeField] private Vector3 lastDebugControlPointWorld = Vector3.zero;
    [SerializeField] private Vector3 lastDebugControlPointHandExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastDebugConfiguredMinusControlPointExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastDebugTargetMinusControlPointExpMm = Vector3.zero;
    [SerializeField] private float lastDebugTargetMinusControlPointUnityYmm = 0f;

    [Header("成功率連動スコア")]
    public float easiestQValue = 1.0f;
    public int easiestScore = 50;
    public float hardestQValue = 0.001f;
    public int hardestScore = 200;

    [Header("成功演出")]
    public bool removeObjectOnSuccess = true;
    public float successLiftHeight = 0.08f;
    public float successLiftDuration = 0.5f;
    public float successRemoveDelay = 0.4f;

    [Header("ゲームルール")]
    public int maxGraspAttempts = 5;
    public bool respawnObjectsOnNext = true;

    [Header("状態制御")]
    public float spawnSettleWait = 1.0f;

    private GameState fallbackState = GameState.Title;

    private int graspAttempts = 0;
    private bool isGameFinished = false;

    private int score = 0;
    private bool isScanning = false;
    private bool isGraspExecuting = false;

    private GameObject currentIndicator;

    private readonly List<GameObject> aiIndicators = new List<GameObject>();
    private readonly List<AIGraspCandidate> displayedCandidates = new List<AIGraspCandidate>();
    private readonly List<Vector3> displayedCandidateSurfaceWorldPositions = new List<Vector3>();
    private readonly List<Vector3> displayedCandidateWorldPositions = new List<Vector3>();
    private readonly List<Vector3> displayedCandidateMarkerWorldPositions = new List<Vector3>();

    private int selectedCandidateIndex = -1;

    private AIGraspCandidate confirmedCandidate;
    private bool hasConfirmedCandidate = false;

    private Vector3 confirmedCandidateWorldPosition;
    private bool hasConfirmedCandidateWorldPosition = false;
    private float lastExecutedGraspYawDeg = 0f;
    private bool hasLastExecutedGraspYaw = false;
    [SerializeField] private Vector3 lastFingerTipGraspCenter;
    [SerializeField] private Vector3 lastFingerTipTargetCenter;
    [SerializeField] private float lastFingerTipCenterDistance;
    [SerializeField] private float lastFingerTipAxisOffset;
    [SerializeField] private float lastFingerTipPerpendicularDistance;

    private Coroutine spawnWaitCoroutine;

    public Button ScanButtonReference => scanButton;
    public Button ResetButtonReference => resetButton;
    public Button GraspButtonReference => graspButton;
    public Button NextButtonReference => nextButton;
    public Button RetryButtonReference => retryButton;
    public Button TitleButtonReference => titleButton;
    void Start()
    {
        if (candidateInfoText != null)
        {
            candidateInfoText.raycastTarget = false;
            UpdateCandidateInfoText("スタートを押してください");
        }

        if (scoreText != null)
        {
            scoreText.raycastTarget = false;
        }

        if (guideText != null)
        {
            guideText.raycastTarget = false;
            guideText.text =
                "最適な把持候補を選んでください\n" +
                "1 / 2 / 3 : 候補を選択\n" +
                "決定キー : 決定\n" +
                "把持 : 把持を実行\n" +
                "リセット : やり直し";
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(false);
        }

        UpdateScoreText();
        SetState(GameState.Title);
    }

    void Update()
    {
        HandleCandidateSelectionInput();
    }

    private GameState CurrentState
    {
        get
        {
            if (uiFlowManager != null)
            {
                return uiFlowManager.CurrentState;
            }

            return fallbackState;
        }
    }

    private void SetState(GameState nextState)
    {
        fallbackState = nextState;

        if (uiFlowManager != null)
        {
            uiFlowManager.SetState(nextState);
        }
        else
        {
            Debug.Log("GameState: " + fallbackState);
        }
    }

    private void RefreshButtonStates()
    {
        if (uiFlowManager != null)
        {
            bool canGrasp =
                hasConfirmedCandidate &&
                !isGameFinished &&
                !isGraspExecuting;

            uiFlowManager.SetGraspAvailable(canGrasp);
            uiFlowManager.UpdateButtons();
        }
    }

    public void ScanObjects()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (CurrentState != GameState.ReadyToScan)
        {
            Debug.LogWarning("現在はスキャンできません: " + CurrentState);
            return;
        }

        if (isGameFinished)
        {
            UpdateCandidateInfoText(
                $"ゲーム終了\n" +
                $"最終スコア: {score}\n" +
                $"リセットを押してください"
            );
            return;
        }

        if (isScanning)
        {
            Debug.LogWarning("Scan is already running. Please wait.");
            return;
        }

        ResetScanResults(false);

        if (useAICommunication)
        {
            if (imageSender == null)
            {
                Debug.LogWarning("ImageSender is not assigned.");
                SetState(GameState.ReadyToScan);
                return;
            }

            Debug.Log("Sending images to AI.");
            UpdateCandidateInfoText("スキャン中...\nAIに画像を送信しています");

            isScanning = true;
            SetState(GameState.Scanning);

            imageSender.SendImagesToAI(OnAIResponseReceived, OnAIRequestFailed);
            return;
        }

        if (objectSpawner == null)
        {
            Debug.LogWarning("ObjectSpawner is not assigned.");
            SetState(GameState.ReadyToScan);
            return;
        }

        List<GameObject> objects = objectSpawner.GetSpawnedObjects();

        if (objects == null || objects.Count == 0)
        {
            Debug.LogWarning("No objects to scan.");
            SetState(GameState.ReadyToScan);
            return;
        }

        GameObject topObject = FindTopObject(objects);

        if (topObject == null)
        {
            Debug.LogWarning("Could not detect the top object.");
            SetState(GameState.ReadyToScan);
            return;
        }

        Vector3 graspPosition = GetGraspPosition(topObject);
        ShowSingleGraspIndicator(graspPosition, graspAngleY);

        Debug.Log("Top object detected: " + topObject.name);

        SetState(GameState.CandidateSelect);
    }

    public void ResetScanResults()
    {
        ResetScanResults(false);
    }

    public void ResetGame()
    {
        ResetScanResults(true);
    }

    public void ResetScanResults(bool resetScore)
    {
        if (resetScore)
        {
            score = 0;
            graspAttempts = 0;
            isGameFinished = false;
        }

        ClearAIIndicators();

        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
            currentIndicator = null;
        }

        selectedCandidateIndex = -1;

        confirmedCandidate = null;
        hasConfirmedCandidate = false;
        hasConfirmedCandidateWorldPosition = false;

        isScanning = false;
        isGraspExecuting = false;

        if (robotHandController != null && robotHandController.enableFixedBaseGraspMotion)
        {
            robotHandController.ResetHandPose();
        }
        else if (handMoveController != null)
        {
            handMoveController.ResetHandTransform();
        }
        else if (robotHandController != null)
        {
            robotHandController.ResetHandPose();
        }

        UpdateCandidateInfoText("「スキャン」ボタンを押してください");
        UpdateScoreText();
        RefreshButtonStates();

        Debug.Log("Scan result has been reset.");
    }

    void OnAIRequestFailed()
    {
        isScanning = false;

        UpdateCandidateInfoText(
            "AIが出力に失敗しました\n" +
            "もう一度スキャンをやり直してください"
        );

        SetState(GameState.ReadyToScan);

        Debug.LogWarning("AI request failed. Scan is available again.");
    }

    void OnAIResponseReceived(string json)
    {
        isScanning = false;

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("AI response is empty.");
            UpdateCandidateInfoText("AIの出力がありませんでした\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
            return;
        }

        AIGraspResponse response;

        try
        {
            response = JsonUtility.FromJson<AIGraspResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to parse AI response JSON: " + e.Message);
            Debug.LogError(json);
            UpdateCandidateInfoText("AIの返答信号がありませんでした\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
            return;
        }

        if (response == null)
        {
            Debug.LogWarning("Parsed AI response is null.");
            UpdateCandidateInfoText("AIの返答がありませんでした\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
            return;
        }

        if (!response.success)
        {
            Debug.LogWarning("AI estimation failed: " + response.message);
            ClearAIIndicators();
            UpdateCandidateInfoText("AIによる推定に失敗しました\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
            return;
        }

        if (response.candidates == null || response.candidates.Length == 0)
        {
            Debug.LogWarning("No AI candidates found.");
            ClearAIIndicators();
            UpdateCandidateInfoText("AIによる候補が見つかりませんでした\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
            return;
        }

        Debug.Log("AI candidate count: " + response.candidates.Length);
        ShowAICandidates(response);

        if (aiIndicators.Count > 0)
        {
            SetState(GameState.CandidateSelect);
            UpdateCandidateInfoText("位置候補を選択してください");
        }
        else
        {
            UpdateCandidateInfoText("位置候補が見つかりませんでした\nもう一度スキャンをやり直してください");
            SetState(GameState.ReadyToScan);
        }
    }

    void ShowAICandidates(AIGraspResponse response)
    {
        ClearAIIndicators();

        int displayCount = Mathf.Min(maxDisplayCandidates, response.candidates.Length);
        float debugImageWidth = response.image_width > 0 ? response.image_width : 512f;
        float debugImageHeight = response.image_height > 0 ? response.image_height : 512f;
        lastDebugDexNetImageSize = new Vector2(debugImageWidth, debugImageHeight);

        for (int i = 0; i < displayCount; i++)
        {
            AIGraspCandidate candidate = response.candidates[i];

            if (!candidate.success)
            {
                Debug.LogWarning("Candidate " + i + " failed: " + candidate.message);
                continue;
            }

            Vector3 surfaceWorldPosition = ConvertAICandidateToSurfaceWorld(candidate, response);
            Vector3 worldPosition = ConvertCandidateSurfacePositionToGraspPosition(surfaceWorldPosition);
            Vector3 markerWorldPosition = ConvertCandidateSurfacePositionToMarkerPosition(surfaceWorldPosition);
            Quaternion worldRotation = ConvertAIAngleToRotation(candidate.angle);

            GameObject indicator = Instantiate(
                graspIndicatorPrefab,
                markerWorldPosition,
                worldRotation
            );

            DisablePhysicsOnIndicator(indicator);

            indicator.transform.localScale = Vector3.one * normalScale;

            ApplyRankMaterial(indicator, candidate.rank);

            aiIndicators.Add(indicator);
            displayedCandidates.Add(candidate);
            displayedCandidateSurfaceWorldPositions.Add(surfaceWorldPosition);
            displayedCandidateWorldPositions.Add(worldPosition);
            displayedCandidateMarkerWorldPositions.Add(markerWorldPosition);

            Debug.Log(
                $"Candidate {candidate.rank}: " +
                $"px=({candidate.center_px.x:F1}, {candidate.center_px.y:F1}), " +
                $"depth={candidate.depth:F3}, " +
                $"surfaceWorld={surfaceWorldPosition}, " +
                $"graspWorld={worldPosition}, " +
                $"markerWorld={markerWorldPosition}, " +
                $"angle={candidate.angle:F3}, " +
                $"Q={candidate.q_value:F4}"
            );
        }

        if (aiIndicators.Count > 0)
        {
            UpdateCandidateInfoText("位置候補を選択してください");
        }
    }

    void HandleCandidateSelectionInput()
    {
        if (CurrentState != GameState.CandidateSelect)
        {
            return;
        }

        if (aiIndicators.Count == 0)
        {
            return;
        }

        if (enableMouseCandidateSelection && Input.GetMouseButtonDown(0))
        {
            TrySelectCandidateByMouse();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectCandidate(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectCandidate(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectCandidate(2);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmSelectedCandidate();
        }
    }

    void TrySelectCandidateByMouse()
    {

        Camera selectionCamera = GetCandidateSelectionCamera();

        if (selectionCamera == null)
        {
            Debug.LogWarning("Candidate selection camera is not available.");
            return;
        }

        Vector2 mousePosition = Input.mousePosition;
        int closestIndex = -1;
        float closestDistance = candidateClickRadiusPx;

        for (int i = 0; i < aiIndicators.Count; i++)
        {
            Vector3 worldPosition;

            if (i < displayedCandidateMarkerWorldPositions.Count)
            {
                worldPosition = displayedCandidateMarkerWorldPositions[i];
            }
            else if (aiIndicators[i] != null)
            {
                worldPosition = aiIndicators[i].transform.position;
            }
            else
            {
                continue;
            }

            Vector3 screenPosition = selectionCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z < 0f)
            {
                continue;
            }

            float distance = Vector2.Distance(mousePosition, new Vector2(screenPosition.x, screenPosition.y));

            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (closestIndex < 0)
        {
            return;
        }

        SelectCandidate(closestIndex);

        if (selectedCandidateIndex == closestIndex)
        {
            ConfirmSelectedCandidate();
        }
    }

    Camera GetCandidateSelectionCamera()
    {
        if (candidateSelectionCamera != null)
        {
            return candidateSelectionCamera;
        }

        if (imageSender != null && imageSender.SimCamera != null)
        {
            return imageSender.SimCamera;
        }

        return Camera.main;
    }
    void SelectCandidate(int index)
    {
        if (CurrentState != GameState.CandidateSelect)
        {
            Debug.LogWarning("現在は候補選択できません: " + CurrentState);
            return;
        }

        if (index < 0 || index >= aiIndicators.Count)
        {
            Debug.LogWarning("That candidate number does not exist.");
            return;
        }

        if (index >= displayedCandidates.Count)
        {
            Debug.LogWarning("Candidate data does not exist.");
            return;
        }

        selectedCandidateIndex = index;

        for (int i = 0; i < aiIndicators.Count; i++)
        {
            if (aiIndicators[i] == null) continue;

            if (i == selectedCandidateIndex)
            {
                aiIndicators[i].transform.localScale = Vector3.one * selectedScale;
            }
            else
            {
                aiIndicators[i].transform.localScale = Vector3.one * normalScale;
            }
        }

        AIGraspCandidate selected = displayedCandidates[selectedCandidateIndex];

        Debug.Log(
            $"Candidate {selected.rank} selected. " +
            $"Q={selected.q_value:F4}, " +
            $"angle={selected.angle:F3}, " +
            $"depth={selected.depth:F3}"
        );

        UpdateCandidateInfoText(
            $"候補 {selected.rank}\n" +
            $"成功率: {selected.q_value:F4}\n" +
            $"成功時の得点: {GetScoreByCandidateQ(selected.q_value)} 点\n" +
            $"角度: {selected.angle:F3} ラジアン\n" +
            $"深度: {selected.depth:F3}\n" +
            $"もう一度クリック、または決定キーで決定"
        );

        RefreshButtonStates();
    }

    void ConfirmSelectedCandidate()
    {
        if (CurrentState != GameState.CandidateSelect)
        {
            Debug.LogWarning("現在は候補確定できません: " + CurrentState);
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (selectedCandidateIndex < 0 || selectedCandidateIndex >= displayedCandidates.Count)
        {
            Debug.LogWarning("No candidate selected. Click a candidate first.");
            return;
        }

        AIGraspCandidate selected = displayedCandidates[selectedCandidateIndex];

        confirmedCandidate = selected;
        hasConfirmedCandidate = true;

        if (selectedCandidateIndex >= 0 && selectedCandidateIndex < displayedCandidateWorldPositions.Count)
        {
            confirmedCandidateWorldPosition = displayedCandidateWorldPositions[selectedCandidateIndex];
            hasConfirmedCandidateWorldPosition = true;
        }
        else
        {
            hasConfirmedCandidateWorldPosition = false;
        }

        RecordConfirmedGraspCalibrationDebug(selected);

        Debug.Log(
            $"Candidate {selected.rank} confirmed. " +
            $"Q={selected.q_value:F4}, " +
            $"image position=({selected.center_px.x:F1}, {selected.center_px.y:F1})"
        );

        UpdateCandidateInfoText(
            $"候補 {selected.rank} を決定しました\n" +
            $"成功率: {selected.q_value:F4}\n" +
            $"成功時の得点: {GetScoreByCandidateQ(selected.q_value)} 点\n" +
            $"「キャッチ」ボタンを押してください"
        );

        RefreshButtonStates();
    }

    void RecordConfirmedGraspCalibrationDebug(AIGraspCandidate selected)
    {
        lastDebugCandidateRank = selected.rank;
        lastDebugDexNetCenterPx = selected.center_px != null
            ? new Vector2(selected.center_px.x, selected.center_px.y)
            : Vector2.zero;
        lastDebugDexNetDepth = selected.depth;
        lastDebugDexNetAngleRad = selected.angle;
        lastDebugDexNetQValue = selected.q_value;

        if (selectedCandidateIndex >= 0 && selectedCandidateIndex < displayedCandidateSurfaceWorldPositions.Count)
        {
            lastDebugCandidateSurfaceWorld = displayedCandidateSurfaceWorldPositions[selectedCandidateIndex];
        }
        else
        {
            lastDebugCandidateSurfaceWorld = Vector3.zero;
        }

        if (selectedCandidateIndex >= 0 && selectedCandidateIndex < displayedCandidateWorldPositions.Count)
        {
            lastDebugCandidateGraspWorld = displayedCandidateWorldPositions[selectedCandidateIndex];
        }
        else
        {
            lastDebugCandidateGraspWorld = Vector3.zero;
        }

        if (selectedCandidateIndex >= 0 && selectedCandidateIndex < displayedCandidateMarkerWorldPositions.Count)
        {
            lastDebugCandidateMarkerWorld = displayedCandidateMarkerWorldPositions[selectedCandidateIndex];
        }
        else
        {
            lastDebugCandidateMarkerWorld = Vector3.zero;
        }

        PushFixedBaseDebugFingerTipReferences();
        PushFixedBaseDebugControlPointReference();

        if (robotHandController != null && hasConfirmedCandidateWorldPosition)
        {
            lastDebugCandidateHandRawExpMm =
                robotHandController.DebugWorldToFixedBaseExperiment(confirmedCandidateWorldPosition);
            lastDebugCandidateHandAdjustedExpMm = lastDebugCandidateHandRawExpMm;
            lastDebugCandidateHandAdjustedExpMm.x += robotHandController.fixedBaseExperimentXOffsetMm;
        }
        else
        {
            lastDebugCandidateHandRawExpMm = Vector3.zero;
            lastDebugCandidateHandAdjustedExpMm = Vector3.zero;
        }

        if (leftFingerTipPoint != null && rightFingerTipPoint != null)
        {
            lastDebugLeftFingerTipWorld = leftFingerTipPoint.position;
            lastDebugRightFingerTipWorld = rightFingerTipPoint.position;
            lastDebugFingerTipMidWorld =
                (lastDebugLeftFingerTipWorld + lastDebugRightFingerTipWorld) * 0.5f;

            if (robotHandController != null)
            {
                lastDebugFingerTipMidHandExpMm =
                    robotHandController.DebugWorldToFixedBaseExperiment(lastDebugFingerTipMidWorld);
                lastDebugTargetMinusTipMidExpMm =
                    lastDebugCandidateHandAdjustedExpMm - lastDebugFingerTipMidHandExpMm;
            }
            else
            {
                lastDebugFingerTipMidHandExpMm = Vector3.zero;
                lastDebugTargetMinusTipMidExpMm = Vector3.zero;
            }
        }
        else
        {
            lastDebugLeftFingerTipWorld = Vector3.zero;
            lastDebugRightFingerTipWorld = Vector3.zero;
            lastDebugFingerTipMidWorld = Vector3.zero;
            lastDebugFingerTipMidHandExpMm = Vector3.zero;
            lastDebugTargetMinusTipMidExpMm = Vector3.zero;
        }

        lastDebugTargetMinusTipMidUnityYmm =
            leftFingerTipPoint != null && rightFingerTipPoint != null
                ? (lastDebugCandidateGraspWorld.y - lastDebugFingerTipMidWorld.y) * 1000f
                : 0f;

        Transform debugControlPoint = GetDebugGraspControlPoint();

        if (debugControlPoint != null)
        {
            lastDebugControlPointWorld = debugControlPoint.position;

            if (robotHandController != null)
            {
                lastDebugControlPointHandExpMm =
                    robotHandController.DebugWorldToFixedBaseExperiment(lastDebugControlPointWorld);
                Vector3 configuredControlPointExpMm = new Vector3(
                    robotHandController.fixedBaseL3xMm,
                    0f,
                    robotHandController.fixedBaseL3zMm + robotHandController.fixedBaseFingerTipZOffsetMm
                );
                lastDebugConfiguredMinusControlPointExpMm =
                    configuredControlPointExpMm - lastDebugControlPointHandExpMm;
                lastDebugTargetMinusControlPointExpMm =
                    lastDebugCandidateHandAdjustedExpMm - lastDebugControlPointHandExpMm;
            }
            else
            {
                lastDebugControlPointHandExpMm = Vector3.zero;
                lastDebugConfiguredMinusControlPointExpMm = Vector3.zero;
                lastDebugTargetMinusControlPointExpMm = Vector3.zero;
            }

            lastDebugTargetMinusControlPointUnityYmm =
                (lastDebugCandidateGraspWorld.y - lastDebugControlPointWorld.y) * 1000f;
        }
        else
        {
            lastDebugControlPointWorld = Vector3.zero;
            lastDebugControlPointHandExpMm = Vector3.zero;
            lastDebugConfiguredMinusControlPointExpMm = Vector3.zero;
            lastDebugTargetMinusControlPointExpMm = Vector3.zero;
            lastDebugTargetMinusControlPointUnityYmm = 0f;
        }

        if (!logGraspCalibrationDebug)
        {
            return;
        }

        float debugFixedBaseXOffsetMm =
            robotHandController != null ? robotHandController.fixedBaseExperimentXOffsetMm : 0f;

        Debug.Log(
            "Confirmed grasp calibration debug:\n" +
            $"DexNet rank={lastDebugCandidateRank} " +
            $"imageSize={FormatVector2(lastDebugDexNetImageSize)} " +
            $"px={FormatVector2(lastDebugDexNetCenterPx)} " +
            $"depth={lastDebugDexNetDepth:F4}m " +
            $"angle={lastDebugDexNetAngleRad:F4}rad " +
            $"Q={lastDebugDexNetQValue:F4} " +
            $"depthMode={candidateDepthValueMode} " +
            $"depthScale={depthToUnityScale:F4} " +
            $"depthOffset={depthWorldOffset:F4}\n" +
            $"world surface={FormatVector3(lastDebugCandidateSurfaceWorld)} " +
            $"grasp={FormatVector3(lastDebugCandidateGraspWorld)} " +
            $"marker={FormatVector3(lastDebugCandidateMarkerWorld)} " +
            $"surfaceOffsetY={candidateGraspSurfaceOffsetY:F4} " +
            $"worldOffset={FormatVector3(candidateGraspWorldOffset)}\n" +
            $"handExp targetRaw={FormatVector3(lastDebugCandidateHandRawExpMm)} " +
            $"targetAdjusted={FormatVector3(lastDebugCandidateHandAdjustedExpMm)} " +
            $"fixedBaseXOffset={debugFixedBaseXOffsetMm:F1}mm\n" +
            $"fingerTips leftWorld={FormatVector3(lastDebugLeftFingerTipWorld)} " +
            $"rightWorld={FormatVector3(lastDebugRightFingerTipWorld)} " +
            $"midWorld={FormatVector3(lastDebugFingerTipMidWorld)} " +
            $"midExp={FormatVector3(lastDebugFingerTipMidHandExpMm)}\n" +
            $"targetMinusTipMidExp={FormatVector3(lastDebugTargetMinusTipMidExpMm)} " +
            $"targetMinusTipMidUnityY={lastDebugTargetMinusTipMidUnityYmm:F1}mm\n" +
            $"controlPoint world={FormatVector3(lastDebugControlPointWorld)} " +
            $"exp={FormatVector3(lastDebugControlPointHandExpMm)} " +
            $"configuredMinusControlPointExp={FormatVector3(lastDebugConfiguredMinusControlPointExpMm)} " +
            $"targetMinusControlPointExp={FormatVector3(lastDebugTargetMinusControlPointExpMm)} " +
            $"targetMinusControlPointUnityY={lastDebugTargetMinusControlPointUnityYmm:F1}mm"
        );
    }

    public void UpdateScoreTextDelayed()
    {
        StartCoroutine(UpdateScoreTextDelayedCoroutine());
    }

    IEnumerator UpdateScoreTextDelayedCoroutine()
    {
        float waitTime = 0.5f;

        if (objectSpawner != null)
        {
            waitTime = objectSpawner.GetEstimatedSpawnDuration() + spawnSettleWait;
        }

        yield return new WaitForSeconds(waitTime);

        UpdateScoreText();
    }

    public void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text =
            $"スコア: {score}\n" +
            $"試行回数: {graspAttempts} / {maxGraspAttempts}";
    }

    public void ExecuteGrasp()
    {
        if (CurrentState != GameState.CandidateSelect)
        {
            Debug.LogWarning("現在はキャッチできません: " + CurrentState);
            return;
        }

        if (isGameFinished)
        {
            UpdateCandidateInfoText(
                $"ゲーム終了\n" +
                $"最終スコア: {score}\n" +
                $"リセットを押してください"
            );
            return;
        }

        if (isGraspExecuting)
        {
            Debug.LogWarning("Grasp is already running. Please wait.");
            return;
        }

        if (!hasConfirmedCandidate || confirmedCandidate == null)
        {
            Debug.LogWarning("No grasp candidate confirmed.");

            UpdateCandidateInfoText(
                "候補が決定されていません\n" +
                "候補をクリックして\n" +
                "「キャッチ」ボタンを押してください"
            );

            return;
        }

        if (robotHandController == null)
        {
            Debug.LogWarning("RobotHandController is not assigned.");

            UpdateCandidateInfoText(
                "ロボットハンド制御が未設定です\n" +
                "管理オブジェクトに設定してください"
            );

            return;
        }

        if (!hasConfirmedCandidateWorldPosition)
        {
            Debug.LogWarning("No confirmed candidate world position.");

            UpdateCandidateInfoText(
                "候補位置がありません\n" +
                "もう一度スキャンしてください"
            );

            return;
        }

        Debug.Log(
            $"Execute grasp: Candidate {confirmedCandidate.rank} " +
            $"Q={confirmedCandidate.q_value:F4}"
        );

        graspAttempts++;
        UpdateScoreText();

        UpdateCandidateInfoText(
            $"キャッチ中...\n" +
            $"候補 {confirmedCandidate.rank}\n" +
            $"成功率: {confirmedCandidate.q_value:F4}\n" +
            $"試行回数: {graspAttempts} / {maxGraspAttempts}"
        );

        isGraspExecuting = true;
        SetState(GameState.Grasping);

        float graspYawDeg = ConvertAIAngleToYawDeg(
            confirmedCandidate.angle,
            handGraspAngleOffsetDeg + simpleHandYawCorrectionDeg
        );
        Quaternion graspRotation = Quaternion.Euler(0f, graspYawDeg, 0f);
        lastExecutedGraspYawDeg = graspYawDeg;
        hasLastExecutedGraspYaw = true;

        Debug.Log($"Execute grasp yaw passed to hand control: {graspYawDeg:F1} deg");

        if (robotHandController != null && robotHandController.enableFixedBaseGraspMotion)
        {
            PushFixedBaseDebugFingerTipReferences();
            PushFixedBaseDebugControlPointReference();

            robotHandController.PlayFixedBaseGraspMotion(
                confirmedCandidateWorldPosition,
                graspYawDeg,
                OnHandGraspMotionFinished
            );
        }
        else if (handMoveController != null && handMoveController.ShouldRunGraspMotion)
        {
            handMoveController.MoveAndGrasp(
                confirmedCandidateWorldPosition,
                graspRotation,
                graspYawDeg,
                OnHandGraspMotionFinished
            );
        }
        else
        {
            robotHandController.SetTargetGraspYaw(graspYawDeg, "ScanManager.ExecuteGrasp");

            Debug.Log(
                "Hand grasp motion is disabled or HandMoveController is not assigned. " +
                "Passed grasp yaw directly to RobotHandController."
            );

            OnHandGraspMotionFinished();
        }
    }

    void OnHandGraspMotionFinished()
    {
        if (ShouldRejectAfterFailedFixedBaseMotion())
        {
            return;
        }

        StartCoroutine(JudgeGraspAfterDelay());
    }

    bool ShouldRejectAfterFailedFixedBaseMotion()
    {
        if (!requireFixedBaseMotionSuccess)
        {
            return false;
        }

        if (robotHandController == null || !robotHandController.enableFixedBaseGraspMotion)
        {
            return false;
        }

        if (robotHandController.LastFixedBaseMotionSucceeded)
        {
            return false;
        }

        string reason = string.IsNullOrEmpty(robotHandController.LastFixedBaseMotionFailureReason)
            ? "ロボットハンドが把持位置へ到達できませんでした"
            : robotHandController.LastFixedBaseMotionFailureReason;

        UpdateCandidateInfoText(
            $"失敗...\n" +
            $"{reason}\n" +
            $"候補 {confirmedCandidate.rank} / 成功率:{confirmedCandidate.q_value:F3}"
        );

        isGraspExecuting = false;

        ShowResultPanel(
            false,
            $"{reason}\n" +
            $"候補: {confirmedCandidate.rank}\n" +
            $"成功率: {confirmedCandidate.q_value:F3}"
        );

        Debug.LogWarning(
            $"Grasp failed before judgement: {reason}, " +
            $"Candidate {confirmedCandidate.rank}, " +
            $"Q={confirmedCandidate.q_value:F4}"
        );

        UpdateScoreText();
        CheckGameClear();

        return true;
    }

    IEnumerator JudgeGraspAfterDelay()
    {
        yield return new WaitForSeconds(graspJudgeDelay);

        JudgeGraspResult();
    }

    void JudgeGraspResult()
    {
        if (!hasConfirmedCandidate || confirmedCandidate == null)
        {
            UpdateCandidateInfoText("判定できませんでした\n決定済みの候補がありません");
            Debug.LogWarning("Grasp judgement failed: confirmedCandidate is null.");

            isGraspExecuting = false;
            SetState(GameState.ReadyToScan);
            return;
        }

        if (!hasConfirmedCandidateWorldPosition)
        {
            UpdateCandidateInfoText("判定できませんでした\n候補位置がありません");
            Debug.LogWarning("Grasp judgement failed: confirmedCandidateWorldPosition is missing.");

            isGraspExecuting = false;
            SetState(GameState.ReadyToScan);
            return;
        }

        string contactFailureReason;
        GameObject targetObject = FindGraspJudgeTarget(out contactFailureReason);
        bool hasObjectNearCandidate = targetObject != null;
        bool qOK = !useQThreshold || confirmedCandidate.q_value >= successQThreshold;

        if (hasObjectNearCandidate && qOK)
        {
            string objectName = targetObject.name;

            UpdateCandidateInfoText(
                $"持ち上げ判定中...\n" +
                $"対象: {objectName}\n" +
                $"候補 {confirmedCandidate.rank} / 成功率:{confirmedCandidate.q_value:F3}"
            );

            Debug.Log(
                $"Grasp contact accepted: {objectName}, " +
                $"Candidate {confirmedCandidate.rank}, " +
                $"Q={confirmedCandidate.q_value:F4}"
            );

            StartCoroutine(ValidateLiftAndFinish(targetObject));
        }
        else
        {
            string reason = "";

            if (!hasObjectNearCandidate)
            {
                reason += contactFailureReason + " ";
            }

            if (!qOK)
            {
                reason += $"成功率が低すぎます({confirmedCandidate.q_value:F3}) ";
            }

            UpdateCandidateInfoText(
                $"失敗...\n" +
                $"{reason}\n" +
                $"候補 {confirmedCandidate.rank} / 成功率:{confirmedCandidate.q_value:F3}"
            );

            isGraspExecuting = false;

            ShowResultPanel(
                false,
                $"{reason}\n" +
                $"候補: {confirmedCandidate.rank}\n" +
                $"成功率: {confirmedCandidate.q_value:F3}"
            );

            Debug.LogWarning(
                $"Grasp failed: {reason}, " +
                $"Candidate {confirmedCandidate.rank}, " +
                $"Q={confirmedCandidate.q_value:F4}"
            );

            UpdateScoreText();
            CheckGameClear();
        }
    }

    GameObject FindGraspJudgeTarget(out string failureReason)
    {
        if (submissionSimpleCandidateJudge)
        {
            return FindNearestTargetNearCandidate(out failureReason, submissionCandidateJudgeRadius);
        }

        if (useFingerContactJudge)
        {
            if (useFingerTipPointJudge)
            {
                GameObject tipTarget = FindFingerTipPointTarget(out failureReason);

                if (tipTarget != null)
                {
                    return tipTarget;
                }

                if (!allowLegacyCandidateJudgeFallback)
                {
                    return null;
                }
            }

            return FindFingerContactTarget(out failureReason);
        }

        return FindNearestTargetNearCandidate(out failureReason, graspJudgeRadius);
    }

    GameObject FindNearestTargetNearCandidate(out string failureReason, float searchRadius)
    {
        Collider[] hits = Physics.OverlapSphere(
            confirmedCandidateWorldPosition,
            searchRadius,
            graspableLayerMask,
            QueryTriggerInteraction.Ignore
        );

        Collider nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            float distance = Vector3.Distance(
                confirmedCandidateWorldPosition,
                hit.ClosestPoint(confirmedCandidateWorldPosition)
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = hit;
            }
        }

        if (nearest == null)
        {
            failureReason = $"候補位置の近くに物体がありません(探索半径:{searchRadius:F3}m)";
            return null;
        }

        GameObject nearestRoot = GetRootGraspableObject(nearest.gameObject);

        Debug.Log(
            $"Candidate-position grasp judge accepted: " +
            $"target={(nearestRoot != null ? nearestRoot.name : nearest.gameObject.name)}, " +
            $"closestDistance={nearestDistance:F4}m, " +
            $"radius={searchRadius:F4}m, " +
            $"candidate={confirmedCandidateWorldPosition}, " +
            $"closestPoint={nearest.ClosestPoint(confirmedCandidateWorldPosition)}"
        );

        failureReason = "";
        return nearestRoot;
    }

    GameObject FindFingerContactTarget(out string failureReason)
    {
        if (!hasLastExecutedGraspYaw)
        {
            failureReason = "把持角度が取得できません";
            return null;
        }

        Quaternion graspRotation = Quaternion.Euler(0f, lastExecutedGraspYawDeg, 0f);
        Vector3 fingerAxis = graspRotation * Vector3.right;
        Vector3 rightProbeCenter = confirmedCandidateWorldPosition + fingerAxis * fingerContactOffset;
        Vector3 leftProbeCenter = confirmedCandidateWorldPosition - fingerAxis * fingerContactOffset;

        List<GameObject> rightTargets = CollectGraspableRootsInSphere(rightProbeCenter, fingerContactRadius);
        List<GameObject> leftTargets = CollectGraspableRootsInSphere(leftProbeCenter, fingerContactRadius);

        GameObject bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (GameObject rightTarget in rightTargets)
        {
            if (rightTarget == null) continue;

            bool hasLeftContact = leftTargets.Contains(rightTarget);

            if (requireBothFingerContacts && !hasLeftContact)
            {
                continue;
            }

            float distance = Vector3.Distance(
                confirmedCandidateWorldPosition,
                GetObjectBoundsCenter(rightTarget)
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = rightTarget;
            }
        }

        if (bestTarget == null && !requireBothFingerContacts)
        {
            foreach (GameObject leftTarget in leftTargets)
            {
                if (leftTarget == null || rightTargets.Contains(leftTarget)) continue;

                float distance = Vector3.Distance(
                    confirmedCandidateWorldPosition,
                    GetObjectBoundsCenter(leftTarget)
                );

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = leftTarget;
                }
            }
        }

        if (bestTarget != null)
        {
            failureReason = "";
            return bestTarget;
        }

        GameObject boxTarget = FindTargetInContactBox(graspRotation);

        if (boxTarget != null && (allowContactBoxFallback || !requireBothFingerContacts))
        {
            failureReason = "";
            return boxTarget;
        }

        failureReason = requireBothFingerContacts
            ? "左右の指が同じ物体に触れていません"
            : "指の判定範囲に物体がありません";

        return null;
    }

    GameObject FindFingerTipPointTarget(out string failureReason)
    {
        failureReason = "";
        EnsureFingerTipPointsAssigned();

        if (leftFingerTipPoint == null || rightFingerTipPoint == null)
        {
            failureReason = "左右の指先点が設定されていません。Left_FingerTipPoint / Right_FingerTipPoint をScanManagerに設定してください";
            return null;
        }

        Vector3 leftTip = leftFingerTipPoint.position;
        Vector3 rightTip = rightFingerTipPoint.position;
        Vector3 tipVector = rightTip - leftTip;
        float tipDistance = tipVector.magnitude;

        if (tipDistance <= 0.0001f)
        {
            failureReason = "左右の指先点が重なっています";
            return null;
        }

        Vector3 fingerAxis = tipVector / tipDistance;
        Vector3 graspCenter = (leftTip + rightTip) * 0.5f;
        float halfTipDistance = tipDistance * 0.5f;

        lastFingerTipGraspCenter = graspCenter;

        if (requireFingerTipCenterNearCandidate)
        {
            float candidateDistance = Vector3.Distance(graspCenter, confirmedCandidateWorldPosition);

            if (candidateDistance > fingerTipCandidateTolerance)
            {
                failureReason =
                    $"指先中心が候補位置から離れすぎています " +
                    $"({candidateDistance:F3}m / 許容:{fingerTipCandidateTolerance:F3}m)";

                Debug.LogWarning(
                    $"Finger-tip grasp judge failed: tip center is far from candidate. " +
                    $"distance={candidateDistance:F4}m, " +
                    $"center={graspCenter}, " +
                    $"candidate={confirmedCandidateWorldPosition}"
                );

                return null;
            }
        }

        List<GameObject> targets = CollectGraspableRootsInSphere(graspCenter, fingerTipSearchRadius);
        GameObject bestTarget = null;
        float bestScore = float.MaxValue;
        float bestCenterDistance = float.MaxValue;
        float bestAxisOffset = 0f;
        float bestPerpendicularDistance = 0f;
        Vector3 bestTargetCenter = Vector3.zero;

        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            Vector3 targetCenter = GetObjectBoundsCenter(target);
            Vector3 centerDelta = targetCenter - graspCenter;
            float axisOffset = Vector3.Dot(centerDelta, fingerAxis);
            Vector3 perpendicularDelta = centerDelta - fingerAxis * axisOffset;
            float perpendicularDistance = perpendicularDelta.magnitude;
            float centerDistance = centerDelta.magnitude;
            bool betweenTips = Mathf.Abs(axisOffset) <= halfTipDistance + fingerTipLineMargin;

            if (!betweenTips)
            {
                continue;
            }

            if (centerDistance > fingerTipCenterTolerance)
            {
                continue;
            }

            if (Mathf.Abs(axisOffset) > fingerTipAxisTolerance)
            {
                continue;
            }

            if (perpendicularDistance > fingerTipPerpendicularTolerance)
            {
                continue;
            }

            float score = centerDistance + Mathf.Abs(axisOffset) * 0.5f + perpendicularDistance * 0.5f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
                bestCenterDistance = centerDistance;
                bestAxisOffset = axisOffset;
                bestPerpendicularDistance = perpendicularDistance;
                bestTargetCenter = targetCenter;
            }
        }

        if (bestTarget != null)
        {
            lastFingerTipTargetCenter = bestTargetCenter;
            lastFingerTipCenterDistance = bestCenterDistance;
            lastFingerTipAxisOffset = bestAxisOffset;
            lastFingerTipPerpendicularDistance = bestPerpendicularDistance;

            Debug.Log(
                $"Finger-tip grasp judge accepted: {bestTarget.name}, " +
                $"center={bestCenterDistance:F4}m, " +
                $"axis={bestAxisOffset:F4}m, " +
                $"perp={bestPerpendicularDistance:F4}m, " +
                $"tipDistance={tipDistance:F4}m"
            );

            failureReason = "";
            return bestTarget;
        }

        lastFingerTipTargetCenter = Vector3.zero;
        lastFingerTipCenterDistance = float.MaxValue;
        lastFingerTipAxisOffset = 0f;
        lastFingerTipPerpendicularDistance = float.MaxValue;

        failureReason =
            $"指先中心の近くで掴めていません " +
            $"(中心許容:{fingerTipCenterTolerance:F3}m / 探索:{fingerTipSearchRadius:F3}m)";

        Debug.LogWarning(
            $"Finger-tip grasp judge failed: center={graspCenter}, " +
            $"tipDistance={tipDistance:F4}m"
        );

        return null;
    }

    void PushFixedBaseDebugFingerTipReferences()
    {
        if (robotHandController == null)
        {
            return;
        }

        EnsureFingerTipPointsAssigned();
        robotHandController.SetFixedBaseDebugFingerTipPoints(leftFingerTipPoint, rightFingerTipPoint);
    }

    void PushFixedBaseDebugControlPointReference()
    {
        if (robotHandController == null)
        {
            return;
        }

        robotHandController.SetFixedBaseDebugControlPoint(GetDebugGraspControlPoint());
    }

    Transform GetDebugGraspControlPoint()
    {
        if (graspControlPoint != null)
        {
            return graspControlPoint;
        }

        if (robotHandController != null && robotHandController.fixedBaseDebugControlPoint != null)
        {
            return robotHandController.fixedBaseDebugControlPoint;
        }

        return null;
    }

    void EnsureFingerTipPointsAssigned()
    {
        if (leftFingerTipPoint != null && rightFingerTipPoint != null)
        {
            return;
        }

        Transform searchRoot = null;

        if (robotHandController != null)
        {
            searchRoot = robotHandController.transform;
        }
        else if (transform.root != null)
        {
            searchRoot = transform.root;
        }

        if (searchRoot == null)
        {
            return;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);

        if (leftFingerTipPoint == null)
        {
            leftFingerTipPoint = FindFingerTipTransform(transforms, true);
        }

        if (rightFingerTipPoint == null)
        {
            rightFingerTipPoint = FindFingerTipTransform(transforms, false);
        }

        if (leftFingerTipPoint != null || rightFingerTipPoint != null)
        {
            Debug.Log(
                $"Finger tip point auto assignment: " +
                $"left={(leftFingerTipPoint != null ? leftFingerTipPoint.name : "null")}, " +
                $"right={(rightFingerTipPoint != null ? rightFingerTipPoint.name : "null")}"
            );
        }
    }

    Transform FindFingerTipTransform(Transform[] transforms, bool left)
    {
        string side = left ? "left" : "right";

        foreach (Transform t in transforms)
        {
            if (t == null) continue;

            string lowerName = t.name.ToLowerInvariant();

            if (lowerName.Contains(side) && lowerName.Contains("tip"))
            {
                return t;
            }
        }

        return null;
    }

    List<GameObject> CollectGraspableRootsInSphere(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(
            center,
            radius,
            graspableLayerMask,
            QueryTriggerInteraction.Ignore
        );

        List<GameObject> targets = new List<GameObject>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            GameObject root = GetRootGraspableObject(hit.gameObject);

            if (root != null && !targets.Contains(root))
            {
                targets.Add(root);
            }
        }

        return targets;
    }

    GameObject FindTargetInContactBox(Quaternion graspRotation)
    {
        Collider[] hits = Physics.OverlapBox(
            confirmedCandidateWorldPosition,
            graspContactBoxHalfExtents,
            graspRotation,
            graspableLayerMask,
            QueryTriggerInteraction.Ignore
        );

        GameObject bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            GameObject root = GetRootGraspableObject(hit.gameObject);
            if (root == null) continue;

            float distance = Vector3.Distance(
                confirmedCandidateWorldPosition,
                GetObjectBoundsCenter(root)
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = root;
            }
        }

        return bestTarget;
    }

    int GetScoreByCandidateQ(float qValue)
    {
        float minQ = Mathf.Min(hardestQValue, easiestQValue);
        float maxQ = Mathf.Max(hardestQValue, easiestQValue);
        float clampedQ = Mathf.Clamp(qValue, minQ, maxQ);
        float difficulty = Mathf.InverseLerp(easiestQValue, hardestQValue, clampedQ);
        float rawScore = Mathf.Lerp(easiestScore, hardestScore, difficulty);

        return Mathf.RoundToInt(rawScore);
    }

    int GetMaxPossibleScore()
    {
        int maxPerAttempt = Mathf.Max(easiestScore, hardestScore);
        return maxGraspAttempts * maxPerAttempt;
    }

    IEnumerator ValidateLiftAndFinish(GameObject targetObject)
    {
        if (targetObject == null)
        {
            isGraspExecuting = false;
            ShowResultPanel(false, "持ち上げ判定に失敗しました\n対象物を見失いました");
            CheckGameClear();
            yield break;
        }

        Rigidbody rb = targetObject.GetComponent<Rigidbody>();
        bool hadRigidbody = rb != null;
        bool previousIsKinematic = false;
        bool previousUseGravity = false;

        if (rb != null)
        {
            previousIsKinematic = rb.isKinematic;
            previousUseGravity = rb.useGravity;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Vector3 startPos = targetObject.transform.position;
        Vector3 endPos = startPos + Vector3.up * successLiftHeight;
        Quaternion startRot = targetObject.transform.rotation;
        bool blockedDuringLift = false;
        string blockedBy = "";

        float elapsed = 0f;

        while (elapsed < successLiftDuration)
        {
            if (targetObject == null)
            {
                isGraspExecuting = false;
                ShowResultPanel(false, "持ち上げ判定に失敗しました\n対象物を見失いました");
                CheckGameClear();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / successLiftDuration);

            targetObject.transform.position = Vector3.Lerp(startPos, endPos, t);

            if (failOnLiftBlockerContact && HasBlockingContact(targetObject, out blockedBy))
            {
                blockedDuringLift = true;
                break;
            }

            yield return null;
        }

        if (blockedDuringLift)
        {
            targetObject.transform.position = startPos;
            targetObject.transform.rotation = startRot;
            RestoreLiftTargetPhysics(rb, hadRigidbody, previousIsKinematic, previousUseGravity);
            FinishLiftFailure("持ち上げ中に別の物体へ接触しました: " + blockedBy);
            yield break;
        }

        if (targetObject != null)
        {
            targetObject.transform.position = endPos;
        }

        float liftDistance = Vector3.Distance(startPos, targetObject.transform.position);
        float rotationChange = Quaternion.Angle(startRot, targetObject.transform.rotation);

        if (liftDistance + 0.001f < successLiftHeight)
        {
            targetObject.transform.position = startPos;
            targetObject.transform.rotation = startRot;
            RestoreLiftTargetPhysics(rb, hadRigidbody, previousIsKinematic, previousUseGravity);
            FinishLiftFailure($"持ち上げ高さが足りません: {liftDistance:F3} m");
            yield break;
        }

        if (rotationChange > maxLiftRotationChangeDeg)
        {
            targetObject.transform.position = startPos;
            targetObject.transform.rotation = startRot;
            RestoreLiftTargetPhysics(rb, hadRigidbody, previousIsKinematic, previousUseGravity);
            FinishLiftFailure($"物体が大きく回転しました: {rotationChange:F1} 度");
            yield break;
        }

        int gainedScore = GetScoreByCandidateQ(confirmedCandidate.q_value);
        score += gainedScore;
        UpdateScoreText();

        string objectName = targetObject.name;
        string resultBody =
            $"候補: {confirmedCandidate.rank}\n" +
            $"成功率: {confirmedCandidate.q_value:F3}\n" +
            $"獲得点: +{gainedScore} 点\n" +
            $"合計スコア: {score}";

        UpdateCandidateInfoText(
            $"成功！\n" +
            $"候補 {confirmedCandidate.rank} / 成功率:{confirmedCandidate.q_value:F3}\n" +
            $"獲得点: +{gainedScore} 点\n" +
            $"合計スコア: {score}"
        );

        Debug.Log(
            $"Grasp success: {objectName}, " +
            $"Candidate {confirmedCandidate.rank}, " +
            $"Q={confirmedCandidate.q_value:F4}, " +
            $"Lift={liftDistance:F3}, " +
            $"Rotation={rotationChange:F1}, " +
            $"Gained={gainedScore}, " +
            $"Score={score}"
        );

        yield return new WaitForSeconds(successRemoveDelay);

        if (removeObjectOnSuccess && targetObject != null)
        {
            Destroy(targetObject);
        }
        else
        {
            RestoreLiftTargetPhysics(rb, hadRigidbody, previousIsKinematic, previousUseGravity);
        }

        yield return null;

        isGraspExecuting = false;

        UpdateScoreText();
        ShowResultPanel(true, resultBody);
        CheckGameClear();
    }

    bool HasBlockingContact(GameObject targetObject, out string blockerName)
    {
        blockerName = "";

        if (targetObject == null)
        {
            return false;
        }

        Bounds bounds;

        if (!TryGetObjectColliderBounds(targetObject, out bounds))
        {
            return false;
        }

        LayerMask mask = liftBlockerLayerMask.value == 0 ? graspableLayerMask : liftBlockerLayerMask;
        Vector3 halfExtents = bounds.extents + Vector3.one * liftBlockerPadding;

        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            Quaternion.identity,
            mask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            GameObject root = GetRootGraspableObject(hit.gameObject);

            if (root == null || root == targetObject)
            {
                continue;
            }

            blockerName = root.name;
            return true;
        }

        return false;
    }

    void RestoreLiftTargetPhysics(Rigidbody rb, bool hadRigidbody, bool wasKinematic, bool usedGravity)
    {
        if (!hadRigidbody || rb == null)
        {
            return;
        }

        rb.isKinematic = wasKinematic;
        rb.useGravity = usedGravity;
    }

    void FinishLiftFailure(string reason)
    {
        UpdateCandidateInfoText(
            $"失敗...\n" +
            $"{reason}\n" +
            $"候補 {confirmedCandidate.rank} / 成功率:{confirmedCandidate.q_value:F3}"
        );

        isGraspExecuting = false;

        ShowResultPanel(
            false,
            $"{reason}\n" +
            $"候補: {confirmedCandidate.rank}\n" +
            $"成功率: {confirmedCandidate.q_value:F3}"
        );

        Debug.LogWarning(
            $"Grasp failed during lift: {reason}, " +
            $"Candidate {confirmedCandidate.rank}, " +
            $"Q={confirmedCandidate.q_value:F4}"
        );

        UpdateScoreText();
        CheckGameClear();
    }

    Vector3 GetObjectBoundsCenter(GameObject targetObject)
    {
        Bounds bounds;

        if (TryGetObjectColliderBounds(targetObject, out bounds))
        {
            return bounds.center;
        }

        return targetObject != null ? targetObject.transform.position : Vector3.zero;
    }

    bool TryGetObjectColliderBounds(GameObject targetObject, out Bounds bounds)
    {
        bounds = new Bounds();

        if (targetObject == null)
        {
            return false;
        }

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
        bool hasBounds = false;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }

    void CheckGameClear()
    {
        if (graspAttempts >= maxGraspAttempts)
        {
            isGameFinished = true;

            UpdateCandidateInfoText(
                $"ゲーム終了\n" +
                $"最終スコア: {score}\n"
            );

            Debug.Log($"Game end. Final score: {score}");
        }
    }

    GameObject GetRootGraspableObject(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return null;
        }

        Rigidbody rb = hitObject.GetComponentInParent<Rigidbody>();

        if (rb != null)
        {
            return rb.gameObject;
        }

        return hitObject.transform.root.gameObject;
    }

    void DisablePhysicsOnIndicator(GameObject indicator)
    {
        if (indicator == null) return;

        Collider[] colliders = indicator.GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = indicator.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    Vector3 ConvertAICandidateToSurfaceWorld(AIGraspCandidate candidate, AIGraspResponse response)
    {
        float imageWidth = response.image_width;
        float imageHeight = response.image_height;

        if (imageWidth <= 0f) imageWidth = 512f;
        if (imageHeight <= 0f) imageHeight = 512f;

        float viewportX = candidate.center_px.x / imageWidth;
        float viewportY = 1.0f - (candidate.center_px.y / imageHeight);

        if (useCameraProjection && imageSender != null && imageSender.SimCamera != null)
        {
            Camera cam = imageSender.SimCamera;

            if (useDepthForCandidateWorldPosition)
            {
                float worldDepth = ConvertCandidateDepthToCameraDistance(candidate.depth, cam);

                if (worldDepth > 0f)
                {
                    Vector3 viewportPos = new Vector3(viewportX, viewportY, worldDepth);
                    Vector3 worldPos = cam.ViewportToWorldPoint(viewportPos);

                    Debug.Log(
                        $"Depth candidate world position: rank={candidate.rank}, " +
                        $"viewport=({viewportX:F3}, {viewportY:F3}), " +
                        $"depth={candidate.depth:F4}, " +
                        $"depthMode={candidateDepthValueMode}, " +
                        $"worldDepth={worldDepth:F4}, " +
                        $"world={worldPos}"
                    );

                    return worldPos;
                }

                Debug.LogWarning(
                    $"Invalid depth for AI candidate rank={candidate.rank}: {candidate.depth:F4}. Falling back to raycast."
                );
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));

            RaycastHit hit;

            if (Physics.SphereCast(ray, sphereCastRadius, out hit, raycastMaxDistance, graspableLayerMask))
            {
                Vector3 hitPosition = hit.point;
                hitPosition.y += raycastFallbackSurfaceOffsetY;

                Debug.Log(
                    $"Raycast candidate surface position: rank={candidate.rank}, " +
                    $"hit={hit.collider.name}, " +
                    $"point={hit.point}, " +
                    $"surfaceWorld={hitPosition}"
                );

                return hitPosition;
            }

            if (Physics.Raycast(ray, out hit, raycastMaxDistance, graspableLayerMask))
            {
                Vector3 hitPosition = hit.point;
                hitPosition.y += raycastFallbackSurfaceOffsetY;

                Debug.Log(
                    $"Raycast candidate surface position: rank={candidate.rank}, " +
                    $"hit={hit.collider.name}, " +
                    $"point={hit.point}, " +
                    $"surfaceWorld={hitPosition}"
                );

                return hitPosition;
            }

            Debug.LogWarning(
                $"Raycast missed for AI candidate rank={candidate.rank}. Viewport=({viewportX:F2}, {viewportY:F2})"
            );

            if (candidate.fastsam_center_px != null)
            {
                float fallbackViewportX = candidate.fastsam_center_px.x / imageWidth;
                float fallbackViewportY = 1.0f - (candidate.fastsam_center_px.y / imageHeight);

                Ray fallbackRay = cam.ViewportPointToRay(new Vector3(fallbackViewportX, fallbackViewportY, 0f));

                if (Physics.SphereCast(fallbackRay, sphereCastRadius, out hit, raycastMaxDistance, graspableLayerMask))
                {
                    Vector3 hitPosition = hit.point;
                    hitPosition.y += raycastFallbackSurfaceOffsetY;
                    return hitPosition;
                }

                if (Physics.Raycast(fallbackRay, out hit, raycastMaxDistance, graspableLayerMask))
                {
                    Vector3 hitPosition = hit.point;
                    hitPosition.y += raycastFallbackSurfaceOffsetY;
                    return hitPosition;
                }
            }

            Debug.LogWarning(
                $"Fallback raycast also missed for AI candidate rank={candidate.rank}. Using depth-based candidate position."
            );

            float fallbackDepth = ConvertCandidateDepthToCameraDistance(candidate.depth, cam);
            Vector3 fallbackViewportPos = new Vector3(viewportX, viewportY, fallbackDepth);
            Vector3 fallbackWorldPos = cam.ViewportToWorldPoint(fallbackViewportPos);

            return fallbackWorldPos;
        }
        else
        {
            float x = (candidate.center_px.x - imageWidth / 2.0f) * manualScale;
            float z = -(candidate.center_px.y - imageHeight / 2.0f) * manualScale;

            return manualOrigin + new Vector3(x, manualHeight, z);
        }
    }

    Vector3 ConvertCandidateSurfacePositionToGraspPosition(Vector3 surfaceWorldPosition)
    {
        return surfaceWorldPosition + Vector3.up * candidateGraspSurfaceOffsetY + candidateGraspWorldOffset;
    }

    Vector3 ConvertCandidateSurfacePositionToMarkerPosition(Vector3 surfaceWorldPosition)
    {
        Vector3 markerPosition = surfaceWorldPosition + Vector3.up * candidateMarkerLiftY + candidateMarkerVisualOffset;

        if (useFixedCandidateMarkerHeight)
        {
            markerPosition.y = candidateMarkerFixedWorldY;
        }

        return markerPosition;
    }

    float ConvertCandidateDepthToCameraDistance(float candidateDepth, Camera cam)
    {
        float cameraDistance;

        switch (candidateDepthValueMode)
        {
            case CandidateDepthValueMode.Normalized01:
                cameraDistance = Mathf.Lerp(cam.nearClipPlane, cam.farClipPlane, Mathf.Clamp01(candidateDepth));
                break;

            case CandidateDepthValueMode.InvertedNormalized01:
                cameraDistance = Mathf.Lerp(cam.nearClipPlane, cam.farClipPlane, 1f - Mathf.Clamp01(candidateDepth));
                break;

            case CandidateDepthValueMode.Meters:
            default:
                cameraDistance = candidateDepth;
                break;
        }

        return cameraDistance * depthToUnityScale + depthWorldOffset;
    }

    float ConvertAIAngleToYawDeg(float angleRad, float additionalOffsetDeg = 0f)
    {
        return -angleRad * Mathf.Rad2Deg + aiGraspAngleOffsetDeg + additionalOffsetDeg;
    }

    Quaternion ConvertAIAngleToRotation(float angleRad, float additionalOffsetDeg = 0f)
    {
        return Quaternion.Euler(0f, ConvertAIAngleToYawDeg(angleRad, additionalOffsetDeg), 0f);
    }

    void ApplyRankMaterial(GameObject indicator, int rank)
    {
        Material mat = null;

        if (rank == 1)
        {
            mat = rank1Material;
        }
        else if (rank == 2)
        {
            mat = rank2Material;
        }
        else if (rank == 3)
        {
            mat = rank3Material;
        }

        if (mat == null)
        {
            return;
        }

        Renderer[] renderers = indicator.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            r.material = mat;
        }
    }

    void ClearAIIndicators()
    {
        foreach (GameObject indicator in aiIndicators)
        {
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }

        aiIndicators.Clear();
        displayedCandidates.Clear();
        displayedCandidateSurfaceWorldPositions.Clear();
        displayedCandidateWorldPositions.Clear();
        displayedCandidateMarkerWorldPositions.Clear();
        selectedCandidateIndex = -1;
    }

    GameObject FindTopObject(List<GameObject> objects)
    {
        GameObject topObject = null;
        float highestY = float.MinValue;

        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            Renderer renderer = obj.GetComponent<Renderer>();

            float y;

            if (renderer != null)
            {
                y = renderer.bounds.max.y;
            }
            else
            {
                y = obj.transform.position.y;
            }

            if (y > highestY)
            {
                highestY = y;
                topObject = obj;
            }
        }

        return topObject;
    }

    Vector3 GetGraspPosition(GameObject targetObject)
    {
        Renderer renderer = targetObject.GetComponent<Renderer>();

        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;

            return new Vector3(
                bounds.center.x,
                bounds.max.y + markerOffsetY,
                bounds.center.z
            );
        }

        Vector3 position = targetObject.transform.position;
        position.y += markerOffsetY;
        return position;
    }

    void ShowSingleGraspIndicator(Vector3 graspPosition, float angleY)
    {
        if (graspIndicatorPrefab == null)
        {
            Debug.LogWarning("graspIndicatorPrefab is not assigned.");
            return;
        }

        Quaternion indicatorRotation = Quaternion.Euler(0f, angleY, 0f);

        if (currentIndicator == null)
        {
            currentIndicator = Instantiate(
                graspIndicatorPrefab,
                graspPosition,
                indicatorRotation
            );

            DisablePhysicsOnIndicator(currentIndicator);
        }
        else
        {
            currentIndicator.transform.position = graspPosition;
            currentIndicator.transform.rotation = indicatorRotation;
            currentIndicator.SetActive(true);

            DisablePhysicsOnIndicator(currentIndicator);
        }
    }

    void UpdateCandidateInfoText(string message)
    {
        if (candidateInfoText != null)
        {
            candidateInfoText.text = message;
        }
    }

    void ShowResultPanel(bool success, string bodyMessage)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(false);
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = "キャッチ結果";
        }

        if (resultBodyText != null)
        {
            string resultText = success ? "成功！" : "失敗...";

            resultBodyText.text =
                resultText + "\n\n" +
                bodyMessage + "\n\n" +
                $"スコア: {score}";
        }

        if (nextButtonText != null)
        {
            nextButtonText.text =
                graspAttempts >= maxGraspAttempts ? "最終結果へ" : "次へ";
        }

        SetState(GameState.Result);
    }

    public void OnNextButtonPressed()
    {
        if (CurrentState != GameState.Result)
        {
            Debug.LogWarning("現在は次へ進めません: " + CurrentState);
            return;
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (graspAttempts >= maxGraspAttempts)
        {
            isGameFinished = true;
            ShowFinalResultPanel();
            return;
        }

        if (respawnObjectsOnNext)
        {
            StartRespawnSequence(false);
        }
        else
        {
            UpdateCandidateInfoText("次のラウンドを始めるには「スキャン」を押してください");
            SetState(GameState.ReadyToScan);
        }

        Debug.Log("次へボタンが押されました。次のラウンドへ進みます。");
    }

    void ShowFinalResultPanel()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(true);
        }

        if (finalTitleText != null)
        {
            finalTitleText.text = "最終結果";
        }

        if (finalBodyText != null)
        {
            finalBodyText.text =
                $"最終スコア: {score}\n\n" +
                $"試行回数: {graspAttempts} / {maxGraspAttempts}\n\n" +
                GetFinalComment(score);
        }

        SetState(GameState.FinalResult);
    }

    string GetFinalComment(int finalScore)
    {
        int maxScore = GetMaxPossibleScore();

        if (finalScore >= maxScore * 0.8f)
        {
            return "Excellent!";
        }
        else if (finalScore >= maxScore * 0.5f)
        {
            return "Good job!";
        }
        else
        {
            return "Nice try!";
        }
    }

    public void ResetGameAndRespawnObjects()
    {
        StartRespawnSequence(true);
    }

    public void ResetGameAndClearObjects()
    {
        StopSpawnWaitCoroutine();

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(false);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        ResetGame();

        if (objectSpawner != null)
        {
            objectSpawner.ClearObjects();
        }
        else
        {
            Debug.LogWarning("ObjectSpawner is not assigned.");
        }

        UpdateCandidateInfoText("スタートを押してください");

        SetState(GameState.Title);

        Debug.Log("ゲームをリセットして物体を削除しました。");
    }

    public void StartNewGameFromTitle()
    {
        StartRespawnSequence(true);

        Debug.Log("タイトル画面から新しいゲームを開始しました。");
    }

    public void OnRetryButtonPressed()
    {
        if (CurrentState != GameState.FinalResult)
        {
            Debug.LogWarning("現在はリトライできません: " + CurrentState);
            return;
        }

        ResetGameAndRespawnObjects();

        Debug.Log("もう一度ボタンが押されました。");
    }

    public void OnTitleButtonPressed()
    {
        if (CurrentState != GameState.FinalResult)
        {
            Debug.LogWarning("現在はタイトルへ戻れません: " + CurrentState);
            return;
        }

        ResetGameAndClearObjects();

        if (uiFlowManager != null)
        {
            uiFlowManager.ShowTitle();
        }
        else
        {
            Debug.LogWarning("UIFlowManager is not assigned.");
        }

        Debug.Log("タイトルへ戻りました。");
    }

    private void StartRespawnSequence(bool resetScore)
    {
        StopSpawnWaitCoroutine();

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(false);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        ResetScanResults(resetScore);

        if (objectSpawner != null)
        {
            objectSpawner.SpawnObjects();
        }
        else
        {
            Debug.LogWarning("ObjectSpawner is not assigned.");
        }

        UpdateCandidateInfoText("物体を積み直しています...\nしばらくお待ちください");

        SetState(GameState.Spawning);

        spawnWaitCoroutine = StartCoroutine(WaitForSpawnAndSetReady());

        Debug.Log("物体の積み直しを開始しました。");
    }

    private IEnumerator WaitForSpawnAndSetReady()
    {
        float timeout = spawnSettleWait + 5.0f;

        if (objectSpawner != null)
        {
            timeout += objectSpawner.GetEstimatedSpawnDuration();
        }

        float elapsed = 0f;

        // ObjectSpawner が生成中なら待つ
        while (objectSpawner != null && objectSpawner.IsSpawning)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= timeout)
            {
                Debug.LogWarning(
                    "ObjectSpawner の生成完了待ちがタイムアウトしました。強制的にスキャン可能状態へ進みます。"
                );
                break;
            }

            yield return null;
        }

        // 生成完了後、物理的に少し落ち着くまで待つ
        yield return new WaitForSeconds(spawnSettleWait);

        spawnWaitCoroutine = null;

        if (CurrentState != GameState.Spawning)
        {
            yield break;
        }

        UpdateScoreText();

        UpdateCandidateInfoText("「スキャン」を押してください");

        SetState(GameState.ReadyToScan);

        Debug.Log("物体配置が完了し、スキャン可能になりました。");
    }

    private string FormatVector2(Vector2 value)
    {
        return $"({value.x:F2}, {value.y:F2})";
    }

    private string FormatVector3(Vector3 value)
    {
        return $"({value.x:F4}, {value.y:F4}, {value.z:F4})";
    }

    private void StopSpawnWaitCoroutine()
    {
        if (spawnWaitCoroutine != null)
        {
            StopCoroutine(spawnWaitCoroutine);
            spawnWaitCoroutine = null;
        }
    }
}

[Serializable]
public class AIGraspResponse
{
    public bool success;
    public string message;
    public int image_width;
    public int image_height;
    public string fastsam_debug_image;
    public string gqcnn_debug_image;
    public AIGraspCandidate[] candidates;
}

[Serializable]
public class AIGraspCandidate
{
    public bool success;
    public int rank;
    public int object_rank;
    public PixelPoint center_px;
    public float depth;
    public float angle;
    public float q_value;
    public PixelPoint fastsam_center_px;
    public float fastsam_score;
    public float fastsam_depth_score;
    public int area;
    public BoundingBox bbox;
    public string message;
}

[Serializable]
public class PixelPoint
{
    public float x;
    public float y;
}

[Serializable]
public class BoundingBox
{
    public int x;
    public int y;
    public int w;
    public int h;
}

