using UnityEngine;

public class MonsterController : MonoBehaviour, IDamageable
{
    public CharacterData monsterData;
    public EnemyState currentState;
    
    protected MonMove moveModule;
    protected MonAnim animModule;

    protected virtual void Awake()
    {
        moveModule = GetComponent<MonMove>();
        animModule = GetComponent<MonAnim>();
    }

    public virtual void TakeDamage(int damage)
    {
        ChangeState(EnemyState.Hit);
    }

    public void ChangeState(EnemyState newState) 
    { 
        currentState = newState; 
    }
}
