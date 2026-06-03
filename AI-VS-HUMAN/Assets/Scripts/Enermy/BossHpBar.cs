using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 HP 바 UI
/// - 보스 오브젝트에 붙이거나 별도 오브젝트에 붙여서 사용
/// - Start()에서 자동으로 UI 생성
/// - 보스가 IDamageable을 구현하고 있으면 자동 연동
/// </summary>
public class BossHpBar : MonoBehaviour
{
    [Header("연결")]
    public float  maxHp       = 500f;     // 최대 HP (보스 스탯과 맞춰야 함)

    [Header("색상")]
    public Color hpBarColor   = new Color(0.2f, 0.8f, 1f);
    public Color bgColor      = new Color(0.1f, 0.1f, 0.1f, 0.8f);

    [Header("위치")]
    public float posY         = -40f;    // 화면 상단 기준 Y 위치
    public float barHeight    = 26f;     // HP 바 높이

    private Slider hpSlider;
    private Canvas bossCanvas;
    private float  currentHp;

    void Start()
    {
        currentHp = maxHp;
        CreateUI();
        // 처음엔 숨김 → ShowBar()로 표시
        Hide();
    }

    // ── UI 생성 ────────────────────────────────
    void CreateUI()
    {
        GameObject canvasObj = new GameObject("BossHpCanvas");
        bossCanvas = canvasObj.AddComponent<Canvas>();
        bossCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        bossCanvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 배경
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        SetAnchorTop(bgRt, posY, barHeight);
        bg.AddComponent<Image>().color = bgColor;

        // 슬라이더
        GameObject slObj = new GameObject("HpSlider");
        slObj.transform.SetParent(canvasObj.transform, false);
        hpSlider = slObj.AddComponent<Slider>();
        RectTransform slRt = slObj.GetComponent<RectTransform>();
        SetAnchorTop(slRt, posY, barHeight);

        GameObject fa = new GameObject("Fill Area");
        fa.transform.SetParent(slObj.transform, false);
        SetFullRect(fa.AddComponent<RectTransform>());

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fa.transform, false);
        SetFullRect(fill.AddComponent<RectTransform>());
        fill.AddComponent<Image>().color = hpBarColor;

        hpSlider.fillRect     = fill.GetComponent<RectTransform>();
        hpSlider.minValue     = 0f;
        hpSlider.maxValue     = maxHp;
        hpSlider.value        = maxHp;
        hpSlider.interactable = false;

    }

    // ── 공개 함수 ──────────────────────────────

    /// <summary>HP 바 표시</summary>
    public void Show()
    {
        if (bossCanvas != null) bossCanvas.gameObject.SetActive(true);
    }

    /// <summary>HP 바 숨기기</summary>
    public void Hide()
    {
        if (bossCanvas != null) bossCanvas.gameObject.SetActive(false);
    }

    /// <summary>HP 업데이트 (0 ~ maxHp)</summary>
    public void SetHp(float hp)
    {
        currentHp = Mathf.Clamp(hp, 0f, maxHp);
        if (hpSlider != null) hpSlider.value = currentHp;
    }

    /// <summary>최대 HP 설정 (보스 Start에서 호출)</summary>
    public void SetMaxHp(float max)
    {
        maxHp = max;
        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.value    = max;
        }
    }

    /// <summary>HP 바 제거</summary>
    public void DestroyBar()
    {
        if (bossCanvas != null) Destroy(bossCanvas.gameObject);
    }

    // ── 유틸 ───────────────────────────────────
    void SetAnchorTop(RectTransform rt, float y, float height)
    {
        rt.anchorMin        = new Vector2(0.1f, 1f);
        rt.anchorMax        = new Vector2(0.9f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(0f, height);
    }

    void SetFullRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
