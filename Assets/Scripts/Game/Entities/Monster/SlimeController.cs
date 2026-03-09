using UnityEngine;

/// <summary>
/// 슬라임 몬스터.
/// 행동 패턴:
///   배회(Walk) → 플레이어 감지 → 추적(Run) → 범위 내 도달 시 제자리 주변 범위 공격(AOE Slam) → 쿨다운 반복
/// </summary>
public class SlimeController : MonsterController
{
    [Header("슬라임 전용 설정")]
    public float slamRadius = 1.5f;        // AOE 슬램 범위
    public int slamDamage = 8;             // 슬램 데미지
    public float attackCooldown = 2.5f;    // 공격 쿨다운

    // 내부 변수
    private float slamTimer;
    private float cooldownTimer;
    private Rigidbody2D rb;
    private bool hasSlammedThisAttack;

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

        // 플레이어 시야에서 사라지면 배회 (수정된 로직에선 안 일어남)
        if (moveModule.IsPlayerOutOfRange())
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        // 공격 사거리 내에 도달 & 쿨다운 완료 → 바로 제자리 공격 시작
        if (moveModule.IsPlayerInAttackRange() && cooldownTimer <= 0f)
        {
            StartSlamAttack();
            return;
        }

        moveModule.HandleChase();

        if (animModule != null)
            animModule.UpdateMoveAnimation(true);
    }

    // ──────────────── AOE 슬램 공격 ────────────────

    private void StartSlamAttack()
    {
        hasSlammedThisAttack = false;
        slamTimer = 0.5f; // 슬램 모션 딜레이 (애니메이션 길이에 맞춤)
        ChangeState(EnemyState.Attack);
    }

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
            ChangeState(EnemyState.Trigger); // 한대 맞으면 무조건 쫓아감
        }
    }

    public override void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Dead) return;

        base.TakeDamage(damage);
        hitTimer = hitStunDuration;
    }

    void OnDrawGizmosSelected()
    {
        // AOE 슬램 범위 (주황색)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
