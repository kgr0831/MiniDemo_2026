using UnityEngine;

// 적 애니메이션 제어
[RequireComponent(typeof(Animator))]
public class MonAnim : MonoBehaviour 
{ 
    public Animator anim { get; private set; }
    private MonMove moveModule;

    void Awake()
    {
        anim = GetComponent<Animator>();
        moveModule = GetComponent<MonMove>();
    }

    /// <summary>
    /// 이동 애니메이션 파라미터 업데이트 (Blend Tree용)
    /// </summary>
    public void UpdateMoveAnimation(bool isMoving)
    {
        if (anim == null) return;

        anim.SetBool("IsMove", isMoving);

        if (moveModule != null)
        {
            Vector2 dir = moveModule.lastMoveDir;
            anim.SetFloat("x", Mathf.RoundToInt(dir.x));
            anim.SetFloat("y", Mathf.RoundToInt(dir.y));
        }
    }

    /// <summary>
    /// 상태 전환 시 애니메이션 트리거
    /// </summary>
    public void PlayStateAnim(EnemyState state)
    {
        if (anim == null) return;

        switch (state)
        {
            case EnemyState.Attack:
                anim.SetTrigger("Attack");
                break;
            case EnemyState.Hit:
                anim.SetTrigger("Hit");
                break;
            case EnemyState.Dead:
                anim.SetTrigger("Dead");
                break;
        }
    }
}
