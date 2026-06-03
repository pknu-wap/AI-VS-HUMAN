using UnityEngine;

public partial class GiantDrone
{
    void Update()
    {
        // 플레이어가 감지 범위에 들어오기 전까지는 보스 패턴을 시작하지 않는다.
        if (isDead || player == null) return;

        if (Vector2.Distance(transform.position, player.position) > detectionRange)
            return;

        if (!isActive)
        {
            isActive = true;
            swayBaseX = transform.position.x;
            if (bossCanvas != null) bossCanvas.gameObject.SetActive(true);
            StartCoroutine(PatternLoop());
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;

        hoverTime += Time.deltaTime;
        if (isDoingUDash) return;

        swayTime += Time.deltaTime;
        swayBaseX = Mathf.MoveTowards(swayBaseX, player.position.x, moveSpeed * 0.5f * Time.deltaTime);

        float bob = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        float targetX = swayBaseX + Mathf.Sin(swayTime * swaySpeed) * swayAmplitude;
        float targetY = baseY + bob;
        float currentMoveSpeed = isDoingPetal ? moveSpeed * petalMoveSpeedMultiplier : moveSpeed;

        float newX = Mathf.MoveTowards(transform.position.x, targetX, currentMoveSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetY, currentMoveSpeed * Time.deltaTime);

        LayerMask groundMask = LayerMask.GetMask("Ground");
        Vector3 nextPosition = new Vector3(newX, newY, 0f);

        MoveToSafePosition(nextPosition, groundMask);
    }

    void LateUpdate()
    {
        if (isDead || !isActive)
            return;

        ClampInsideCameraView();
    }
}
