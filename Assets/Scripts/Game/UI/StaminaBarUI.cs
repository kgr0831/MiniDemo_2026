using UnityEngine;
using UnityEngine.UI;

// 기력 UI
public class StaminaBarUI : MonoBehaviour 
{ 
    Image staminaFillImage; 

    [SerializeField] PlayerController targetPlayer;
    [SerializeField] RectTransform frameRectTransform; // 기력바 테두리 프레임 UI

    private RectTransform rectTransform;
    private bool wasFullLastFrame = true; // 스태미나가 이전 프레임에 100% 였는지 기억
    private Image frameImage; // 프레임 투명도 조절용

    // 페이드 아웃(Fade Out) 변수
    private float fullStaminaTimer = 0f;
    private float fadeAlpha = 1f;
    private float fadeSpeed = 3f;

    // 팝업 애니메이션용 변수
    private float popTimer = 0f;
    private float popDuration = 0.4f;
    private float bloomIntensity = 5.0f; // 쉐이더 _Intensity로 전달됨 (1.0 = 평소, 5.0 = Bloom 발동)
    private Vector3 originalScale;
    private Vector3 originalFrameScale;

    // Material 인스턴스 (Custom/UI_Glow 쉐이더의 _Intensity를 제어)
    private Material fillMaterialInstance;
    private Material frameMaterialInstance;

    void Start()
    {
        staminaFillImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        
        if (rectTransform != null)
            originalScale = rectTransform.localScale;

        if (frameRectTransform != null)
        {
            originalFrameScale = frameRectTransform.localScale;
            frameImage = frameRectTransform.GetComponent<Image>();
        }

        // Material 인스턴스 복제 (원본 머티리얼 오염 방지)
        if (staminaFillImage != null && staminaFillImage.material != null)
        {
            fillMaterialInstance = Instantiate(staminaFillImage.material);
            staminaFillImage.material = fillMaterialInstance;
        }
        if (frameImage != null && frameImage.material != null)
        {
            frameMaterialInstance = Instantiate(frameImage.material);
            frameImage.material = frameMaterialInstance;
        }
    }

    void Update()
    {
        if (targetPlayer != null && targetPlayer.myData != null && targetPlayer.myData.stats != null)
        {
            float current = targetPlayer.myData.stats.stamina;
            float max = targetPlayer.myData.stats.maxStamina;
            bool isExhausted = targetPlayer.myData.stats.isExhausted;

            UpdateStamina(current, max, isExhausted);
        }
    }

    public void UpdateStamina(float currentStamina, float maxStamina, bool isExhausted)
    {
        if (staminaFillImage != null && maxStamina > 0f)
        {
            float fillRatio = currentStamina / maxStamina;
            staminaFillImage.fillAmount = fillRatio;

            bool isFullNow = currentStamina >= maxStamina;

            // 1. 방금 막 기력이 100% 도달했는지 체크
            if (!wasFullLastFrame && isFullNow)
            {
                popTimer = 1f;
            }
            
            // 2. 팝업 & Bloom 애니메이션
            if (popTimer > 0f)
            {
                popTimer -= Time.deltaTime / popDuration;
                
                // 크기 팝
                float scalePop = Mathf.Lerp(1.0f, 1.3f, popTimer);
                if (rectTransform != null)
                    rectTransform.localScale = originalScale * scalePop;
                if (frameRectTransform != null)
                    frameRectTransform.localScale = originalFrameScale * scalePop;

                // ★ 쉐이더의 _Intensity를 직접 제어 → Bloom Threshold를 넘기는 핵심!
                float currentIntensity = Mathf.Lerp(1.0f, bloomIntensity, popTimer * popTimer);
                SetMaterialIntensity(currentIntensity);

                staminaFillImage.color = Color.white;
            }
            else
            {
                // 평소 상태
                if (rectTransform != null) rectTransform.localScale = originalScale;
                if (frameRectTransform != null) frameRectTransform.localScale = originalFrameScale;
                
                staminaFillImage.color = isExhausted ? Color.red : Color.white;
                SetMaterialIntensity(1.0f);
            }

            // 3. 페이드 아웃
            if (currentStamina >= maxStamina && !isExhausted)
            {
                fullStaminaTimer += Time.deltaTime;
                if (fullStaminaTimer >= 1.0f)
                    fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, fadeSpeed * Time.deltaTime);
            }
            else
            {
                fullStaminaTimer = 0f;
                fadeAlpha = 1f;
            }

            SetImageAlpha(staminaFillImage, fadeAlpha);
            if (frameImage != null)
                SetImageAlpha(frameImage, fadeAlpha);

            wasFullLastFrame = isFullNow;
        }
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private void SetMaterialIntensity(float intensity)
    {
        if (fillMaterialInstance != null)
            fillMaterialInstance.SetFloat("_Intensity", intensity);
        if (frameMaterialInstance != null)
            frameMaterialInstance.SetFloat("_Intensity", intensity);
    }
}
