using UnityEngine;

// 플레이어 애니메이션 제어
[RequireComponent(typeof(Animator))]
public class PlayerAnim : MonoBehaviour 
{ 
    public Animator anim { get; private set; }
    private PlayerController controller;

    void Awake()
    {
        anim = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
    }

    // 4방향 Blend Tree를 위한 파라미터 업데이트
    public void UpdateMoveAnimation(Vector2 currentInput, Vector2 lastDir, bool isMoving)
    {
        if (anim == null) return;

        // IsMove 상태 (Idle / Run을 블렌드 트리 바깥 트랜지션으로 뺄 때 유용)
        anim.SetBool("IsMove", isMoving);

        // 뛰고 있는지 걷고 있는지 판별 (단순히 Shift 키 입력이 아니라, Controller에서 실제로 Move 상태인지 확인)
        bool isRunning = controller != null && controller.currentState == PlayerState.Move;
        anim.SetBool("IsRun", isRunning); // 애니메이터 파라미터 (Walk/Run 분기용)

        // 정지(Idle) 상태일 때는 마지막으로 바라보던 방향의 Idle이 재생되도록 유지
        Vector2 blendDir = isMoving ? currentInput : lastDir;
        
        // BlendTree에서 좌우, 상하 스프라이트가 흔들리지 않게 정확한 정수형(-1, 0, 1)에 가깝게 맞춰줌
        anim.SetFloat("x", Mathf.RoundToInt(blendDir.x));
        anim.SetFloat("y", Mathf.RoundToInt(blendDir.y));

        // 스피드 스탯(기본 3.0)에 비례하여 걷기/달리기 애니메이션 재생 속도 조절 (Animator에서 MoveSpeed 파라미터 적용 필요)
        float baseSpeed = 3.0f;
        if (controller != null && controller.myData != null && controller.myData.stats != null)
        {
            baseSpeed = controller.myData.stats.speed;
            if (baseSpeed <= 0.1f) baseSpeed = 3.0f;
        }
        anim.SetFloat("MoveSpeed", baseSpeed / 3.0f);
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
                
            // Attack 애니메이션은 콤보 카운트에 따라 외부(PlayerAttack)에서 별도 함수(PlayAttackAnim)로 제어합니다.
                
            case PlayerState.Tool: 
                anim.SetTrigger("Tool"); 
                break;
                
            case PlayerState.Hit: 
                anim.SetTrigger("Hit"); 
                break;
        }
    } 
    
    // 공격 콤보 전용 애니메이션 트리거 함수
    public void PlayAttackAnim(int comboStep)
    {
        if (anim == null) return;
        
        // 스탯의 공격 속도 반영 (기본값 1.0f)
        float currentAttackSpeed = 1.0f;
        if (controller != null && controller.myData != null && controller.myData.stats != null)
        {
            currentAttackSpeed = controller.myData.stats.attackSpeed;
            if (currentAttackSpeed <= 0f) currentAttackSpeed = 1.0f; // 0 이하 방지용 보정
        }
        
        // 애니메이터에 연결된 애니메이션 클립 재생 속도를 조절하는 파라미터 (사용자님이 설정하신 'AttackSpeed' 파라미터와 동기화)
        anim.SetFloat("AttackSpeed", currentAttackSpeed);
        
        if (comboStep == 1)      anim.SetTrigger("Attack1");
        else if (comboStep == 2) anim.SetTrigger("Attack2");
    }
}
