using UnityEngine;
using UnityEngine.UI;

// 적 (일반 몬스터)
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterController : MonoBehaviour, IDamageable
{
    [Header("몬스터 데이터")]
    public CharacterData monsterData;
    public EnemyState currentState;

    [Header("체력 설정")]
    public int maxHP = 30;
    protected int currentHP;

    [Header("피격 설정")]
    public float hitStunDuration = 0.3f;   // 피격 경직 시간
    public float knockbackForce = 3f;      // 넉백 힘

    [Header("HP 바")]
    public GameObject hpBarPrefab;
    public float hpBarOffsetY = 0.2f;
    public float hpBarWidth = 0f;
    public float hpBarHeight = 0.1f;
    public float hpBarFadeSpeed = 3f;

    [Header("드롭 설정")]
    public GameObject dropPrefab;
    public int dropAmount = 1;

    // 모듈 참조
    [HideInInspector] public MonMove moveModule;
    [HideInInspector] public MonAnim animModule;
    [HideInInspector] public MonAttack attackModule;

    // 내부 변수
    private float hitTimer = 0f;
    private Rigidbody2D rb;

    // HP 바 관련
    private GameObject hpBarInstance;
    private Image hpFillImage;
    private CanvasGroup hpBarCanvasGroup;
    protected bool hpBarCreated = false;
    protected float hpBarAlpha = 0f;
    protected float lastHitTime = -999f;

    // 흔들림
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private Vector3 originalPosition;
    public float shakeDuration = 0.2f;
    public float shakeIntensity = 0.05f;

    protected virtual void Awake()
    {
        moveModule = GetComponent<MonMove>();
        animModule = GetComponent<MonAnim>();
        attackModule = GetComponent<MonAttack>();
        rb = GetComponent<Rigidbody2D>();
    }

    protected virtual void Start()
    {
        currentHP = maxHP;
        originalPosition = transform.localPosition;

        // 기본 스탯 초기화
        if (monsterData == null) monsterData = new CharacterData();
        if (monsterData.stats == null) monsterData.stats = new StatData();

        ChangeState(EnemyState.Idle);
    }

    protected virtual void Update()
    {
        HandleShake();
        HandleHPBarFade();

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Move:
                // 배회 중 플레이어 감지 체크
                if (moveModule != null && moveModule.IsPlayerInDetectRange())
                {
                    ChangeState(EnemyState.Trigger);
                    break;
                }

                // 배회 이동
                if (moveModule != null)
                {
                    moveModule.HandlePatrol();
                }

                // 애니메이션 갱신
                if (animModule != null)
                {
                    bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
                    animModule.UpdateMoveAnimation(isMoving);
                    if (isMoving && currentState == EnemyState.Idle)
                        currentState = EnemyState.Move;
                    else if (!isMoving && currentState == EnemyState.Move)
                        currentState = EnemyState.Idle;
                }
                break;

            case EnemyState.Trigger:
                // 추적 모드: 플레이어를 향해 이동
                if (moveModule != null)
                {
                    // 플레이어가 너무 멀어지면 배회로 복귀
                    if (moveModule.IsPlayerOutOfRange())
                    {
                        ChangeState(EnemyState.Idle);
                        break;
                    }

                    // 공격 사거리 진입 시 공격 시도
                    if (moveModule.IsPlayerInAttackRange())
                    {
                        if (attackModule != null && attackModule.CanAttack())
                        {
                            ChangeState(EnemyState.Attack);
                            break;
                        }
                    }

                    // 추적 이동
                    moveModule.HandleChase();
                }

                if (animModule != null)
                    animModule.UpdateMoveAnimation(true);
                break;

            case EnemyState.Attack:
                if (moveModule != null)
                    moveModule.StopMoving();

                if (attackModule != null)
                {
                    bool attackDone = attackModule.HandleAttack();
                    if (attackDone)
                    {
                        // 공격 끝나면 다시 추적 모드
                        ChangeState(EnemyState.Trigger);
                    }
                }
                break;

            case EnemyState.Hit:
                hitTimer -= Time.deltaTime;
                if (hitTimer <= 0f)
                {
                    // 경직 끝 → 추적 모드로 복귀 (플레이어를 때린 놈이니까 적대)
                    ChangeState(EnemyState.Trigger);
                }
                break;

            case EnemyState.Dead:
                // 사망 상태 — 아무것도 안 함
                break;
        }
    }

    // ──────────────── 피격 처리 ────────────────

    public virtual void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHP -= damage;
        lastHitTime = Time.time;

        // HP바 생성 (Lazy)
        if (!hpBarCreated)
        {
            CreateHPBar();
            hpBarCreated = true;
        }
        UpdateHPBar();
        hpBarAlpha = 1f;

        // 흔들림
        StartShake();

        Debug.Log($"[Monster] {gameObject.name} took {damage} damage! ({currentHP}/{maxHP})");

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
        else
        {
            // 경직 + 넉백
            ChangeState(EnemyState.Hit);
            hitTimer = hitStunDuration;

            // 넉백: 공격자(플레이어) 반대 방향으로
            if (moveModule != null && rb != null)
            {
                Vector2 knockDir = -moveModule.GetDirectionToPlayer();
                rb.linearVelocity = knockDir * knockbackForce;

                // 넉백 시 기존 A* 경로 초기화 (바로 방향 전환 방지)
                moveModule.InvalidatePath();
            }
        }
    }

    protected void Die()
    {
        ChangeState(EnemyState.Dead);
        
        if (moveModule != null)
            moveModule.StopMoving();

        // 드롭 아이템
        if (dropPrefab != null)
        {
            for (int i = 0; i < dropAmount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 0.5f;
                Instantiate(dropPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            }
        }

        // 콜라이더 비활성화 후 일정 시간 뒤 파괴
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 1f); // 1초 후 제거 (사망 애니메이션 재생 시간)
    }

    // ──────────────── 상태 전환 ────────────────

    public void ChangeState(EnemyState newState) 
    { 
        if (currentState == EnemyState.Dead) return; // 사망 후엔 전환 불가

        currentState = newState;

        if (animModule != null)
            animModule.PlayStateAnim(newState);

        // 공격 시작 시 AttackModule 초기화
        if (newState == EnemyState.Attack && attackModule != null)
            attackModule.StartAttack();
    }

    // ──────────────── HP 바 (Lazy 생성) ────────────────

    protected void CreateHPBar()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float topY = sr != null ? (sr.bounds.max.y - transform.position.y + hpBarOffsetY) : (1f + hpBarOffsetY);

        if (hpBarWidth <= 0f && sr != null)
            hpBarWidth = sr.bounds.size.x * 0.8f;
        if (hpBarWidth <= 0f)
            hpBarWidth = 0.8f;

        if (hpBarPrefab != null)
        {
            hpBarInstance = Instantiate(hpBarPrefab, transform);
            hpBarInstance.transform.localPosition = new Vector3(0f, topY, 0f);
        }
        else
        {
            // 코드로 자동 생성
            hpBarInstance = new GameObject("HPBar_Canvas");
            hpBarInstance.transform.SetParent(transform);
            hpBarInstance.transform.localPosition = new Vector3(0f, topY, 0f);
            hpBarInstance.transform.localScale = Vector3.one;

            Canvas canvas = hpBarInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            RectTransform canvasRect = hpBarInstance.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(hpBarWidth, hpBarHeight);
            canvasRect.localScale = Vector3.one;

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

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(hpBarInstance.transform);
            hpFillImage = fillObj.AddComponent<Image>();
            hpFillImage.color = new Color(0.85f, 0.2f, 0.2f, 1f); // 몬스터는 빨간색
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpFillImage.fillAmount = 1f;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
            fillRect.localScale = Vector3.one;
        }

        if (hpFillImage == null && hpBarInstance != null)
        {
            Transform fillTransform = hpBarInstance.transform.Find("Fill");
            if (fillTransform != null)
                hpFillImage = fillTransform.GetComponent<Image>();
        }

        if (hpBarInstance != null)
        {
            hpBarCanvasGroup = hpBarInstance.GetComponent<CanvasGroup>();
            if (hpBarCanvasGroup == null)
                hpBarCanvasGroup = hpBarInstance.AddComponent<CanvasGroup>();
            hpBarCanvasGroup.alpha = 0f;
        }
    }

    protected void UpdateHPBar()
    {
        if (hpFillImage != null)
            hpFillImage.fillAmount = (float)currentHP / maxHP;
    }

    private void HandleHPBarFade()
    {
        if (hpBarCanvasGroup == null) return;

        float targetAlpha;
        if (currentHP >= maxHP || currentHP <= 0)
            targetAlpha = 0f;
        else
        {
            float timeSinceHit = Time.time - lastHitTime;
            targetAlpha = (timeSinceHit < 5f) ? 1f : 0f;
        }

        hpBarAlpha = Mathf.MoveTowards(hpBarAlpha, targetAlpha, hpBarFadeSpeed * Time.deltaTime);
        hpBarCanvasGroup.alpha = hpBarAlpha;
    }

    // ──────────────── 흔들림 ────────────────

    protected void StartShake()
    {
        if (!isShaking) originalPosition = transform.localPosition;
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
