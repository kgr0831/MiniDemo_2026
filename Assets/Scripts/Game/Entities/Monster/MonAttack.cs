using UnityEngine;

// 적 공격 메커니즘 제어 및 최종 연산 데미지 반환
public class MonAttack : MonoBehaviour 
{ 
    [Header("공격 설정")]
    public int attackDamage = 5;           // 공격 데미지
    public float attackCooldown = 1.5f;    // 공격 쿨다운 (초)
    public float attackDuration = 0.5f;    // 공격 모션 지속 시간
    public float attackHitboxRange = 0.8f; // 공격 판정 거리
    public float attackHitboxRadius = 0.5f;// 공격 판정 반지름

    private MonsterController controller;
    private MonMove moveModule;
    private float cooldownTimer = 0f;
    private float attackTimer = 0f;
    private bool hasHitThisSwing = false;  // 한 스윙에 한 번만 히트

    void Awake()
    {
        controller = GetComponent<MonsterController>();
        moveModule = GetComponent<MonMove>();
    }

    void Update()
    {
        // 쿨다운 타이머 감소
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 공격 쿨다운이 끝났는지
    /// </summary>
    public bool CanAttack()
    {
        return cooldownTimer <= 0f;
    }

    /// <summary>
    /// 공격 시작
    /// </summary>
    public void StartAttack()
    {
        attackTimer = 0f;
        hasHitThisSwing = false;
        cooldownTimer = attackCooldown;
    }

    /// <summary>
    /// 공격 모션 중 매 프레임 호출
    /// </summary>
    public bool HandleAttack()
    {
        attackTimer += Time.deltaTime;

        // 공격 모션의 중간 지점(40%)에서 히트 판정
        if (!hasHitThisSwing && attackTimer >= attackDuration * 0.4f)
        {
            PerformHitDetection();
            hasHitThisSwing = true;
        }

        // 공격 모션 끝
        return attackTimer >= attackDuration;
    }

    private void PerformHitDetection()
    {
        if (moveModule == null) return;

        Vector2 attackDir = moveModule.GetDirectionToPlayer();
        Vector2 attackPos = (Vector2)transform.position + (attackDir * attackHitboxRange);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, attackHitboxRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // 플레이어만 데미지
            PlayerController player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(attackDamage);
                }
                Debug.Log($"[Monster] {gameObject.name} attacked player for {attackDamage} damage!");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        MonMove move = GetComponent<MonMove>();
        if (move == null) return;
        
        Vector2 dir = move.lastMoveDir;
        Vector2 attackPos = (Vector2)transform.position + (dir * attackHitboxRange);
        
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(attackPos, attackHitboxRadius);
    }
}
