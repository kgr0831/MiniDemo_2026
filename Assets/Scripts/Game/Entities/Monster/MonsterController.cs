using UnityEngine;

// 적
public class MonsterController : MonoBehaviour, IDamageable
{
    public CharacterData monsterData; // 스탯 데이터
    public EnemyState currentState; // 적 상태 제어
    
    protected MonMove moveModule; // 적 움직임(이동, 회피)
    protected MonAnim animModule; // 적 애니메이션 제어

    protected virtual void Awake()
    {
        moveModule = GetComponent<MonMove>();
        animModule = GetComponent<MonAnim>();
    }

    // 최종 연산 데미지 적용
    public virtual void TakeDamage(int damage)
    {
        ChangeState(EnemyState.Hit); // 피격 당함
    }

    public void ChangeState(EnemyState newState) 
    { 
        currentState = newState; 
    }
}
