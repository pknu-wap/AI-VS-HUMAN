using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEnemyCollisionIgnorer : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private Collider2D[] playerColliders;
    private float refreshTimer;

    private void Awake()
    {
        CachePlayerColliders();
        RefreshIgnoredCollisions();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer < RefreshInterval)
            return;

        refreshTimer = 0f;
        RefreshIgnoredCollisions();
    }

    private void CachePlayerColliders()
    {
        playerColliders = GetComponents<Collider2D>();
    }

    private void RefreshIgnoredCollisions()
    {
        if (playerColliders == null || playerColliders.Length == 0)
            CachePlayerColliders();

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (Collider2D enemyCollider in colliders)
        {
            if (!IsEnemyBodyCollider(enemyCollider))
                continue;

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
            }
        }
    }

    private bool IsEnemyBodyCollider(Collider2D candidate)
    {
        if (candidate == null || candidate.isTrigger || candidate.GetComponentInParent<PlayerHealth>() != null)
            return false;

        return candidate.GetComponentInParent<EnemyBase>() != null
            || candidate.GetComponentInParent<CoreXBoss>() != null
            || candidate.GetComponentInParent<GiantDrone>() != null
            || candidate.GetComponentInParent<HealDrone>() != null
            || candidate.GetComponentInParent<ServerNode>() != null
            || candidate.GetComponentInParent<GhostEnemy>() != null
            || candidate.GetComponentInParent<ShadowEnemy>() != null
            || candidate.CompareTag("Enemy");
    }
}
