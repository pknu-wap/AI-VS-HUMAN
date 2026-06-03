using System.Collections;
using System;
using UnityEngine;

[RequireComponent(typeof(CoreXIntroPhase))]
[RequireComponent(typeof(CoreXPhase1))]
[RequireComponent(typeof(CoreXPhaseTransition))]
[RequireComponent(typeof(CoreXPhase2))]
[RequireComponent(typeof(CoreXDashPattern))]
[RequireComponent(typeof(CoreXSummonPattern))]
[RequireComponent(typeof(CoreXElectricTrapPattern))]
public class CoreXBoss : MonoBehaviour, IDamageable
{
    private const float FadeDuration = 2f;

    [Header("체력")]
    public float maxHp = 500f;
    public bool deactivateOnDeathInsteadOfDestroy;

    [Header("페이즈")]
    [SerializeField] private CoreXIntroPhase introPhase;
    [SerializeField] private CoreXPhase1 phase1;
    [SerializeField] private CoreXPhaseTransition phaseTransition;
    [SerializeField] private CoreXPhase2 phase2;

    [Header("패턴")]
    [SerializeField] private CoreXDashPattern dashPattern;
    [SerializeField] private CoreXSummonPattern summonPattern;
    [SerializeField] private CoreXElectricTrapPattern electricTrapPattern;

    private float currentHp;
    private bool isDead;
    private bool isInvincible = true;
    private int serversAlive;
    private int serversDestroyed;

    private Room myRoom;
    private Transform player;
    private BossHpBar hpBar;
    private ServerNode[] phase1Servers;
    private ServerNode[] phase2Servers;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Coroutine hitFlashCoroutine;
    private Color originalColor;
    private Coroutine battleFlowCoroutine;
    private bool battleStatePrepared;

    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public int ServersAlive => serversAlive;
    public int ServersDestroyed => serversDestroyed;
    public Transform Player => player;
    public Room BossRoom => myRoom;
    public BossHpBar HpBar => hpBar;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public Rigidbody2D Rigidbody => rb;
    public Color OriginalColor => originalColor;
    public CoreXDashPattern DashPattern => dashPattern;
    public event Action Died;

    public void ConfigureForBossRoom(Room room, Transform playerTransform, BossHpBar bar,
                                     ServerNode[] p1Servers, ServerNode[] p2Servers)
    {
        myRoom = room;
        player = playerTransform;
        hpBar = bar;
        phase1Servers = p1Servers;
        phase2Servers = p2Servers;

        if (hpBar != null)
        {
            hpBar.SetMaxHp(maxHp);
            hpBar.Hide();
        }
    }

    private void Start()
    {
        CacheComponents();
        ResolveComponents();
        SetupRigidbody();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (!battleStatePrepared)
            ResetBattleState();

        StartBattleFlow();
    }

    public void PrepareForBossRoomActivation(Room room, Transform playerTransform, BossHpBar bar,
                                             ServerNode[] p1Servers, ServerNode[] p2Servers)
    {
        ConfigureForBossRoom(room, playerTransform, bar, p1Servers, p2Servers);
        CacheComponents();
        ResolveComponents();
        SetupRigidbody();
        StopAllCoroutines();
        battleFlowCoroutine = null;
        StopPhase2Patterns();
        ClearSpawnedMinions();
        ResetBattleState();
        battleStatePrepared = true;

        if (isActiveAndEnabled)
            StartBattleFlow();
    }

    public void ResetForBossRoomRetry()
    {
        StopAllCoroutines();
        battleFlowCoroutine = null;
        StopPhase2Patterns();
        ClearSpawnedMinions();
        ResetBattleState();
    }

    private void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    private void ResetBattleState()
    {
        currentHp = maxHp;
        isDead = false;
        isInvincible = true;
        serversAlive = 0;
        serversDestroyed = 0;

        if (hitFlashCoroutine != null)
            hitFlashCoroutine = null;

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (hpBar != null)
        {
            hpBar.SetMaxHp(maxHp);
            hpBar.Hide();
        }
    }

    private void StartBattleFlow()
    {
        if (battleFlowCoroutine != null)
            return;

        battleFlowCoroutine = StartCoroutine(BattleFlowRoutine());
    }

