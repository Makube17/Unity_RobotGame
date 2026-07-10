using System;
using System.Collections;
using UnityEngine;

public class HandMoveController : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Transform initialParent;

    private void Start()
    {
        Transform root = TargetRoot;

        initialPosition = root.position;
        initialRotation = root.rotation;
        initialParent = root.parent;
    }

    [Header("移動させるハンド全体")]
    [Tooltip("簡易把持で位置移動する対象。未設定ならhandRoot、それも未設定ならこのTransformを動かします。土台を動かしたくない場合は手首/ハンド先端側のTransformを指定してください。")]
    public Transform motionRoot;
    public Transform handRoot;

    [Header("指の開閉制御")]
    public RobotHandController robotHandController;

    [Header("移動設定")]
    [Tooltip("ONにすると把持候補へハンド全体を移動し、指の開閉だけを行います。疑似把持の親子付けは行いません。")]
    public bool enableSimpleSimulatedMotion = true;
    [Tooltip("ONにすると旧クレーン式のハンド移動・開閉・疑似把持を実行します。")]
    public bool enableLegacyCraneMotion = false;
    public float moveSpeed = 0.45f;
    public float rotateSpeed = 180f;
    public float approachHeight = 0.18f;
    public float contactHeight = 0.04f;
    public float liftHeight = 0.18f;
    public float waitTime = 0.25f;

    [Header("把持回転設定")]
    [Tooltip("簡易シミュレーション移動中にハンド全体のYawをAI把持角度へ合わせます。")]
    public bool rotateRootDuringSimpleMotion = true;
    public bool rotateRootDuringGrasp = false;

    [Header("疑似把持設定")]
    public bool attachObjectOnGrip = true;
    public float attachRadius = 0.06f;
    public LayerMask graspableLayerMask;

    private bool isMoving = false;
    private GameObject grabbedObject;
    private Rigidbody grabbedRigidbody;

    public bool ShouldRunGraspMotion => enableSimpleSimulatedMotion || enableLegacyCraneMotion;
    public bool LegacyCraneMotionEnabled => enableLegacyCraneMotion;

    public void ResetHandTransform()
    {
        StopAllCoroutines();

        Transform root = TargetRoot;

        ReleaseObject();

        root.SetParent(initialParent, true);
        root.position = initialPosition;
        root.rotation = initialRotation;

        isMoving = false;

        if (robotHandController != null)
        {
            robotHandController.ResetHandPose();
        }

        Debug.Log("ハンド全体を初期位置に戻しました。");
    }

    public bool IsMoving
    {
        get { return isMoving; }
    }

    private Transform TargetRoot
    {
        get
        {
            if (motionRoot != null) return motionRoot;
            if (handRoot != null) return handRoot;
            return transform;
        }
    }

    public void MoveAndGrasp(Vector3 graspPosition, Quaternion graspRotation, Action onFinished = null)
    {
        MoveAndGrasp(graspPosition, graspRotation, graspRotation.eulerAngles.y, onFinished);
    }

    public void MoveAndGrasp(Vector3 graspPosition, Quaternion graspRotation, float graspYawDeg, Action onFinished = null)
    {
        if (isMoving)
        {
            Debug.LogWarning("ハンドはすでに移動中です。");
            return;
        }

        if (enableSimpleSimulatedMotion)
        {
            StartCoroutine(SimpleSimulatedGraspCoroutine(graspPosition, graspRotation, graspYawDeg, onFinished));
            return;
        }

        if (!enableLegacyCraneMotion)
        {
            PassGraspYawToRobotHand(graspYawDeg, "HandMoveController.MoveAndGrasp");

            Debug.Log(
                $"Legacy crane hand motion is disabled. Passed grasp yaw only: {graspYawDeg:F1} deg"
            );

            if (onFinished != null)
            {
                onFinished();
            }

            return;
        }

        StartCoroutine(MoveAndGraspCoroutine(graspPosition, graspRotation, graspYawDeg, onFinished));
    }

    private IEnumerator SimpleSimulatedGraspCoroutine(
        Vector3 graspPosition,
        Quaternion graspRotation,
        float graspYawDeg,
        Action onFinished
    )
    {
        isMoving = true;

        PassGraspYawToRobotHand(graspYawDeg, "HandMoveController.SimpleSimulatedGrasp");

        if (robotHandController != null)
        {
            robotHandController.OpenGrip();
        }

        Vector3 approachPosition = graspPosition + Vector3.up * approachHeight;
        Vector3 contactPosition = graspPosition + Vector3.up * contactHeight;

        yield return MoveTo(approachPosition, graspRotation, rotateRootDuringSimpleMotion);
        yield return new WaitForSeconds(waitTime);

        yield return MoveTo(contactPosition, graspRotation, rotateRootDuringSimpleMotion);
        yield return new WaitForSeconds(waitTime);

        if (robotHandController != null)
        {
            robotHandController.CloseGrip();
        }

        yield return new WaitForSeconds(waitTime);

        isMoving = false;

        if (onFinished != null)
        {
            onFinished();
        }
    }

    private IEnumerator MoveAndGraspCoroutine(
        Vector3 graspPosition,
        Quaternion graspRotation,
        float graspYawDeg,
        Action onFinished
    )
    {
        isMoving = true;

        PassGraspYawToRobotHand(graspYawDeg, "HandMoveController.MoveAndGraspCoroutine");

        if (robotHandController != null)
        {
            robotHandController.OpenGrip();
        }

        Vector3 approachPosition = graspPosition + Vector3.up * approachHeight;
        Vector3 contactPosition = graspPosition + Vector3.up * contactHeight;
        Vector3 liftPosition = graspPosition + Vector3.up * liftHeight;

        // 1. 把持位置の上に移動
        yield return MoveTo(approachPosition, graspRotation);

        yield return new WaitForSeconds(waitTime);

        // 2. 物体の近くまで下降
        yield return MoveTo(contactPosition, graspRotation);

        yield return new WaitForSeconds(waitTime);

        // 3. 指を閉じる
        if (robotHandController != null)
        {
            robotHandController.CloseGrip();
        }

        yield return new WaitForSeconds(waitTime);

        // 4. 近くの物体を疑似的に掴む
        if (attachObjectOnGrip)
        {
            TryAttachObject(graspPosition);
        }

        yield return new WaitForSeconds(waitTime);

        // 5. 持ち上げる
        yield return MoveTo(liftPosition, graspRotation);

        isMoving = false;

        if (onFinished != null)
        {
            onFinished();
        }
    }

    private void PassGraspYawToRobotHand(float graspYawDeg, string source)
    {
        if (robotHandController == null)
        {
            Debug.LogWarning("RobotHandController is not assigned. Could not pass grasp yaw.");
            return;
        }

        robotHandController.SetTargetGraspYaw(graspYawDeg, source);
    }

    private IEnumerator MoveTo(Vector3 targetPosition, Quaternion targetRotation)
    {
        yield return MoveTo(targetPosition, targetRotation, rotateRootDuringGrasp);
    }

    private IEnumerator MoveTo(Vector3 targetPosition, Quaternion targetRotation, bool rotateRoot)
    {
        Transform root = TargetRoot;

        while (Vector3.Distance(root.position, targetPosition) > 0.005f ||
               (rotateRoot && Quaternion.Angle(root.rotation, targetRotation) > 1f))
        {
            root.position = Vector3.MoveTowards(
                root.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (rotateRoot)
            {
                root.rotation = Quaternion.RotateTowards(
                    root.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            yield return null;
        }

        root.position = targetPosition;

        if (rotateRoot)
        {
            root.rotation = targetRotation;
        }
    }

    private void TryAttachObject(Vector3 graspPosition)
    {
        Collider[] hits = Physics.OverlapSphere(
            graspPosition,
            attachRadius,
            graspableLayerMask,
            QueryTriggerInteraction.Ignore
        );

        Collider nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Rigidbody rb = hit.GetComponentInParent<Rigidbody>();
            if (rb == null) continue;

            float distance = Vector3.Distance(graspPosition, hit.ClosestPoint(graspPosition));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = hit;
            }
        }

        if (nearest == null)
        {
            Debug.Log("把持位置の近くに掴める物体がありませんでした。");
            return;
        }

        grabbedRigidbody = nearest.GetComponentInParent<Rigidbody>();
        grabbedObject = grabbedRigidbody.gameObject;

        grabbedRigidbody.isKinematic = true;
        grabbedRigidbody.useGravity = false;

        grabbedObject.transform.SetParent(TargetRoot, true);

        Debug.Log("疑似把持しました: " + grabbedObject.name);
    }

    public void ReleaseObject()
    {
        if (grabbedObject == null) return;

        grabbedObject.transform.SetParent(null, true);

        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.isKinematic = false;
            grabbedRigidbody.useGravity = true;
        }

        grabbedObject = null;
        grabbedRigidbody = null;
    }
}
