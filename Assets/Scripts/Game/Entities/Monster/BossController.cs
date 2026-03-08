using UnityEngine;

// Monster를 상속 (보스 몬스터)
public class BossController : MonsterController
{
    protected override void Awake()
    {
        base.Awake();
        // 보스 특수 패턴 초기화
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
        // 페이즈 전환 등 기믹
    }
}
