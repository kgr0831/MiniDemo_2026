using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 각 방향별 공격 히트박스 오브젝트에 부착하는 스크립트.
/// BoxCollider2D(IsTrigger)로 적과 충돌을 감지하고, PlayerAttack에 보고합니다.
/// 같은 공격 스윙 중 같은 대상을 중복 히트하지 않습니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class AttackHitbox : MonoBehaviour
{
    private PlayerAttack playerAttack;
    private BoxCollider2D hitboxCollider;

    // 한 번의 공격(스윙) 동안 이미 히트한 대상을 기록하여 중복 판정 방지
    private HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider2D>();
        playerAttack = GetComponentInParent<PlayerAttack>();
    }

    void Start()
    {
        // Start에서 콜라이더 끄기 (Awake 이후이므로 확실히 초기화됨)
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    /// <summary>
    /// 히트박스 콜라이더를 활성화합니다.
    /// </summary>
    public void EnableHitbox()
    {
        alreadyHit.Clear();
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
    }

    /// <summary>
    /// 히트박스 콜라이더를 비활성화합니다.
    /// </summary>
    public void DisableHitbox()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
        alreadyHit.Clear();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (alreadyHit.Contains(other)) return;
        if (playerAttack != null && other.gameObject == playerAttack.gameObject) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null && playerAttack != null)
        {
            // 자원 오브젝트(나무, 바위 등)이면 gatherPower 기반 데미지
            // 몬스터 등 일반 대상이면 공격력 기반 데미지
            int damage;
            if (other.GetComponent<ResourceEntity>() != null)
            {
                damage = playerAttack.CalculateGatherDamage();
            }
            else
            {
                damage = playerAttack.CalculateDamage();
            }

            damageable.TakeDamage(damage);
            alreadyHit.Add(other);
            Debug.Log($"[Hitbox] {gameObject.name} hit {other.name} for {damage} damage!");
        }
    }
}
