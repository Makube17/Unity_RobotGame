using UnityEngine;

using System;
using System.Collections;

public class RobotHandController : MonoBehaviour
{
    [Header("AI把持角度")]
    [SerializeField] private bool hasTargetGraspYaw = false;
    [SerializeField] private float targetGraspYawDeg = 0f;
    [SerializeField] private int targetGraspYawReceiveCount = 0;
    [SerializeField] private string lastTargetGraspYawSource = "";
    [SerializeField] private float lastTargetGraspYawTime = -1f;

    public bool HasTargetGraspYaw => hasTargetGraspYaw;
    public float TargetGraspYawDeg => targetGraspYawDeg;
    public int TargetGraspYawReceiveCount => targetGraspYawReceiveCount;
    public string LastTargetGraspYawSource => lastTargetGraspYawSource;
    public float LastTargetGraspYawTime => lastTargetGraspYawTime;

    public void SetTargetGraspYaw(float yawDeg)
    {
        SetTargetGraspYaw(yawDeg, "Unknown");
    }

    public void SetTargetGraspYaw(float yawDeg, string source)
    {
        targetGraspYawDeg = NormalizeAngle180(yawDeg);
        hasTargetGraspYaw = true;
        targetGraspYawReceiveCount++;
        lastTargetGraspYawSource = source;
        lastTargetGraspYawTime = Time.time;

        Debug.Log(
            $"AI grasp yaw received by RobotHandController: {targetGraspYawDeg:F1} deg " +
            $"(source={lastTargetGraspYawSource}, count={targetGraspYawReceiveCount})"
        );
    }

    private void ClearTargetGraspYaw()
    {
        hasTargetGraspYaw = false;
        targetGraspYawDeg = 0f;
        lastTargetGraspYawSource = "";
        lastTargetGraspYawTime = -1f;
    }

    private float NormalizeAngle180(float angleDeg)
    {
        return Mathf.Repeat(angleDeg + 180f, 360f) - 180f;
    }

    [Header("土台固定キャッチモーション")]
    public bool enableFixedBaseGraspMotion = true;
    public Transform fixedBaseOrigin;
    public float fixedBaseUnityToExperimentScale = 1000f;
    [Tooltip("M9から指先/把持中心までのX方向距離[mm]。ハンド座標XはUnity上方向が負なので、手先がM9より上にある場合は負の値。")]
    public float fixedBaseL3xMm = -95f;
    public float fixedBaseL3zMm = 53f;
    [Tooltip("fixedBaseL3zMmに足す、指根本中点から指先/把持中心までのZ方向距離[mm]。")]
    public float fixedBaseFingerTipZOffsetMm = 120f;
    [Tooltip("ONの場合、fixedBaseDebugControlPointの現在位置を上下回転の初期手先ベクトルとして使います。通常はOFFにして、L3定数だけで制御します。")]
    public bool useFixedBaseControlPointForPitchReference = false;
    public float fixedBaseExperimentXOffsetMm = 0f;
    public float fixedBaseYawOffsetDeg = 0f;
    [HideInInspector] public float fixedBasePitchOffsetDeg = 60f;
    [HideInInspector] public float fixedBasePitchUnityAngleOffsetDeg = -180f;
    public bool invertFixedBaseYaw = false;
    public bool invertFixedBasePitch = false;
    public float fixedBasePrepareDuration = 0.35f;
    public float fixedBaseAimDuration = 0.6f;
    public float fixedBaseSettleBeforeCloseDuration = 0.25f;
    public float fixedBaseCloseDuration = 0.45f;
    public bool enableFixedBaseLiftAfterClose = false;
    public float fixedBaseLiftDuration = 0.45f;
    public float fixedBaseLiftPitchDeg = 0f;

