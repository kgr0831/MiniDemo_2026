using UnityEngine;

// 플레이어 애니메이션 제어
[RequireComponent(typeof(Animator))]
public class PlayerAnim : MonoBehaviour 
{ 
    private Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    // 4방향 Blend Tree를 위한 파라미터 업데이트
    public void UpdateMoveAnimation(Vector2 currentInput, Vector2 lastDir, bool isMoving)
    {
        if (anim == null) return;

        // IsMove 상태 (Idle / Run을 블렌드 트리 바깥 트랜지션으로 뺄 때 유용)
        anim.SetBool("IsMove", isMoving);

        // 뛰고 있는지 걷고 있는지 판별 (PlayerMove에서 shift값을 참조)
        PlayerMove moveModule = GetComponent<PlayerMove>();
        bool isRunning = moveModule != null && moveModule.IsRunningPressed;
        anim.SetBool("IsRun", isRunning); // 애니메이터 파라미터 (Walk/Run 분기용)

        // 정지(Idle) 상태일 때는 마지막으로 바라보던 방향의 Idle이 재생되도록 유지
        Vector2 blendDir = isMoving ? currentInput : lastDir;
        
        // BlendTree에서 좌우, 상하 스프라이트가 흔들리지 않게 정확한 정수형(-1, 0, 1)에 가깝게 맞춰줌
        anim.SetFloat("x", Mathf.RoundToInt(blendDir.x));
        anim.SetFloat("y", Mathf.RoundToInt(blendDir.y));
    }

    // FSM 상태가 바뀔 때 Trigger를 호출해 단발성 애니메이션을 제어
    public void PlayStateAnim(PlayerState state) 
    { 
        if (anim == null) return;

        switch(state)
        {
            case PlayerState.Dash:
                anim.SetTrigger("Dash");
                break;
            
            // 추후 캐릭터 스킬 등 확장 시 사용 (예시)
            // case PlayerState.Attack: anim.SetTrigger("Attack"); break;
            // case PlayerState.Hit: anim.SetTrigger("Hit"); break;
        }
    } 
}
