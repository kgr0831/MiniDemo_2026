[System.Serializable]
public class StatData 
{
    public int hp; // 체력
    public float stamina; // 기력
    public float maxStamina; // 최대 기력
    public float staminaRecoveryRate; // 초당 기력 회복량
    public bool isExhausted; // 기력 탈진 (0이 되어 회복 중인 상태)
    public int attackPoint; // 공격력
    public int gatherPower; // 채집 능력 (나무, 바위 등 자원에 가하는 데미지)
    public float speed; // 속도
    public float attackSpeed; // 공격 속도
}
