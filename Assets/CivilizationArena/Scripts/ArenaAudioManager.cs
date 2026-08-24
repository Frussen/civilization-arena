using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaAudioManager : MonoBehaviour
{
    private const float MusicVolume = 0.25f;
    private const float UiClickVolume = 0.5f;
    private const float ResultJingleVolume = 0.6f;

    private static ArenaAudioManager instance;

    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip positiveResultClip;
    [SerializeField] private AudioClip defeatResultClip;

    private AudioSource musicSource;
    private AudioSource uiSfxSource;
    private AudioSource resultSfxSource;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = CreateSource(MusicVolume);
        musicSource.clip = musicClip;
        musicSource.loop = true;

        uiSfxSource = CreateSource(UiClickVolume);
        resultSfxSource = CreateSource(ResultJingleVolume);

        if (musicClip != null)
        {
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void PlayUiClick()
    {
        if (instance == null ||
            instance.uiSfxSource == null ||
            instance.uiClickClip == null)
        {
            return;
        }

        instance.uiSfxSource.PlayOneShot(instance.uiClickClip);
    }

    public static void PlayPositiveResultJingle()
    {
        PlayResultJingle(instance?.positiveResultClip);
    }

    public static void PlayDefeatResultJingle()
    {
        PlayResultJingle(instance?.defeatResultClip);
    }

    private static void PlayResultJingle(AudioClip clip)
    {
        if (instance == null ||
            instance.resultSfxSource == null ||
            clip == null)
        {
            return;
        }

        instance.resultSfxSource.PlayOneShot(clip);
    }

    private AudioSource CreateSource(float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = volume;
        source.spatialBlend = 0f;
        return source;
    }
}
