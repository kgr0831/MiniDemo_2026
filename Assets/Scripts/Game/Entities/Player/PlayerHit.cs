using UnityEngine;

// 플레이어 피격 / 스턴 및 플레이어에게 가해지는 최종 데미지 적용
public class PlayerHit : MonoBehaviour, IDamageable 
{
    private PlayerController controller;

    public bool IsInvincible { get; set; } = false; // 무적 상태 플래그

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public void TakeDamage(int damage)
    {
        // 무적 상태이거나 이미 죽었으면 데미지 무시
        if (IsInvincible || controller == null || controller.currentState == PlayerState.Dead) return;

        if (controller != null)
        {
            controller.ChangeState(PlayerState.Hit); // 피격 당함
        }
    }
}
