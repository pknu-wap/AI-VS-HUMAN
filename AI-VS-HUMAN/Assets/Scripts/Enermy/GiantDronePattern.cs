using System.Collections;
using UnityEngine;

public partial class GiantDrone
{

    IEnumerator PatternLoop()
    {
        // 공격 패턴 순서를 섞어 반복 실행하고, 회복 드론 루프는 별도 코루틴으로 돌린다.
        yield return new WaitForSeconds(2f);
        StartCoroutine(HealDroneLoop());

        int[] patterns = { 0, 1 };
        while (!isDead)
        {
            ShufflePatterns(patterns);
            foreach (int pattern in patterns)
            {
                if (isDead) yield break;
                if (pattern == 0)
                {
                    yield return StartCoroutine(SmoothUDashAndFire());
                    yield return new WaitForSeconds(fanCooldown);
                }
                else
                {
                    isDoingPetal = true;
                    yield return StartCoroutine(FirePetalPattern());
                    isDoingPetal = false;
                    yield return new WaitForSeconds(petalLoopDelay);
                }
            }
        }
    }

    void ShufflePatterns(int[] patterns)
    {
        for (int i = patterns.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = patterns[i];
            patterns[i] = patterns[j];
            patterns[j] = temp;
        }
    }

    // ── 개선된 U자 돌진 + 시간차 부채꼴 발사 ────────────────
    IEnumerator SmoothUDashAndFire()
    {
        if (isDead || player == null) yield break;

        isDoingUDash = true;
        LayerMask groundMask = LayerMask.GetMask("Ground");

        Vector3 p0 = transform.position;
        float dirX = player.position.x > transform.position.x ? 1f : -1f;

        // 베지에 곡선 제어점 (U자형)
        Vector3 p2 = new Vector3(p0.x + dashWidth * dirX, p0.y, 0f);
        Vector3 p1 = new Vector3((p0.x + p2.x) * 0.5f, p0.y - (dashDropY * 2f), 0f);

        float duration = Mathf.Max(dashWidth / dashSpeed, 1.2f);
        float elapsed = 0f;
        
        int firedCount = 0;
        float nextFireTime = 0f;

        while (elapsed < duration && !isDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            // 2차 베지에 곡선 위치 업데이트
            Vector3 m1 = Vector3.Lerp(p0, p1, easeT);
            Vector3 m2 = Vector3.Lerp(p1, p2, easeT);
            Vector3 targetPosition = Vector3.Lerp(m1, m2, easeT);
            bool reachedTarget = MoveToSafePosition(targetPosition, groundMask);

            // 벽에 막혔으면 그 자리에서 돌진을 끊어 관통/끼임을 막는다.
            if (!reachedTarget)
            {
                ResolveCurrentWallOverlap(groundMask);
                break;
            }

            // 곡선 진행도가 20%를 넘으면 설정한 간격(fanDashFireDelay)마다 발사
            if (easeT >= 0.2f && firedCount < fanDashVolleyCount)
            {
                if (Time.time >= nextFireTime)
                {
                    FireFanBullets();
                    firedCount++;
                    nextFireTime = Time.time + fanDashFireDelay;
                }
            }

            yield return null;
        }

        if (firedCount == 0 && !isDead) FireFanBullets();

        swayBaseX = transform.position.x;
        isDoingUDash = false;
    }

