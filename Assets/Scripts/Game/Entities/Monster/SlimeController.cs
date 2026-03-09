using UnityEngine;

/// <summary>
/// 슬라임 몬스터.
/// 행동 패턴:
///   배회(Walk) → 플레이어 감지 → 추적(Run) → 돌진(Dash) → 주변 범위 공격(AOE Slam) → 추적 복귀
/// </summary>
public class SlimeController : MonsterController
{
    [Header("슬라임 전용 설정")]
    public float dashSpeed = 8f;           // 돌진 속도
    public float dashDuration = 0.3f;      // 돌진 지속 시간
    public float slamRadius = 1.5f;        // AOE 슬램 범위
    public int slamDamage = 8;             // 슬램 데미지
    public float slamDelay = 0.2f;         // 돌진 후 슬램까지의 대기 시간
    public float attackCooldown = 2.5f;    // 공격 쿨다운

    // 내부 변수
    private float dashTimer;
    private float slamTimer;
    private float cooldownTimer;
    private Vector2 dashDirection;
    private Rigidbody2D rb;
    private bool hasSlammedThisAttack;

    private enum SlimePhase { Dash, SlamWait, Slam }
    private SlimePhase attackPhase;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void Start()
    {
        base.Start();
        cooldownTimer = 0f;
    }

    protected override void Update()
    {
        // 부모의 흔들림/HP바 로직은 유지 (base.Update의 FSM은 override)
        // 직접 FSM 구현

        // 쿨다운 타이머
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Move:
                HandlePatrolState();
                break;

            case EnemyState.Trigger:
                HandleChaseState();
                break;

            case EnemyState.Dash:
                HandleDashState();
                break;

            case EnemyState.Attack:
                HandleSlamState();
                break;

            case EnemyState.Hit:
                HandleHitState();
                break;

            case EnemyState.Dead:
                break;
        }
    }

    // ──────────────── 배회 (Walk) ────────────────

    private void HandlePatrolState()
    {
        // 플레이어 감지 → 추적
        if (moveModule != null && moveModule.IsPlayerInDetectRange())
        {
            ChangeState(EnemyState.Trigger);
            return;
        }

        if (moveModule != null)
            moveModule.HandlePatrol();

        if (animModule != null)
        {
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
            animModule.UpdateMoveAnimation(isMoving);
            
            if (isMoving && currentState == EnemyState.Idle)
                currentState = EnemyState.Move;
            else if (!isMoving && currentState == EnemyState.Move)
                currentState = EnemyState.Idle;
        }
    }

    // ──────────────── 추적 (Run) ────────────────

    private void HandleChaseState()
    {
        if (moveModule == null) return;

        // 너무 멀어지면 배회로 복귀
        if (moveModule.IsPlayerOutOfRange())
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        // 공격 사거리 + 쿨다운 완료 → 돌진 시작
        if (moveModule.IsPlayerInAttackRange() && cooldownTimer <= 0f)
        {
            StartDash();
            return;
        }

        moveModule.HandleChase();

        if (animModule != null)
            animModule.UpdateMoveAnimation(true);
    }

    // ──────────────── 돌진 (Dash) ────────────────

    private void StartDash()
    {
        dashDirection = moveModule.GetDirectionToPlayer();
        dashTimer = dashDuration;
        hasSlammedThisAttack = false;
        ChangeState(EnemyState.Dash);
    }

    private void HandleDashState()
    {
        dashTimer -= Time.deltaTime;

        if (dashTimer > 0)
        {
            // 고속 돌진
            rb.linearVelocity = dashDirection * dashSpeed;
        }
        else
        {
            // 돌진 끝 → 슬램 대기
            rb.linearVelocity = Vector2.zero;
            slamTimer = slamDelay;
            attackPhase = SlimePhase.SlamWait;
            ChangeState(EnemyState.Attack);
        }
    }

    // ──────────────── AOE 슬램 공격 ────────────────

    private void HandleSlamState()
    {
        if (moveModule != null)
            moveModule.StopMoving();

        slamTimer -= Time.deltaTime;

        if (!hasSlammedThisAttack && slamTimer <= 0f)
        {
            // 슬램! 주변 범위 데미지
            PerformSlam();
            hasSlammedThisAttack = true;
            slamTimer = 0.3f; // 슬램 후 잠시 경직
        }
        else if (hasSlammedThisAttack && slamTimer <= 0f)
        {
            // 슬램 후 경직 끝 → 쿨다운 시작, 추적 복귀
            cooldownTimer = attackCooldown;
            ChangeState(EnemyState.Trigger);
        }
    }

    private void PerformSlam()
    {
        Debug.Log($"[Slime] {gameObject.name} SLAM! Radius: {slamRadius}");

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            PlayerController player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(slamDamage);
                }
                Debug.Log($"[Slime] Slam hit player for {slamDamage} damage!");
            }
        }
    }

    // ──────────────── 피격 ────────────────

    private float hitTimer;

    private void HandleHitState()
    {
        hitTimer -= Time.deltaTime;
        if (hitTimer <= 0f)
        {
            ChangeState(EnemyState.Trigger);
        }
    }

    public override void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Dead) return;

        // 돌진 중에는 경직 없이 데미지만 받음
        if (currentState == EnemyState.Dash)
        {
            // base 로직 중 HP 감소만 수동 처리
            // (base.TakeDamage는 ChangeState(Hit)를 호출하므로 직접 처리)
            TakeDamageWithoutStun(damage);
            return;
        }

        base.TakeDamage(damage);
        hitTimer = hitStunDuration;
    }

    private void TakeDamageWithoutStun(int damage)
    {
        currentHP -= damage;
        lastHitTime = Time.time;

        if (!hpBarCreated)
        {
            CreateHPBar();
            hpBarCreated = true;
        }
        UpdateHPBar();
        hpBarAlpha = 1f;
        StartShake();

        Debug.Log($"[Slime] {gameObject.name} took {damage} damage during dash! ({currentHP}/{maxHP}) (no stun)");

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
    }

    void OnDrawGizmosSelected()
    {
        // AOE 슬램 범위 (주황색)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
