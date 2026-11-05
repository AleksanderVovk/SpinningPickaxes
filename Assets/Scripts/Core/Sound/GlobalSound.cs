using UnityEngine;

public class GlobalSound : MonoBehaviour
{
    public static GlobalSound instance;

    public AudioSource audioSource;

    [SerializeField] private AudioClip _levelUp;
    [SerializeField] private AudioClip _clickDefault;
    [SerializeField] private AudioClip _closeDefault;
    [SerializeField] private AudioClip _shortPop;
    [SerializeField] private AudioClip _buyTheme1;
    [SerializeField] private AudioClip _buyTheme2;
    [SerializeField] private AudioClip _buyTheme3;
    [SerializeField] private AudioClip _buyFailed;
    [SerializeField] private AudioClip _rebirthComplete;

    private void Awake()
    {
        instance = this;
    }

    public void PlaySound_LevelUp(bool needPitch)
    {
        PlaySound_Custom(_levelUp, needPitch);
    }

    public void PlaySound_ClickDefault(bool needPitch)
    {
        PlaySound_Custom(_clickDefault, needPitch);
    }

    public void PlaySound_CloseDefault(bool needPitch)
    {
        PlaySound_Custom(_closeDefault, needPitch);
    }

    public void PlaySound_ShortPop(bool needPitch)
    {
        PlaySound_Custom(_shortPop, needPitch);
    }

    public void PlaySound_BuyTheme1(bool needPitch)
    {
        PlaySound_Custom(_buyTheme1, needPitch);
    }

    public void PlaySound_BuyTheme2(bool needPitch)
    {
        PlaySound_Custom(_buyTheme2, needPitch);
    }

    public void PlaySound_BuyTheme3(bool needPitch)
    {
        PlaySound_Custom(_buyTheme3, needPitch);
    }

    public void PlaySound_BuyFaield(bool needPitch)
    {
        PlaySound_Custom(_buyFailed, needPitch);
    }

    public void PlaySound_RebirthComplete(bool needPitch)
    {
        PlaySound_Custom(_rebirthComplete, needPitch);
    }

    public void PlaySound_Custom(AudioClip audioClip, bool needPitch)
    {
        if (audioSource == null) return;

        if (needPitch)
            audioSource.pitch = Random.Range(0.95f, 1f);
        else
            audioSource.pitch = 1;

        audioSource.PlayOneShot(audioClip);
    }
}
