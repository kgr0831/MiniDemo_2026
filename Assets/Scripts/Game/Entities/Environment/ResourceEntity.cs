using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 나무, 바위 등 채집 가능한 자원 오브젝트의 공통 베이스 클래스.
/// 피격 흔들림, HP바 자동 생성/페이드 인/아웃, HP 자동 회복 기능 포함.
/// </summary>
public abstract class ResourceEntity : MonoBehaviour, IDamageable
{
    [Header("자원 기본 설정")]
    public int maxHealth = 30;
    public GameObject dropPrefab;  // 파괴 시 드랍할 아이템 프리팹
    public int dropAmount = 2;     // 드랍 개수

    [Header("HP 회복")]
    public float regenDelay = 5f;       // 마지막 피격 후 회복 시작까지 대기 시간
    public float regenPerSecond = 5f;   // 초당 HP 회복량

    [Header("HP 바")]
    public GameObject hpBarPrefab;      // HP 바 프리팹 (없으면 자동 생성)
    public float hpBarOffsetY = 0.2f;   // 스프라이트 상단에서 HP바까지의 추가 간격
    public float hpBarWidth = 0f;       // HP 바 너비 (0이면 스프라이트 너비의 80%로 자동 계산)
    public float hpBarHeight = 0.1f;    // HP 바 높이
    public float hpBarFadeSpeed = 3f;   // 페이드 인/아웃 속도

    [Header("피격 흔들림")]
    public float shakeDuration = 0.3f;
    public float shakeIntensity = 0.08f;

    // 내부 변수
    protected int currentHealth;
    private float lastHitTime;
    private float hpBarAlpha = 0f;
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private Vector3 originalPosition;

    // HP 바 관련
    private GameObject hpBarInstance;
    private Image hpFillImage;
    private CanvasGroup hpBarCanvasGroup;
    private bool hpBarCreated = false; // Lazy 생성 플래그

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        originalPosition = transform.localPosition;
    }

    protected virtual void Start()
    {
        // HP 바는 첨 피격 시 만들어지므로 Start에서는 아무것도 안 함
    }

    private void CreateHPBar(float topOffset)
    {
        if (hpBarPrefab != null)
        {
            // 프리팹 사용
            hpBarInstance = Instantiate(hpBarPrefab, transform);
            hpBarInstance.transform.localPosition = new Vector3(0f, topOffset, 0f);
        }
        else
        {
            // 프리팹이 없으면 코드로 자동 생성
            hpBarInstance = new GameObject("HPBar_Canvas");
            hpBarInstance.transform.SetParent(transform);
            hpBarInstance.transform.localPosition = new Vector3(0f, topOffset, 0f);
            hpBarInstance.transform.localScale = Vector3.one;

            // World Space Canvas
            Canvas canvas = hpBarInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            RectTransform canvasRect = hpBarInstance.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(hpBarWidth, hpBarHeight);
            canvasRect.localScale = Vector3.one;

            // 배경 (어두운 바)
            GameObject bgObj = new GameObject("BG");
            bgObj.transform.SetParent(hpBarInstance.transform);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.localScale = Vector3.one;

            // Fill 바 (초록색)
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(hpBarInstance.transform);
            hpFillImage = fillObj.AddComponent<Image>();
            hpFillImage.color = new Color(0.2f, 0.85f, 0.2f, 1f);
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpFillImage.fillAmount = 1f;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);   // 약간의 패딩
            fillRect.offsetMax = new Vector2(-1f, -1f);
            fillRect.localScale = Vector3.one;
        }

        // 프리팹에서 Fill 이미지 찾기 (프리팹 사용 시)
        if (hpFillImage == null && hpBarInstance != null)
        {
            // "Fill"이라는 이름의 자식에서 Image 컴포넌트 찾기
            Transform fillTransform = hpBarInstance.transform.Find("Fill");
            if (fillTransform != null)
                hpFillImage = fillTransform.GetComponent<Image>();
        }

        // CanvasGroup 세팅
        if (hpBarInstance != null)
        {
            hpBarCanvasGroup = hpBarInstance.GetComponent<CanvasGroup>();
            if (hpBarCanvasGroup == null)
                hpBarCanvasGroup = hpBarInstance.AddComponent<CanvasGroup>();
            hpBarCanvasGroup.alpha = 0f; // 시작 시 숨김
        }
    }

    protected virtual void Update()
    {
        HandleShake();
        HandleHPBarFade();
        HandleRegen();
    }

    // ──────────────── 피격 처리 ────────────────

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        lastHitTime = Time.time;
        
        // 첨 피격 시 HP바 생성 (Lazy)
        if (!hpBarCreated)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            float topY = sr != null ? (sr.bounds.max.y - transform.position.y + hpBarOffsetY) : (1f + hpBarOffsetY);
            
            // 바 너비가 0이면 스프라이트 너비에 맞춤 자동 계산
            if (hpBarWidth <= 0f && sr != null)
                hpBarWidth = sr.bounds.size.x * 0.8f; // 스프라이트 너비의 80%
            if (hpBarWidth <= 0f)
                hpBarWidth = 0.8f; // 기본값
                
            CreateHPBar(topY);
            hpBarCreated = true;
        }

        UpdateHPBar();
        hpBarAlpha = 1f; // 즉시 페이드 인

        StartShake();

        Debug.Log($"{gameObject.name} took {damage} damage! ({currentHealth}/{maxHealth})");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDestroyed();
        }
    }

    // ──────────────── 파괴 시 아이템 드랍 ────────────────

    protected virtual void OnDestroyed()
    {
        if (dropPrefab != null)
        {
            for (int i = 0; i < dropAmount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                Instantiate(dropPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity);
            }
        }
        Destroy(gameObject);
    }

    // ──────────────── HP 자동 회복 ────────────────

    private void HandleRegen()
    {
        if (currentHealth <= 0 || currentHealth >= maxHealth) return;

        if (Time.time - lastHitTime >= regenDelay)
        {
            currentHealth += Mathf.CeilToInt(regenPerSecond * Time.deltaTime);
            if (currentHealth >= maxHealth)
            {
                currentHealth = maxHealth;
            }
            UpdateHPBar();
        }
    }

    // ──────────────── HP 바 UI ────────────────

    private void UpdateHPBar()
    {
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = (float)currentHealth / maxHealth;
        }
    }

    private void HandleHPBarFade()
    {
        if (hpBarCanvasGroup == null) return;

        float targetAlpha;
        if (currentHealth >= maxHealth)
        {
            targetAlpha = 0f;
        }
        else
        {
            float timeSinceHit = Time.time - lastHitTime;
            targetAlpha = (timeSinceHit < regenDelay + 2f) ? 1f : 0f;
        }

        hpBarAlpha = Mathf.MoveTowards(hpBarAlpha, targetAlpha, hpBarFadeSpeed * Time.deltaTime);
        hpBarCanvasGroup.alpha = hpBarAlpha;
    }

    // ──────────────── 피격 흔들림 ────────────────

    private void StartShake()
    {
        if (!isShaking)
        {
            originalPosition = transform.localPosition;
        }
        isShaking = true;
        shakeTimer = shakeDuration;
    }

    private void HandleShake()
    {
        if (!isShaking) return;

        shakeTimer -= Time.deltaTime;
        if (shakeTimer > 0)
        {
            float offsetX = Mathf.Sin(shakeTimer * 50f) * shakeIntensity * (shakeTimer / shakeDuration);
            transform.localPosition = originalPosition + new Vector3(offsetX, 0f, 0f);
        }
        else
        {
            isShaking = false;
            transform.localPosition = originalPosition;
        }
    }
}