    [Header("土台固定キャッチ デバッグ")]
    public bool logFixedBaseCalibrationDebug = true;
    public Transform fixedBaseDebugLeftFingerTipPoint;
    public Transform fixedBaseDebugRightFingerTipPoint;
    public Transform fixedBaseDebugControlPoint;
    [SerializeField] private bool isFixedBaseMotionRunning = false;
    [SerializeField] private bool lastFixedBaseMotionSucceeded = false;
    [SerializeField] private string lastFixedBaseMotionFailureReason = "";
    [SerializeField] private float lastFixedBaseTargetYawDeg = 0f;
    [SerializeField] private float lastFixedBaseTargetPitchDeg = 0f;
    [SerializeField] private float lastFixedBasePitchDeg = 0f;
    [SerializeField] private float lastFixedBaseClampedPitchDeg = 0f;
    [SerializeField] private float lastFixedBaseLocalGraspAngleDeg = 0f;
    [SerializeField] private Vector3 lastFixedBaseRawTargetExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseAdjustedTargetExpMm = Vector3.zero;
    [SerializeField] private float lastFixedBasePitchRawXmm = 0f;
    [SerializeField] private float lastFixedBasePitchClampedXmm = 0f;
    [SerializeField] private float lastFixedBasePitchHeightDeltaXmm = 0f;
    [SerializeField] private float lastFixedBaseEffectiveL3zMm = 0f;
    [SerializeField] private Vector3 lastFixedBaseTargetWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseOriginWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseTargetRelativeUnity = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseConfiguredTipExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugLeftTipWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugRightTipWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugTipMidWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugTipMidExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugConfiguredMinusTipMidExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugTargetMinusTipMidExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugControlPointWorldPosition = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugControlPointExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugConfiguredMinusControlPointExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBaseDebugTargetMinusControlPointExpMm = Vector3.zero;
    [SerializeField] private Vector3 lastFixedBasePitchReferenceExpMm = Vector3.zero;
    [SerializeField] private bool lastFixedBaseUsedControlPointPitchReference = false;
    [SerializeField] private float lastFixedBasePitchRadiusMm = 0f;
    [SerializeField] private float lastFixedBasePitchBiasDeg = 0f;
    [SerializeField] private float lastFixedBasePitchTargetHeightAngleDeg = 0f;
    [SerializeField] private float lastFixedBasePitchBeforeInvertDeg = 0f;
    [SerializeField] private bool lastFixedBasePitchWasInverted = false;

    public bool IsFixedBaseMotionRunning => isFixedBaseMotionRunning;
    public bool LastFixedBaseMotionSucceeded => lastFixedBaseMotionSucceeded;
    public string LastFixedBaseMotionFailureReason => lastFixedBaseMotionFailureReason;

    public Vector3 DebugWorldToFixedBaseExperiment(Vector3 worldPosition)
    {
        return WorldToExperimentFromFixedBase(worldPosition);
    }

    public void SetFixedBaseDebugFingerTipPoints(Transform leftTip, Transform rightTip)
    {
        fixedBaseDebugLeftFingerTipPoint = leftTip;
        fixedBaseDebugRightFingerTipPoint = rightTip;
    }

    public void SetFixedBaseDebugControlPoint(Transform controlPoint)
    {
        fixedBaseDebugControlPoint = controlPoint;
    }

    public void PlayFixedBaseGraspMotion(
        Vector3 graspWorldPosition,
        float graspYawDeg,
        Action onFinished = null
    )
    {
        if (!enableFixedBaseGraspMotion)
        {
            SetTargetGraspYaw(graspYawDeg, "RobotHandController.FixedBaseDisabled");

            if (onFinished != null)
            {
                onFinished();
            }

            return;
        }

        StopCoroutine(nameof(FixedBaseGraspMotionCoroutine));
        StartCoroutine(FixedBaseGraspMotionCoroutine(graspWorldPosition, graspYawDeg, onFinished));
    }

