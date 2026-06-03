using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDronePhase1 : MonoBehaviour
{
    [Header("감지")]
    public float detectionRange = 25f;

    [Header("Camera Bounds")]
    public bool keepInsideCameraView = false;
    public float cameraEdgePadding = 0.5f;

    [Header("이동")]
    public float moveSpeed = 2.5f;
    public float hoverAmplitude = 0.4f;
    public float hoverFrequency = 1.2f;
    public float swaySpeed = 1.5f;
    public float swayAmplitude = 3f;

    [Header("벽 회피")]
    public float wallAvoidDistance = 1.8f;
    public float wallAvoidSpeed = 5f;
    public float wallCheckRadius = 0.45f;
    public float wallStopPadding = 0.2f;
    public float wallUnstuckPadding = 0.05f;
    public float wallSafeStepDistance = 0.2f;
    public int wallResolveIterations = 4;

    private GiantDrone boss;
    private Coroutine attackLoopCoroutine;

    private void Awake()
    {
        boss = GetComponent<GiantDrone>();
    }

    public void Tick(GiantDrone owner)
    {
        boss = owner;

        if (boss == null || boss.isDead)
            return;

        ResolvePlayer();
        if (boss.player == null)
            return;

        if (Vector2.Distance(transform.position, boss.player.position) > detectionRange)
            return;

        StartPhase(boss);
        TickMovement();
    }

    public void LateTick(GiantDrone owner)
    {
        boss = owner;

        if (boss == null || boss.isDead || !boss.isActive)
            return;

        boss.ClampInsideCameraView();
    }

    public void StartPhase(GiantDrone owner)
    {
        boss = owner;

        if (boss.isActive)
            return;

        boss.isActive = true;
        boss.swayBaseX = transform.position.x;

        if (boss.bossCanvas != null)
            boss.bossCanvas.gameObject.SetActive(true);

        if (attackLoopCoroutine == null)
            attackLoopCoroutine = StartCoroutine(AttackLoop());
    }

    public void StopPhase()
    {
        StopAllCoroutines();
        attackLoopCoroutine = null;
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(2f);

        int[] patterns = { 0, 1, 2 };
        while (!boss.isDead)
        {
            ShufflePatterns(patterns);
            foreach (int pattern in patterns)
            {
                if (boss.isDead)
                {
                    attackLoopCoroutine = null;
                    yield break;
                }

                if (pattern == 0)
                {
                    if (boss.uDashPattern != null)
                        yield return StartCoroutine(boss.uDashPattern.Run(boss));

                    yield return new WaitForSeconds(boss.uDashPattern != null ? boss.uDashPattern.fanCooldown : 0f);
                }
                else if (pattern == 1)
                {
                    if (boss.petalPattern != null)
                        yield return StartCoroutine(boss.petalPattern.Run(boss));

                    yield return new WaitForSeconds(boss.petalPattern != null ? boss.petalPattern.loopDelay : 0f);
                }
                else
                {
                    if (boss.homingMissilePattern != null)
                        boss.homingMissilePattern.Fire(boss);

                    yield return new WaitForSeconds(boss.homingMissilePattern != null ? boss.homingMissilePattern.cooldown : 0f);
                }
            }
        }

        attackLoopCoroutine = null;
    }

    private void TickMovement()
    {
        if (boss.spriteRenderer != null)
            boss.spriteRenderer.flipX = boss.player.position.x < transform.position.x;

        boss.hoverTime += Time.deltaTime;
        if (boss.isDoingUDash)
            return;

        boss.swayTime += Time.deltaTime;
        boss.swayBaseX = Mathf.MoveTowards(
            boss.swayBaseX,
            boss.player.position.x,
            moveSpeed * 0.5f * Time.deltaTime);

        float bob = Mathf.Sin(boss.hoverTime * hoverFrequency) * hoverAmplitude;
        float targetX = boss.swayBaseX + Mathf.Sin(boss.swayTime * swaySpeed) * swayAmplitude;
        float targetY = boss.baseY + bob;
        float currentMoveSpeed = boss.isDoingPetal
            ? moveSpeed * (boss.petalPattern != null ? boss.petalPattern.moveSpeedMultiplier : 1f)
            : moveSpeed;

        float newX = Mathf.MoveTowards(transform.position.x, targetX, currentMoveSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetY, currentMoveSpeed * Time.deltaTime);
        Vector3 nextPosition = new Vector3(newX, newY, 0f);

        boss.MoveToSafePosition(nextPosition, LayerMask.GetMask("Ground"));
    }

    private void ResolvePlayer()
    {
        if (boss.player != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            boss.player = playerObj.transform;
    }

    private void ShufflePatterns(int[] patterns)
    {
        for (int i = patterns.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = patterns[i];
            patterns[i] = patterns[j];
            patterns[j] = temp;
        }
    }
}
