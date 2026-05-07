// 플레이어의 하트 기반 체력, 사망, 리스폰, 부활 무적, 화면 체력 UI를 담당하는 스크립트입니다.
// 체력이 모두 줄거나 보스룸 카메라 아래로 떨어지면 일정 시간 후 시작 위치에서 부활하고, 사망/부활 이벤트로 보스룸을 초기화합니다.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public static event Action<PlayerHealth> PlayerDied;
    public static event Action<PlayerHealth> PlayerRespawned;

    [Header("HP 설정")]
    public int maxHp = 5;

    [Header("리스폰")]
    public float respawnDelay = 2f;
    public Transform respawnPoint;

    [Header("무적 프레임")]
    public float invincibleDuration = 1.0f;
    public float blinkInterval = 0.1f;

    [Header("체력 UI")]
    public bool createHealthUI = true;
    public Vector2 healthUiPosition = new Vector2(-40f, 40f);
    public int healthFontSize = 32;
    public string fullHeart = "■";
    public string emptyHeart = "□";

    private int currentHp;
    private bool isInvincible;
    private bool isDead;
    private Vector3 initialSpawnPosition;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D[] colliders;
    private PlayerMove playerMove;
    private InputController inputController;
    private AssaultRifle assaultRifle;
    private PlayerDashKnockback dashKnockback;
    private Coroutine invincibleCoroutine;
    private Coroutine respawnCoroutine;
    private Canvas healthCanvas;
    private Text healthText;
    private bool originalRigidbodySimulated = true;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        initialSpawnPosition = transform.position;
        currentHp = maxHp;

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        playerMove = GetComponent<PlayerMove>();
        inputController = GetComponent<InputController>();
        assaultRifle = GetComponent<AssaultRifle>();
        dashKnockback = GetComponent<PlayerDashKnockback>();

        if (rb != null)
            originalRigidbodySimulated = rb.simulated;
    }

    private void Start()
    {
        if (createHealthUI)
            CreateHealthUI();

        UpdateHealthUI();
    }

    public void TakeDamage(int damage)
    {
        if (isDead || isInvincible)
            return;

        currentHp = Mathf.Clamp(currentHp - Mathf.Max(0, damage), 0, maxHp);
        UpdateHealthUI();

        if (currentHp <= 0)
        {
            Kill();
            return;
        }

        StartInvincibility();
    }

    public void Heal(int amount)
    {
        if (isDead)
            return;

        currentHp = Mathf.Clamp(currentHp + Mathf.Max(0, amount), 0, maxHp);
        UpdateHealthUI();
    }

    public void Kill()
    {
        if (isDead)
            return;

        currentHp = 0;
        UpdateHealthUI();
        BeginDeath();
    }

    public void RespawnNow()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        Respawn();
    }

    private void BeginDeath()
    {
        isDead = true;
        isInvincible = false;

        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }

        SetControlEnabled(false);
        SetCollisionEnabled(false);
        SetSpriteVisible(false);
        StopRigidbody();

        PlayerDied?.Invoke(this);
        respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
        Respawn();
    }

    private void Respawn()
    {
        Vector3 spawnPosition = respawnPoint != null ? respawnPoint.position : initialSpawnPosition;
        transform.position = spawnPosition;
        Physics2D.SyncTransforms();

        currentHp = maxHp;
        isDead = false;

        RestoreRigidbody();
        SetCollisionEnabled(true);
        SetSpriteVisible(true);
        SetControlEnabled(true);
        UpdateHealthUI();

        PlayerRespawned?.Invoke(this);
        StartInvincibility();
        respawnCoroutine = null;
    }

    private void StartInvincibility()
    {
        if (invincibleCoroutine != null)
            StopCoroutine(invincibleCoroutine);

        invincibleCoroutine = StartCoroutine(InvincibleFrame());
    }

    private IEnumerator InvincibleFrame()
    {
        // 무적 시간 동안 스프라이트를 깜박여 플레이어가 피격 불가 상태임을 보여줍니다.
        isInvincible = true;
        float elapsed = 0f;
        float interval = Mathf.Max(0.02f, blinkInterval);

        while (elapsed < invincibleDuration)
        {
            SetSpriteVisible(!IsSpriteVisible());
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        SetSpriteVisible(true);
        isInvincible = false;
        invincibleCoroutine = null;
    }

    private void SetControlEnabled(bool enabled)
    {
        if (!enabled && playerMove != null)
            playerMove.ResetForRespawn();

        if (playerMove != null)
            playerMove.enabled = enabled;

        if (inputController != null)
            inputController.enabled = enabled;

        if (assaultRifle != null)
            assaultRifle.enabled = enabled;

        if (dashKnockback != null)
            dashKnockback.enabled = enabled;
    }

    private void SetCollisionEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        foreach (Collider2D col in colliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    private void StopRigidbody()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
    }

    private void RestoreRigidbody()
    {
        if (rb == null)
            return;

        rb.simulated = originalRigidbodySimulated;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }

    private bool IsSpriteVisible()
    {
        return spriteRenderer == null || spriteRenderer.enabled;
    }

    private void CreateHealthUI()
    {
        if (healthCanvas != null)
            return;

        GameObject canvasObj = new GameObject("PlayerHpCanvas");
        healthCanvas = canvasObj.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        healthCanvas.sortingOrder = 1000;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("PlayerHpText");
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        // 보스 체력바와 겹치지 않도록 플레이어 체력은 화면 오른쪽 아래에 하트로 표시합니다.
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = healthUiPosition;
        rt.sizeDelta = new Vector2(260f, 50f);

        healthText = textObj.AddComponent<Text>();
        healthText.alignment = TextAnchor.MiddleRight;
        healthText.color = new Color(1f, 0.2f, 0.25f, 1f);
        healthText.fontSize = healthFontSize;
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void UpdateHealthUI()
    {
        if (healthText != null)
            healthText.text = BuildHeartText();
    }

    private string BuildHeartText()
    {
        int safeMaxHp = Mathf.Max(0, maxHp);
        int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        System.Text.StringBuilder builder = new System.Text.StringBuilder(safeMaxHp);

        for (int i = 0; i < safeMaxHp; i++)
            builder.Append(i < safeCurrentHp ? fullHeart : emptyHeart);

        return builder.ToString();
    }

    private void OnDestroy()
    {
        if (healthCanvas != null)
            Destroy(healthCanvas.gameObject);
    }
}