    private IEnumerator FixedBaseGraspMotionCoroutine(
        Vector3 graspWorldPosition,
        float graspYawDeg,
        Action onFinished
    )
    {
        isFixedBaseMotionRunning = true;
        lastFixedBaseMotionSucceeded = false;
        lastFixedBaseMotionFailureReason = "";

        SetTargetGraspYaw(graspYawDeg, "RobotHandController.FixedBaseGraspMotion");

        if (!HasFixedBaseMotionJointSetup(out lastFixedBaseMotionFailureReason))
        {
            isFixedBaseMotionRunning = false;

            Debug.LogWarning(
                "Fixed-base grasp motion aborted: " +
                lastFixedBaseMotionFailureReason
            );

            if (onFinished != null)
            {
                onFinished();
            }

            yield break;
        }

        Vector3 rawTargetExpMm = WorldToExperimentFromFixedBase(graspWorldPosition);
        Vector3 targetExpMm = rawTargetExpMm;
        targetExpMm.x += fixedBaseExperimentXOffsetMm;

        float targetYawDeg = CalculateFixedBaseYawDeg(targetExpMm);
        float targetPitchDeg = CalculateFixedBasePitchDeg(targetExpMm);
        float clampedPitchDeg = Mathf.Clamp(targetPitchDeg, elbowPitchMinAngle, elbowPitchMaxAngle);
        float localGraspAngleDeg = NormalizeAngle180(graspYawDeg - targetYawDeg);

        CaptureFixedBaseCalibrationDebug(graspWorldPosition, targetExpMm);

        lastFixedBaseRawTargetExpMm = rawTargetExpMm;
        lastFixedBaseAdjustedTargetExpMm = targetExpMm;
        lastFixedBaseTargetYawDeg = targetYawDeg;
        lastFixedBaseTargetPitchDeg = targetPitchDeg;
        lastFixedBaseClampedPitchDeg = clampedPitchDeg;
        lastFixedBaseLocalGraspAngleDeg = localGraspAngleDeg;

        Debug.Log(
            $"Fixed-base grasp target: rawExpMm={rawTargetExpMm}, " +
            $"adjustedExpMm={targetExpMm}, " +
            $"L3z(base/tip/reference)={fixedBaseL3zMm:F1}/{fixedBaseFingerTipZOffsetMm:F1}/{lastFixedBaseEffectiveL3zMm:F1} mm, " +
            $"pitchRef={(lastFixedBaseUsedControlPointPitchReference ? "controlPoint" : "constant")} " +
            $"{FormatVector3(lastFixedBasePitchReferenceExpMm)}, " +
            $"pitchX(raw/clamped/deltaFromL3x)={lastFixedBasePitchRawXmm:F1}/{lastFixedBasePitchClampedXmm:F1}/{lastFixedBasePitchHeightDeltaXmm:F1} mm, " +
            $"M10(yaw)={targetYawDeg:F1} deg, " +
            $"M9(pitch/clamped)={targetPitchDeg:F1}/{clampedPitchDeg:F1} deg, " +
            $"localGraspAngle={localGraspAngleDeg:F1} deg"
        );

        if (logFixedBaseCalibrationDebug)
        {
            Debug.Log(
                "Fixed-base calibration debug:\n" +
                $"targetWorld={FormatVector3(lastFixedBaseTargetWorldPosition)} " +
                $"originWorld={FormatVector3(lastFixedBaseOriginWorldPosition)} " +
                $"relativeUnity={FormatVector3(lastFixedBaseTargetRelativeUnity)}\n" +
                $"targetExp raw={FormatVector3(lastFixedBaseRawTargetExpMm)} " +
                $"adjusted={FormatVector3(lastFixedBaseAdjustedTargetExpMm)}\n" +
                $"configuredTipExp(L3)={FormatVector3(lastFixedBaseConfiguredTipExpMm)} " +
                $"actualTipMidWorld={FormatVector3(lastFixedBaseDebugTipMidWorldPosition)} " +
                $"actualTipMidExp={FormatVector3(lastFixedBaseDebugTipMidExpMm)} " +
                $"configuredMinusActualTipMidExp={FormatVector3(lastFixedBaseDebugConfiguredMinusTipMidExpMm)} " +
                $"targetMinusActualTipMidExp={FormatVector3(lastFixedBaseDebugTargetMinusTipMidExpMm)}\n" +
                $"controlPointWorld={FormatVector3(lastFixedBaseDebugControlPointWorldPosition)} " +
                $"controlPointExp={FormatVector3(lastFixedBaseDebugControlPointExpMm)} " +
                $"configuredMinusControlPointExp={FormatVector3(lastFixedBaseDebugConfiguredMinusControlPointExpMm)} " +
                $"targetMinusControlPointExp={FormatVector3(lastFixedBaseDebugTargetMinusControlPointExpMm)}\n" +
                $"pitchReference usedControlPoint={lastFixedBaseUsedControlPointPitchReference} " +
                $"exp={FormatVector3(lastFixedBasePitchReferenceExpMm)}\n" +
                $"pitchCalc radius={lastFixedBasePitchRadiusMm:F2}mm " +
                $"bias={lastFixedBasePitchBiasDeg:F2}deg " +
                $"heightAngle={lastFixedBasePitchTargetHeightAngleDeg:F2}deg " +
                $"pitchBeforeInvert={lastFixedBasePitchBeforeInvertDeg:F2}deg " +
                $"invert={lastFixedBasePitchWasInverted} " +
                $"pitch={targetPitchDeg:F2}deg clamped={clampedPitchDeg:F2}deg"
            );
        }

        yield return InterpolateFixedBasePose(
            elbowYawAngle,
            elbowPitchAngle,
            leftBaseAngle,
            leftMiddleAngle,
            rightBaseAngle,
            rightMiddleAngle,
            elbowYawAngle,
            elbowPitchAngle,
            leftBaseOpenAngle,
            middleOpenAngle,
            rightBaseOpenAngle,
            middleOpenAngle,
            fixedBasePrepareDuration
        );

        yield return InterpolateFixedBasePose(
            elbowYawAngle,
            elbowPitchAngle,
            leftBaseAngle,
            leftMiddleAngle,
            rightBaseAngle,
            rightMiddleAngle,
            targetYawDeg,
            targetPitchDeg,
            leftBaseOpenAngle,
            middleOpenAngle,
            rightBaseOpenAngle,
            middleOpenAngle,
            fixedBaseAimDuration
        );

        if (fixedBaseSettleBeforeCloseDuration > 0f)
        {
            yield return new WaitForSeconds(fixedBaseSettleBeforeCloseDuration);
        }

        yield return InterpolateFixedBasePose(
            elbowYawAngle,
            elbowPitchAngle,
            leftBaseAngle,
            leftMiddleAngle,
            rightBaseAngle,
            rightMiddleAngle,
            targetYawDeg,
            targetPitchDeg,
            leftGripCloseAngle,
            leftGripCloseAngle,
            rightGripCloseAngle,
            rightGripCloseAngle,
            fixedBaseCloseDuration
        );

        if (enableFixedBaseLiftAfterClose)
        {
            yield return InterpolateFixedBasePose(
                elbowYawAngle,
                elbowPitchAngle,
                leftBaseAngle,
                leftMiddleAngle,
                rightBaseAngle,
                rightMiddleAngle,
                targetYawDeg,
                fixedBaseLiftPitchDeg,
                leftBaseAngle,
                leftMiddleAngle,
                rightBaseAngle,
                rightMiddleAngle,
                fixedBaseLiftDuration
            );
        }

        isGripperClosed = true;
        lastFixedBaseMotionSucceeded = true;
        isFixedBaseMotionRunning = false;

        if (onFinished != null)
        {
            onFinished();
        }
    }

