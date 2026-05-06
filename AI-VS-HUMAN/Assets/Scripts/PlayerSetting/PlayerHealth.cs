// 플레이어의 하트 기반 체력, 피격 무적 시간, 사망 처리를 담당하는 스크립트
// 탄환에 맞으면 체력이 줄고, 무적 시간 동안에는 추가 데미지를 받지 않는다.
using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("HP 설정")]
    public int maxHp = 5;
    private int currentHp;

    [Header("무적 프레임")]
    public float invincibleDuration = 1.0f;
    private bool isInvincible = false;

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;

        currentHp = Mathf.Clamp(currentHp - damage, 0, maxHp);
        Debug.Log("플레이어 HP: " + currentHp + " / " + maxHp);

        StartCoroutine(InvincibleFrame());

        if (currentHp <= 0) Die();
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        Debug.Log("플레이어 HP 회복: " + currentHp + " / " + maxHp);
    }

    IEnumerator InvincibleFrame()
    {
        // 무적 시간 동안 스프라이트를 깜빡여서 피격 상태를 보여준다.
        isInvincible = true;
        float elapsed = 0f;

        while (elapsed < invincibleDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        isInvincible = false;
    }

    void Die()
    {
        Debug.Log("플레이어 사망!");
        gameObject.SetActive(false);
    }
}
