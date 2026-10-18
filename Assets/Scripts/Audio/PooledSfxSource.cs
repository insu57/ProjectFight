using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class PooledSfxSource : PoolableObject
{
    private AudioSource _audioSource;
    private bool _hasPlaybackRequest;
    private bool _waitingForClip;
    private bool _hasStartedPlaying;
    private double _expectedEndDspTime;

    // 외부 반환 후 같은 인스턴스가 재대여되어도 이전 Manager 기록이 새 소리를 정리하지 않게 한다.
    public uint PlaybackVersion { get; private set; }

    private void Awake()
    {
        TryGetComponent(out _audioSource);
        ResetPlayback();
    }

    protected override void OnPoolSpawned()
    {
        unchecked { PlaybackVersion++; }
        ResetPlayback();
    }

    protected override void OnPoolDespawned()
    {
        ResetPlayback();
    }

    private void OnDisable()
    {
        ResetPlayback();
    }

    public bool TryPlay(SoundDefinition sound, AudioMixerGroup mixerGroup, bool isSpatial)
    {
        if (!isActiveAndEnabled || !IsSpawned || !_audioSource || !sound || !sound.TryGetClip(out AudioClip clip)) return false;

        ResetPlayback();
        _audioSource.clip = clip;
        _audioSource.outputAudioMixerGroup = mixerGroup;
        _audioSource.volume = sound.Volume;
        _audioSource.pitch = UnityEngine.Random.Range(sound.MinPitch, sound.MaxPitch);
        _audioSource.spatialBlend = isSpatial ? 1f : 0f;
        _audioSource.minDistance = sound.MinDistance;
        _audioSource.maxDistance = sound.MaxDistance;
        _audioSource.rolloffMode = sound.RolloffMode;
        _audioSource.priority = sound.Priority;

        // Preload Audio Data가 꺼진 클립도 로딩이 끝날 때까지 같은 음원에서 대기한다.
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
        {
            ResetPlayback();
            return false;
        }
        if (clip.loadState == AudioDataLoadState.Failed)
        {
            ResetPlayback();
            return false;
        }

        _hasPlaybackRequest = true;
        _waitingForClip = clip.loadState != AudioDataLoadState.Loaded;
        if (!_waitingForClip) BeginPlayback();
        return true;
    }

    private void BeginPlayback()
    {
        _audioSource.Play();
        _hasStartedPlaying = _audioSource.isPlaying;
        // 오디오 스레드가 아직 시작하지 않았거나 한 프레임 사이에 짧은 클립이 끝나도 반환할 수 있다.
        _expectedEndDspTime = AudioSettings.dspTime + _audioSource.clip.length / _audioSource.pitch;
    }

    // 재생 또는 로딩 대기가 계속되면 true이며, 실제 반환은 소유 AudioManager가 처리한다.
    public bool TickPlayback()
    {
        if (!isActiveAndEnabled || !_audioSource || !_hasPlaybackRequest || !_audioSource.clip) return false;
        // Listener 일시정지는 isPlaying이 false여도 소리를 종료한 것으로 취급하지 않는다.
        if (AudioListener.pause) return true;

        if (_waitingForClip)
        {
            AudioDataLoadState loadState = _audioSource.clip.loadState;
            if (loadState == AudioDataLoadState.Failed) return false;
            if (loadState != AudioDataLoadState.Loaded) return true;
            _waitingForClip = false;
            BeginPlayback();
        }

        if (_audioSource.isPlaying)
        {
            _hasStartedPlaying = true;
            return true;
        }
        return !_hasStartedPlaying && AudioSettings.dspTime < _expectedEndDspTime;
    }

    public void StopAndReturn()
    {
        ResetPlayback();
        if (!ReturnToPool()) gameObject.SetActive(false);
    }

    private void ResetPlayback()
    {
        _hasPlaybackRequest = false;
        _waitingForClip = false;
        _hasStartedPlaying = false;
        _expectedEndDspTime = 0d;
        if (!_audioSource) return;

        _audioSource.Stop();
        _audioSource.clip = null;
        _audioSource.outputAudioMixerGroup = null;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.volume = 1f;
        _audioSource.pitch = 1f;
        _audioSource.spatialBlend = 1f;
        _audioSource.dopplerLevel = 0f;
        _audioSource.ignoreListenerPause = false;
    }
}