    void FireFanBullets()
    {
        if (fanBulletPrefab == null || player == null) return;

        Vector2 aimDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector2.down;

        Vector3 firePos = transform.position + (Vector3)(aimDir * fanFireOffset);
        float baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle * 0.5f;
        float step = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + step * i;
            Vector2 dir = AngleToDir(angle);
            SpawnBullet(fanBulletPrefab, firePos, dir, fanBulletDamage, fanBulletSpeed, angle);
        }
    }

    IEnumerator FirePetalPattern()
    {
        if (petalBulletPrefab == null) yield break;
        float angleStep = 360f / petalArmCount;

        for (int shot = 0; shot < petalBulletsPerArm; shot++)
        {
            for (int arm = 0; arm < petalArmCount; arm++)
            {
                float angle = petalBaseAngle + angleStep * arm;
                Vector2 dir = AngleToDir(angle);
                float curveDir = arm % 2 == 0 ? 1f : -1f;

                GameObject go = Instantiate(petalBulletPrefab, transform.position, Quaternion.identity);
                PetalBullet pb = go.GetComponent<PetalBullet>() ?? go.AddComponent<PetalBullet>();
                pb.Init(dir, petalBulletSpeed, petalCurvature * curveDir, 3.5f, petalSpawnOffset);
            }
            petalBaseAngle -= petalRotatePerShot;
            yield return new WaitForSeconds(petalFireInterval);
        }
    }

    IEnumerator HealDroneLoop()
    {
        while (!isDead && currentHp > maxHp * 0.5f)
            yield return null;

        yield return new WaitForSeconds(healDroneFirstDelay);
        while (!isDead)
        {
            if (healDronePrefab == null) yield break;
            if (currentHp < maxHp)
                yield return StartCoroutine(HealDronePattern());
            yield return new WaitForSeconds(healDroneRepeatDelay);
        }
    }

    IEnumerator HealDronePattern()
    {
        // 보스전 2페이즈부터 화면 밖 좌우에서 회복 드론을 날려 보내고, 둘 다 붙거나 파괴될 때까지 기다린다.
        if (healDronePrefab == null) yield break;

        healDroneAliveCount = 0;
        float elapsed = 0f;
        float[] sides = { -1f, 1f };
        int spawnCount = Mathf.Min(Mathf.Max(0, healDroneCount), sides.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            float side = sides[i];
            Vector3 spawnPos = GetHealDroneSpawnPosition(side);
            GameObject go = Instantiate(healDronePrefab, spawnPos, Quaternion.identity);
            HealDrone hd = go.GetComponent<HealDrone>() ?? go.AddComponent<HealDrone>();
            hd.Init(this, side);
            healDroneAliveCount++;
        }

        // 드론을 빠르게 파괴해도 정해진 지속 시간이 끝나야 다음 회복 패턴 쿨타임으로 넘어간다.
        while (!isDead && (healDroneAliveCount > 0 || elapsed < healDronePatternMinDuration))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Vector3 GetHealDroneSpawnPosition(float side)
    {
        // 현재 카메라 기준 화면 밖 좌우에서 출발하게 만들어 2페이즈 카메라 이동 후에도 같은 패턴이 유지되게 한다.
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        Vector3 bossCenter = GetBossVisualCenter();

        if (cam == null || !cam.orthographic)
            return bossCenter + new Vector3(side * (12f + healDroneSpawnOutsidePadding), healDroneOffsetY, 0f);

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float spawnX = cam.transform.position.x + Mathf.Sign(side) * (halfWidth + healDroneSpawnOutsidePadding);
        return new Vector3(spawnX, bossCenter.y + healDroneOffsetY, 0f);
    }

    public Vector3 GetHealDroneAttachPosition(float side)
    {
        // 드론이 보스의 좌우 하단에 붙어 보이도록 보스 중심 기준 부착 위치를 제공한다.
        Vector3 bossCenter = GetBossVisualCenter();
        return bossCenter + new Vector3(Mathf.Sign(side) * healDroneOffsetX, healDroneOffsetY, 0f);
    }

    public void HealFromAttachedDrone(float deltaTime)
    {
        if (isDead)
            return;

        currentHp = Mathf.Clamp(currentHp + healAmount * deltaTime, 0f, maxHp);
        UpdateHpBar();
    }

    public void OnHealDroneDestroyed()
    {
        healDroneAliveCount = Mathf.Max(0, healDroneAliveCount - 1);
    }

    Vector2 AngleToDir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    void SpawnBullet(GameObject prefab, Vector3 spawnPos, Vector2 dir, float damage, float speed, float angle)
    {
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet != null) bullet.Init(dir, damage, speed);
    }
}
