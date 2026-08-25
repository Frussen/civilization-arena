using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaAudioManager : MonoBehaviour
{
    public const float DefaultMusicVolume = 0.25f;
    public const float DefaultSfxVolume = 0.5f;

    private const float ResultJingleRelativeGain = 1.2f;
    private const string MusicVolumePreferenceKey =
        "CivilizationArena.MusicVolume";
    private const string SfxVolumePreferenceKey =
        "CivilizationArena.SfxVolume";

    private static ArenaAudioManager instance;
    private static float currentMusicVolume = DefaultMusicVolume;
    private static float currentSfxVolume = DefaultSfxVolume;
    private static bool preferencesLoaded;

    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip positiveResultClip;
    [SerializeField] private AudioClip defeatResultClip;

    private AudioSource musicSource;
    private AudioSource uiSfxSource;
    private AudioSource resultSfxSource;

    public static float CurrentMusicVolume
    {
        get
        {
            EnsurePreferencesLoaded();
            return currentMusicVolume;
        }
    }

    public static float CurrentSfxVolume
    {
        get
        {
            EnsurePreferencesLoaded();
            return currentSfxVolume;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        currentMusicVolume = DefaultMusicVolume;
        currentSfxVolume = DefaultSfxVolume;
        preferencesLoaded = false;
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
        EnsurePreferencesLoaded();

        musicSource = CreateSource(currentMusicVolume);
        musicSource.clip = musicClip;
        musicSource.loop = true;

        uiSfxSource = CreateSource(currentSfxVolume);
        resultSfxSource = CreateSource(GetResultJingleVolume());

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

    public static void SetMusicVolume(float volume)
    {
        EnsurePreferencesLoaded();
        currentMusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(
            MusicVolumePreferenceKey,
            currentMusicVolume);

        if (instance != null && instance.musicSource != null)
        {
            instance.musicSource.volume = currentMusicVolume;
        }
    }

    public static void SetSfxVolume(float volume)
    {
        EnsurePreferencesLoaded();
        currentSfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, currentSfxVolume);

        if (instance == null)
        {
            return;
        }

        if (instance.uiSfxSource != null)
        {
            instance.uiSfxSource.volume = currentSfxVolume;
        }

        if (instance.resultSfxSource != null)
        {
            instance.resultSfxSource.volume = GetResultJingleVolume();
        }
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

    private static float GetResultJingleVolume()
    {
        return Mathf.Clamp01(
            currentSfxVolume * ResultJingleRelativeGain);
    }

    private static void EnsurePreferencesLoaded()
    {
        if (preferencesLoaded)
        {
            return;
        }

        currentMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(
            MusicVolumePreferenceKey,
            DefaultMusicVolume));
        currentSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(
            SfxVolumePreferenceKey,
            DefaultSfxVolume));
        preferencesLoaded = true;
    }
}
