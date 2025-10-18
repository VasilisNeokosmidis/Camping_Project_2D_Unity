using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class DeliveryTruckController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float arriveDistance = 0.08f;

    [Header("Directional Sprites (one active)")]
    [SerializeField] private GameObject truck_goes_right;
    [SerializeField] private GameObject truck_goes_left;
    [SerializeField] private GameObject truck_goes_down;   // idle
    [SerializeField] private GameObject truck_goes_up;

    Rigidbody2D rb;
    Coroutine moveCo;

    Vector2 startPos;
    bool lockFacingDown;

    // Pause state
    bool isPaused;

    // Global registry of live trucks (for optional mass ops)
    static readonly HashSet<DeliveryTruckController> Live = new HashSet<DeliveryTruckController>();

    public float Speed => speed;
    public bool IsDelivering => moveCo != null;

    void OnEnable()
    {
        Live.Add(this);
        DeliveryWeatherPause.OnRainStarted += HandleRainStart;
        DeliveryWeatherPause.OnRainStopped += HandleRainStop;
    }

    void OnDisable()
    {
        Live.Remove(this);
        DeliveryWeatherPause.OnRainStarted -= HandleRainStart;
        DeliveryWeatherPause.OnRainStopped  -= HandleRainStop;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        EnableOnly(truck_goes_down);
    }

    void HandleRainStart()  => SetPaused(true);
    void HandleRainStop()   => SetPaused(false);

    public void SetPaused(bool paused)
    {
        isPaused = paused;
#if UNITY_2021_3_OR_NEWER
        if (paused && rb) rb.linearVelocity = Vector2.zero;
#endif
    }

    public static void PauseAll(bool paused)
    {
        foreach (var t in Live) t.SetPaused(paused);
    }

    public void PrimeAtDepot(Vector2 pos)
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
#if UNITY_2021_3_OR_NEWER
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
#endif
        rb.position = pos;
        startPos = pos;
        lockFacingDown = true;
        EnableOnly(truck_goes_down);
    }

    public void DeliverAndReturn(List<Vector3> path, Action onReachedTent, Action onComplete)
    {
        if (path == null || path.Count == 0) { onComplete?.Invoke(); return; }
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = StartCoroutine(DeliverAndReturnCo(path, onReachedTent, onComplete));
    }

    IEnumerator DeliverAndReturnCo(List<Vector3> path, Action onReachedTent, Action onComplete)
    {
        // --- GO TO TENT ---
        yield return FollowPathCo(path);
        onReachedTent?.Invoke();

        yield return new WaitForSeconds(0.05f);

        // --- RETURN ---
        var back = new List<Vector3>(path);
        if (back.Count > 0 && Vector2.Distance(rb.position, back[back.Count - 1]) <= arriveDistance * 1.25f)
            back.RemoveAt(back.Count - 1);
        back.Reverse();

        if (back.Count > 0)
            yield return FollowPathCo(back);

        // Reset to depot
        rb.MovePosition(startPos);
#if UNITY_2021_3_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#endif
        EnableOnly(truck_goes_down);

        moveCo = null;
        onComplete?.Invoke();
    }

    IEnumerator FollowPathCo(List<Vector3> points)
    {
        int i = 0;

        while (i < points.Count && Vector2.Distance(rb.position, (Vector2)points[i]) <= arriveDistance)
            i++;

        float movedFromStartSq = 0f;

        while (i < points.Count)
        {
            var target = (Vector2)points[i];

            float lastDist = float.MaxValue;
            int stallFrames = 0;
            const int maxStallFrames = 30;
            const float snapIfCloserThan = 0.03f;

            while (Vector2.Distance(rb.position, target) > arriveDistance)
            {
                // Pause gate — completely freeze movement while raining
                if (isPaused)
                {
#if UNITY_2021_3_OR_NEWER
                    rb.linearVelocity = Vector2.zero;
#endif
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                Vector2 delta = target - rb.position;
                float dist = delta.magnitude;

                if (dist > 1e-5f)
                {
                    Vector2 dir = delta / dist;

                    if (lockFacingDown)
                    {
                        movedFromStartSq = (rb.position - startPos).sqrMagnitude;
                        if (movedFromStartSq > 0.0004f) // ~0.02 units
                            lockFacingDown = false;
                    }

                    if (!lockFacingDown)
                        UpdateDirectionSprite(dir);
                    else
                        EnableOnly(truck_goes_down);

                    rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
                }

                if (dist >= lastDist - 1e-5f) stallFrames++;
                else stallFrames = 0;
                lastDist = dist;

                if (stallFrames >= maxStallFrames || dist <= snapIfCloserThan)
                {
                    rb.MovePosition(target);
                    break;
                }

                yield return new WaitForFixedUpdate();
            }

            i++;
            while (i < points.Count && Vector2.Distance(rb.position, (Vector2)points[i]) <= arriveDistance)
                i++;

            yield return null;
        }
    }

    void UpdateDirectionSprite(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        bool horizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        if (horizontal) EnableOnly(dir.x >= 0f ? truck_goes_right : truck_goes_left);
        else            EnableOnly(dir.y >= 0f ? truck_goes_up    : truck_goes_down);
    }

    void EnableOnly(GameObject g)
    {
        if (truck_goes_right) truck_goes_right.SetActive(g == truck_goes_right);
        if (truck_goes_left)  truck_goes_left.SetActive(g == truck_goes_left);
        if (truck_goes_down)  truck_goes_down.SetActive(g == truck_goes_down);
        if (truck_goes_up)    truck_goes_up.SetActive(g == truck_goes_up);
    }
}