    private bool HasFixedBaseMotionJointSetup(out string failureReason)
    {
        if (elbowYawJoint == null && elbowPitchJoint == null)
        {
            failureReason = "左右回転関節と上下回転関節が未設定です";
            return false;
        }

        if (elbowYawJoint == null)
        {
            failureReason = "左右回転関節が未設定です";
            return false;
        }

        if (elbowPitchJoint == null)
        {
            failureReason = "上下回転関節が未設定です";
            return false;
        }

        failureReason = "";
        return true;
    }

    private IEnumerator InterpolateFixedBasePose(
        float fromYaw,
        float fromPitch,
        float fromLeftBase,
        float fromLeftMiddle,
        float fromRightBase,
        float fromRightMiddle,
        float toYaw,
        float toPitch,
        float toLeftBase,
        float toLeftMiddle,
        float toRightBase,
        float toRightMiddle,
        float duration
    )
    {
        if (duration <= 0f)
        {
            SetFixedBasePose(toYaw, toPitch, toLeftBase, toLeftMiddle, toRightBase, toRightMiddle);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);

            SetFixedBasePose(
                Mathf.Lerp(fromYaw, toYaw, smoothT),
                Mathf.Lerp(fromPitch, toPitch, smoothT),
                Mathf.Lerp(fromLeftBase, toLeftBase, smoothT),
                Mathf.Lerp(fromLeftMiddle, toLeftMiddle, smoothT),
                Mathf.Lerp(fromRightBase, toRightBase, smoothT),
                Mathf.Lerp(fromRightMiddle, toRightMiddle, smoothT)
            );

            yield return null;
        }