    private void StopBattleFlow()
    {
        if (battleFlowCoroutine == null)
            return;

        StopCoroutine(battleFlowCoroutine);
        battleFlowCoroutine = null;
    }

    private IEnumerator BattleFlowRoutine()
    {
        yield return StartCoroutine(BossBattleFlow());
        battleFlowCoroutine = null;
    }

    private void ResolveComponents()
    {
        introPhase = GetOrAddComponent(introPhase);
        phase1 = GetOrAddComponent(phase1);
        phaseTransition = GetOrAddComponent(phaseTransition);
        phase2 = GetOrAddComponent(phase2);
        dashPattern = GetOrAddComponent(dashPattern);
        summonPattern = GetOrAddComponent(summonPattern);
        electricTrapPattern = GetOrAddComponent(electricTrapPattern);
    }

    private T GetOrAddComponent<T>(T current) where T : Component
    {
        if (current != null)
            return current;

        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private void SetupRigidbody()
    {
        if (rb == null)
            return;

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private IEnumerator BossBattleFlow()
    {
        yield return StartCoroutine(introPhase.Run(this));
        if (isDead) yield break;

        yield return StartCoroutine(phase1.Run(this));
        if (isDead) yield break;

        yield return StartCoroutine(phaseTransition.Run(this));
        if (isDead) yield break;

        yield return StartCoroutine(phase2.Run(this));
        StopPhase2Patterns();
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    public void SetHp(float hp)
    {
        currentHp = Mathf.Clamp(hp, 0f, maxHp);
        if (hpBar != null)
            hpBar.SetHp(currentHp);
    }

    public void SetMaxHp(float newMaxHp)
    {
        maxHp = Mathf.Max(0f, newMaxHp);
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);

        if (hpBar != null)
            hpBar.SetMaxHp(maxHp);
    }

    public void SetSpriteColor(Color color)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    public void SetOriginalColor(Color color)
    {
        originalColor = color;
    }

    public bool ActivatePhase1Servers()
    {
        return ActivateServers(phase1Servers);
    }

    public bool ActivatePhase2Servers()
    {
        serversDestroyed = 0;
        return ActivateServers(phase2Servers);
    }

    private bool ActivateServers(ServerNode[] servers)
    {
        serversAlive = 0;

        if (servers == null || servers.Length == 0)
            return false;

        foreach (ServerNode server in servers)
        {
            if (server == null)
                continue;

            server.ResetServer();
            server.gameObject.SetActive(true);
            server.Init(this);
            serversAlive++;
        }

        return serversAlive > 0;
    }

    public void OnServerDestroyed()
    {
        serversAlive = Mathf.Max(0, serversAlive - 1);
        serversDestroyed++;
    }

    public void StartPhase2PressurePatterns()
    {
        summonPattern.StartPattern(this);
        electricTrapPattern.StartPattern(this);
    }

    public void StopPhase2Patterns()
    {
        if (summonPattern != null)
            summonPattern.StopPattern();

        if (electricTrapPattern != null)
            electricTrapPattern.StopPattern();
    }

    public void ClearSpawnedMinions()
    {
        if (summonPattern != null)
            summonPattern.ClearSpawnedMinions();
    }

    public void StartDeath()
    {
        if (!isDead)
            StartCoroutine(Die());
    }

    public void TakeDamage(float damage)
    {
        if (isDead || isInvincible)
            return;

        SetHp(currentHp - damage);

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);

        if (!isDead)
            spriteRenderer.color = originalColor;

        hitFlashCoroutine = null;
    }

    private IEnumerator Die()
    {
        isDead = true;
        Died?.Invoke();
        StopAllCoroutines();
        battleFlowCoroutine = null;
        StopPhase2Patterns();
        ClearSpawnedMinions();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        if (hpBar != null)
            hpBar.DestroyBar();

        for (float t = 0f; t < FadeDuration; t += Time.deltaTime)
        {
            if (spriteRenderer != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, t / FadeDuration);
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }

            yield return null;
        }

        if (deactivateOnDeathInsteadOfDestroy)
        {
            gameObject.SetActive(false);
            yield break;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
