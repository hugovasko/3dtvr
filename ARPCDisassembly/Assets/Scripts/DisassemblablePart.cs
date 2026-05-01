using System.Collections;
using UnityEngine;

/// <summary>
/// One removable part of a PC component (e.g. a CPU heatsink, a fan, a RAM stick).
/// Holds two local positions — assembled and disassembled — and animates between them
/// with an eased coroutine. Stops any in-flight animation when a new move is requested,
/// so rapid taps stay responsive.
/// </summary>
public class DisassemblablePart : MonoBehaviour
{
    [Tooltip("Local position when the component is assembled (default = current position at Awake).")]
    public Vector3 assembledLocalPosition;

    [Tooltip("Local position when the component is disassembled (exploded view).")]
    public Vector3 disassembledLocalPosition;

    [Tooltip("Seconds to move between the two positions.")]
    [Range(0.1f, 5f)] public float moveDuration = 1.2f;

    [Tooltip("Easing curve. Default = ease-in-out for natural motion.")]
    public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine activeMove;

    void Awake()
    {
        // If positions weren't explicitly set (e.g. for parts that were placed by hand
        // and never had assembledPos configured), capture the current position as the
        // assembled state. This makes the script fail-safe.
        if (assembledLocalPosition == Vector3.zero && disassembledLocalPosition == Vector3.zero)
            assembledLocalPosition = transform.localPosition;
    }

    public void MoveToAssembled() => StartMove(assembledLocalPosition);
    public void MoveToDisassembled() => StartMove(disassembledLocalPosition);

    private void StartMove(Vector3 target)
    {
        if (activeMove != null) StopCoroutine(activeMove);
        activeMove = StartCoroutine(MoveCoroutine(target));
    }

    private IEnumerator MoveCoroutine(Vector3 target)
    {
        Vector3 start = transform.localPosition;
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = easing.Evaluate(Mathf.Clamp01(elapsed / moveDuration));
            transform.localPosition = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }
        transform.localPosition = target;
        activeMove = null;
    }
}