        SetFixedBasePose(toYaw, toPitch, toLeftBase, toLeftMiddle, toRightBase, toRightMiddle);
    }

    private void SetFixedBasePose(
        float yawDeg,
        float pitchDeg,
        float leftBaseDeg,
        float leftMiddleDeg,
        float rightBaseDeg,
        float rightMiddleDeg
    )
    {
        elbowYawAngle = Mathf.Clamp(yawDeg, elbowYawMinAngle, elbowYawMaxAngle);
        elbowPitchAngle = Mathf.Clamp(pitchDeg, elbowPitchMinAngle, elbowPitchMaxAngle);
        leftBaseAngle = Mathf.Clamp(leftBaseDeg, fingerMinAngle, fingerMaxAngle);
        leftMiddleAngle = Mathf.Clamp(leftMiddleDeg, fingerMinAngle, fingerMaxAngle);
        rightBaseAngle = Mathf.Clamp(rightBaseDeg, fingerMinAngle, fingerMaxAngle);
        rightMiddleAngle = Mathf.Clamp(rightMiddleDeg, fingerMinAngle, fingerMaxAngle);
    }

    private Vector3 WorldToExperimentFromFixedBase(Vector3 worldPosition)
    {
        Transform origin = fixedBaseOrigin != null ? fixedBaseOrigin : elbowPitchJoint;

        if (origin == null)
        {
            return Vector3.zero;
        }

        Vector3 relativeUnity = worldPosition - origin.position;

        return new Vector3(
            -relativeUnity.y,
            relativeUnity.x,
            relativeUnity.z
        ) * fixedBaseUnityToExperimentScale;
    }

    private void CaptureFixedBaseCalibrationDebug(Vector3 targetWorldPosition, Vector3 adjustedTargetExpMm)
    {
        Transform origin = fixedBaseOrigin != null ? fixedBaseOrigin : elbowPitchJoint;

        lastFixedBaseTargetWorldPosition = targetWorldPosition;

        if (origin != null)
        {
            lastFixedBaseOriginWorldPosition = origin.position;
            lastFixedBaseTargetRelativeUnity = targetWorldPosition - origin.position;
        }
        else
        {
            lastFixedBaseOriginWorldPosition = Vector3.zero;
            lastFixedBaseTargetRelativeUnity = Vector3.zero;
        }

        lastFixedBaseConfiguredTipExpMm = GetFixedBaseConfiguredTipExpMm();

        if (fixedBaseDebugLeftFingerTipPoint != null && fixedBaseDebugRightFingerTipPoint != null)
        {
            lastFixedBaseDebugLeftTipWorldPosition = fixedBaseDebugLeftFingerTipPoint.position;
            lastFixedBaseDebugRightTipWorldPosition = fixedBaseDebugRightFingerTipPoint.position;
            lastFixedBaseDebugTipMidWorldPosition =
                (lastFixedBaseDebugLeftTipWorldPosition + lastFixedBaseDebugRightTipWorldPosition) * 0.5f;
            lastFixedBaseDebugTipMidExpMm = WorldToExperimentFromFixedBase(lastFixedBaseDebugTipMidWorldPosition);
            lastFixedBaseDebugConfiguredMinusTipMidExpMm =
                lastFixedBaseConfiguredTipExpMm - lastFixedBaseDebugTipMidExpMm;
            lastFixedBaseDebugTargetMinusTipMidExpMm = adjustedTargetExpMm - lastFixedBaseDebugTipMidExpMm;
        }
        else
        {
            lastFixedBaseDebugLeftTipWorldPosition = Vector3.zero;
            lastFixedBaseDebugRightTipWorldPosition = Vector3.zero;
            lastFixedBaseDebugTipMidWorldPosition = Vector3.zero;
            lastFixedBaseDebugTipMidExpMm = Vector3.zero;
            lastFixedBaseDebugConfiguredMinusTipMidExpMm = Vector3.zero;
            lastFixedBaseDebugTargetMinusTipMidExpMm = Vector3.zero;
        }

        if (fixedBaseDebugControlPoint != null)
        {
            lastFixedBaseDebugControlPointWorldPosition = fixedBaseDebugControlPoint.position;
            lastFixedBaseDebugControlPointExpMm =
                WorldToExperimentFromFixedBase(lastFixedBaseDebugControlPointWorldPosition);
            lastFixedBaseDebugConfiguredMinusControlPointExpMm =
                lastFixedBaseConfiguredTipExpMm - lastFixedBaseDebugControlPointExpMm;
            lastFixedBaseDebugTargetMinusControlPointExpMm =
                adjustedTargetExpMm - lastFixedBaseDebugControlPointExpMm;
        }
        else
        {
            lastFixedBaseDebugControlPointWorldPosition = Vector3.zero;
            lastFixedBaseDebugControlPointExpMm = Vector3.zero;
            lastFixedBaseDebugConfiguredMinusControlPointExpMm = Vector3.zero;
            lastFixedBaseDebugTargetMinusControlPointExpMm = Vector3.zero;
        }
    }

    private float CalculateFixedBaseYawDeg(Vector3 targetExpMm)
    {
        float yawDeg = Mathf.Atan2(targetExpMm.y, targetExpMm.z) * Mathf.Rad2Deg;

        if (invertFixedBaseYaw)
        {
            yawDeg = -yawDeg;
        }

        return NormalizeAngle180(yawDeg + fixedBaseYawOffsetDeg);
    }

    private float CalculateFixedBasePitchDeg(Vector3 targetExpMm)
    {
        Vector3 pitchReferenceExpMm = GetFixedBasePitchReferenceExpMm(
            out bool usedControlPointReference
        );
        float referenceXmm = pitchReferenceExpMm.x;
        float effectiveL3zMm = pitchReferenceExpMm.z;
        lastFixedBasePitchReferenceExpMm = pitchReferenceExpMm;
        lastFixedBaseUsedControlPointPitchReference = usedControlPointReference;
        lastFixedBaseEffectiveL3zMm = effectiveL3zMm;

        float radius = Mathf.Sqrt(referenceXmm * referenceXmm + effectiveL3zMm * effectiveL3zMm);
        lastFixedBasePitchRadiusMm = radius;
        lastFixedBasePitchWasInverted = invertFixedBasePitch;

        if (radius <= 0.0001f)
        {
            lastFixedBasePitchRawXmm = targetExpMm.x;
            lastFixedBasePitchClampedXmm = targetExpMm.x;
            lastFixedBasePitchHeightDeltaXmm = 0f;
            lastFixedBasePitchBiasDeg = 0f;
            lastFixedBasePitchTargetHeightAngleDeg = 0f;
            lastFixedBasePitchBeforeInvertDeg = 0f;
            return 0f;
        }

        lastFixedBasePitchRawXmm = targetExpMm.x;
        float targetX = Mathf.Clamp(targetExpMm.x, -radius, radius);
        lastFixedBasePitchClampedXmm = targetX;
        lastFixedBasePitchHeightDeltaXmm = targetX - referenceXmm;
        float bias = Mathf.Atan2(referenceXmm, effectiveL3zMm);
        float targetHeightAngleRad = Mathf.Asin(targetX / radius);
        float targetPitchRad = bias - targetHeightAngleRad;
        float pitchBeforeInvertDeg = targetPitchRad * Mathf.Rad2Deg;
        float pitchDeg = pitchBeforeInvertDeg;

        lastFixedBasePitchBiasDeg = bias * Mathf.Rad2Deg;
        lastFixedBasePitchTargetHeightAngleDeg = targetHeightAngleRad * Mathf.Rad2Deg;
        lastFixedBasePitchBeforeInvertDeg = pitchBeforeInvertDeg;

        if (invertFixedBasePitch)
        {
            pitchDeg = -pitchDeg;
        }

        lastFixedBasePitchDeg = pitchDeg;

        return pitchDeg;
    }

    private Vector3 GetFixedBaseConfiguredTipExpMm()
    {
        return new Vector3(
            fixedBaseL3xMm,
            0f,
            fixedBaseL3zMm + fixedBaseFingerTipZOffsetMm
        );
    }

    private Vector3 GetFixedBasePitchReferenceExpMm(out bool usedControlPointReference)
    {
        usedControlPointReference = false;

        if (useFixedBaseControlPointForPitchReference && fixedBaseDebugControlPoint != null)
        {
            Vector3 controlPointExpMm =
                WorldToExperimentFromFixedBase(fixedBaseDebugControlPoint.position);

            if (Mathf.Abs(controlPointExpMm.x) > 0.0001f ||
                Mathf.Abs(controlPointExpMm.z) > 0.0001f)
            {
                usedControlPointReference = true;
                return new Vector3(controlPointExpMm.x, 0f, controlPointExpMm.z);
            }
        }

        return GetFixedBaseConfiguredTipExpMm();
    }

    private string FormatVector3(Vector3 value)
    {
        return $"({value.x:F4}, {value.y:F4}, {value.z:F4})";
    }

    public void CloseGrip()
    {
        leftBaseAngle = leftGripCloseAngle;
        leftMiddleAngle = leftGripCloseAngle;
        rightBaseAngle = rightGripCloseAngle;
        rightMiddleAngle = rightGripCloseAngle;

        isGripperClosed = true;

        Debug.Log("把持実行：グリッパーを閉じました。");
    }

    public void OpenGrip()
    {
        SetOpenPose();

        Debug.Log("グリッパーを開き姿勢にしました。");
    }

    public void ResetHandPose()
    {
        SetOpenPose();

        elbowYawAngle = 0f;
        elbowPitchAngle = 0f;
        ClearTargetGraspYaw();

        Debug.Log("ロボットハンドを初期姿勢に戻しました。");
    }

    [Header("左指の関節")]
    public Transform leftFingerBaseJoint;
    public Transform leftFingerMiddleJoint;

    [Header("右指の関節")]
    public Transform rightFingerBaseJoint;
    public Transform rightFingerMiddleJoint;

    [Header("肘関節")]
    public Transform elbowYawJoint;    // 左右回転
    public Transform elbowPitchJoint;  // 上下回転

    [Header("操作速度")]
    public float fingerRotateSpeed = 60f;
    public float elbowRotateSpeed = 45f;

    [Header("指の角度制限")]
    public float fingerMinAngle = -90f;
    public float fingerMaxAngle = 90f;

    [Header("肘 左右回転の角度制限")]
    public float elbowYawMinAngle = -60f;
    public float elbowYawMaxAngle = 60f;

    [Header("肘 上下回転の角度制限")]
    public float elbowPitchMinAngle = -30f;
    public float elbowPitchMaxAngle = 60f;

    [Header("一括開閉")]
    public float leftGripCloseAngle = 90f;
    public float rightGripCloseAngle = -90f;

    [Header("初期・開き姿勢")]
    public float leftBaseOpenAngle = -90f;
    public float rightBaseOpenAngle = 90f;
    public float middleOpenAngle = 0f;

    [Header("指の回転方向補正")]
    public float leftBaseDirection = 1f;
    public float leftMiddleDirection = 1f;
    public float rightBaseDirection = 1f;
    public float rightMiddleDirection = 1f;

    private Quaternion leftFingerBaseInitialRotation;
    private Quaternion leftFingerMiddleInitialRotation;
    private Quaternion rightFingerBaseInitialRotation;
    private Quaternion rightFingerMiddleInitialRotation;
    private Quaternion elbowYawInitialRotation;
    private Quaternion elbowPitchInitialRotation;

    [Header("現在角度確認用")]
    [SerializeField] private float leftBaseAngle = 0f;
    [SerializeField] private float leftMiddleAngle = 0f;
    [SerializeField] private float rightBaseAngle = 0f;
    [SerializeField] private float rightMiddleAngle = 0f;
    [SerializeField] private float elbowYawAngle = 0f;
    [SerializeField] private float elbowPitchAngle = 0f;

    private bool isGripperClosed = false;

    void Start()
    {
        SaveInitialRotations();

        SetOpenPose();

        elbowYawAngle = 0f;
        elbowPitchAngle = 0f;
    }

    void Update()
    {
        HandleElbowInput();
        HandleFingerInput();
        HandleShortcutInput();
    }

    void LateUpdate()
    {
        ApplyAllJointRotations();
    }

    void SaveInitialRotations()
    {
        if (leftFingerBaseJoint != null)
            leftFingerBaseInitialRotation = leftFingerBaseJoint.localRotation;

        if (leftFingerMiddleJoint != null)
            leftFingerMiddleInitialRotation = leftFingerMiddleJoint.localRotation;

        if (rightFingerBaseJoint != null)
            rightFingerBaseInitialRotation = rightFingerBaseJoint.localRotation;

        if (rightFingerMiddleJoint != null)
            rightFingerMiddleInitialRotation = rightFingerMiddleJoint.localRotation;

        if (elbowYawJoint != null)
            elbowYawInitialRotation = elbowYawJoint.localRotation;

        if (elbowPitchJoint != null)
            elbowPitchInitialRotation = elbowPitchJoint.localRotation;
    }

    void HandleElbowInput()
    {
        float elbowYawInput = 0f;
        float elbowPitchInput = 0f;

        if (Input.GetKey(KeyCode.A))
            elbowYawInput = -1f;
        else if (Input.GetKey(KeyCode.D))
            elbowYawInput = 1f;

        if (Input.GetKey(KeyCode.W))
            elbowPitchInput = 1f;
        else if (Input.GetKey(KeyCode.S))
            elbowPitchInput = -1f;

        elbowYawAngle += elbowYawInput * elbowRotateSpeed * Time.deltaTime;
        elbowPitchAngle += elbowPitchInput * elbowRotateSpeed * Time.deltaTime;

        elbowYawAngle = Mathf.Clamp(elbowYawAngle, elbowYawMinAngle, elbowYawMaxAngle);
        elbowPitchAngle = Mathf.Clamp(elbowPitchAngle, elbowPitchMinAngle, elbowPitchMaxAngle);
    }

    void HandleFingerInput()
    {
        float leftBaseInput = 0f;
        float leftMiddleInput = 0f;
        float rightBaseInput = 0f;
        float rightMiddleInput = 0f;

        // 左指 付け根：Z / X
        if (Input.GetKey(KeyCode.Z))
            leftBaseInput = -1f;
        else if (Input.GetKey(KeyCode.X))
            leftBaseInput = 1f;

        // 左指 中間：C / V
        if (Input.GetKey(KeyCode.C))
            leftMiddleInput = -1f;
        else if (Input.GetKey(KeyCode.V))
            leftMiddleInput = 1f;

        // 右指 付け根：B / N
        if (Input.GetKey(KeyCode.B))
            rightBaseInput = -1f;
        else if (Input.GetKey(KeyCode.N))
            rightBaseInput = 1f;

        // 右指 中間：M / ,
        if (Input.GetKey(KeyCode.M))
            rightMiddleInput = -1f;
        else if (Input.GetKey(KeyCode.Comma))
            rightMiddleInput = 1f;

        bool hasFingerInput =
            leftBaseInput != 0f ||
            leftMiddleInput != 0f ||
            rightBaseInput != 0f ||
            rightMiddleInput != 0f;

        if (hasFingerInput)
        {
            isGripperClosed = false;
        }

        leftBaseAngle += leftBaseInput * fingerRotateSpeed * Time.deltaTime;
        leftMiddleAngle += leftMiddleInput * fingerRotateSpeed * Time.deltaTime;
        rightBaseAngle += rightBaseInput * fingerRotateSpeed * Time.deltaTime;
        rightMiddleAngle += rightMiddleInput * fingerRotateSpeed * Time.deltaTime;

        leftBaseAngle = Mathf.Clamp(leftBaseAngle, fingerMinAngle, fingerMaxAngle);
        leftMiddleAngle = Mathf.Clamp(leftMiddleAngle, fingerMinAngle, fingerMaxAngle);
        rightBaseAngle = Mathf.Clamp(rightBaseAngle, fingerMinAngle, fingerMaxAngle);
        rightMiddleAngle = Mathf.Clamp(rightMiddleAngle, fingerMinAngle, fingerMaxAngle);
    }

    void HandleShortcutInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleGripper();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPose();
        }
    }

    void ToggleGripper()
    {
        isGripperClosed = !isGripperClosed;

        if (isGripperClosed)
        {
            leftBaseAngle = leftGripCloseAngle;
            leftMiddleAngle = leftGripCloseAngle;
            rightBaseAngle = rightGripCloseAngle;
            rightMiddleAngle = rightGripCloseAngle;

            Debug.Log("グリッパーを閉じました");
        }
        else
        {
            SetOpenPose();

            Debug.Log("グリッパーを開き姿勢にしました");
        }
    }

    void ResetPose()
    {
        leftBaseAngle = 0f;
        leftMiddleAngle = 0f;
        rightBaseAngle = 0f;
        rightMiddleAngle = 0f;
        elbowYawAngle = 0f;
        elbowPitchAngle = 0f;
        isGripperClosed = false;
        ClearTargetGraspYaw();

        Debug.Log("ロボットハンドの姿勢を初期化しました");
    }

    void ApplyAllJointRotations()
    {
        // 指関節はローカルZ軸で回転
        if (leftFingerBaseJoint != null)
        {
            leftFingerBaseJoint.localRotation =
                leftFingerBaseInitialRotation *
                Quaternion.Euler(0f, 0f, leftBaseAngle * leftBaseDirection);
        }

        if (leftFingerMiddleJoint != null)
        {
            leftFingerMiddleJoint.localRotation =
                leftFingerMiddleInitialRotation *
                Quaternion.Euler(0f, 0f, leftMiddleAngle * leftMiddleDirection);
        }

        if (rightFingerBaseJoint != null)
        {
            rightFingerBaseJoint.localRotation =
                rightFingerBaseInitialRotation *
                Quaternion.Euler(0f, 0f, rightBaseAngle * rightBaseDirection);
        }

        if (rightFingerMiddleJoint != null)
        {
            rightFingerMiddleJoint.localRotation =
                rightFingerMiddleInitialRotation *
                Quaternion.Euler(0f, 0f, rightMiddleAngle * rightMiddleDirection);
        }

        // 肘の左右回転はローカルY軸
        if (elbowYawJoint != null)
        {
            elbowYawJoint.localRotation =
                elbowYawInitialRotation *
                Quaternion.Euler(0f, elbowYawAngle, 0f);
        }

        // 肘の上下回転はローカルX軸
        if (elbowPitchJoint != null)
        {
            elbowPitchJoint.localRotation =
                elbowPitchInitialRotation *
                Quaternion.Euler(elbowPitchAngle, 0f, 0f);
        }
    }

    void SetOpenPose()
    {
        leftBaseAngle = leftBaseOpenAngle;
        leftMiddleAngle = middleOpenAngle;

        rightBaseAngle = rightBaseOpenAngle;
        rightMiddleAngle = middleOpenAngle;

        isGripperClosed = false;
    }
}

