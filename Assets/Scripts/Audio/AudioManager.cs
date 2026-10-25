using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AudioManager : Singleton<AudioManager>
{
    private const float MinimumVolumeDb = -80f;
    private const int MasterVolumeMask = 1;
    private const int BgmVolumeMask = 2;
    private const int SfxVolumeMask = 4;
    private const int AllVolumeMask = MasterVolumeMask | BgmVolumeMask | SfxVolumeMask;

    private struct ActiveSfx
    {
        public PooledSfxSource Source;
        public uint PlaybackVersion;
        public bool IsEnemyFootstep;
    }

    [Header("Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;
    [SerializeField] private string _masterVolumeParameter = "MasterVolume";
    [SerializeField] private string _bgmVolumeParameter = "BgmVolume";
    [SerializeField] private string _sfxVolumeParameter = "SfxVolume";

    [Header("SFX 풀")]
    [SerializeField] private PooledSfxSource _sfxSourcePrefab;
    [SerializeField, Min(0)] private int _sfxPrewarmCount = 16;
    [SerializeField, Min(1)] private int _maxConcurrentSfx = 32;

    [Header("적 발소리 제한")]
    [SerializeField, Min(0f), Tooltip("적 전체에서 발소리 요청을 허용하는 최소 간격입니다. 제한된 요청은 대기하지 않고 생략합니다.")]
    private float _enemyFootstepMinInterval = 0.06f;
    [SerializeField, Min(1), Tooltip("플레이어 발소리와 별도로 제한하는 적 발소리의 최대 동시 재생 수입니다.")]
    private int _maxConcurrentEnemyFootsteps = 3;

    [Header("옵션 음량")]
    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

    private readonly List<ActiveSfx> _activeSfx = new(32);
    private AudioSource _bgmSource;
    private int _pendingSfxSpawns;
    private int _pendingEnemyFootstepSpawns;
    private double _nextEnemyFootstepDspTime;
    private int _dirtyVolumeMask = AllVolumeMask;
    private bool _hasStarted;
    private bool _isStoppingSfx;

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;
    public int ActiveSfxCount => _activeSfx.Count;

    protected override void OnSingletonAwake()
    {
        TryGetComponent(out _bgmSource);
        if (_bgmSource)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.dopplerLevel = 0f;
            _bgmSource.pitch = 1f;
            _bgmSource.priority = 0;
            _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
        }
        _activeSfx.Capacity = Mathf.Max(_activeSfx.Capacity, Mathf.Max(1, _maxConcurrentSfx));
    }

    private void Start()
    {
        if (Instance != this || !isActiveAndEnabled) return;

        _hasStarted = true;
        ApplyPendingVolumes();
        if (!_audioMixer || !_bgmMixerGroup || !_sfxMixerGroup)
        {
            Debug.LogWarning("AudioManager에 AudioMixer와 BGM/SFX 출력 그룹을 연결해야 옵션 음량이 모든 소리에 적용됩니다.", this);
        }

        // 모든 Singleton의 Awake가 끝난 뒤 기존 공용 풀에 SFX 프리팹을 준비한다.
        PoolingManager pool = PoolingManager.Instance;
        if (pool && pool.isActiveAndEnabled && _sfxSourcePrefab)
        {
            pool.Prewarm(_sfxSourcePrefab, Mathf.Clamp(_sfxPrewarmCount, 0, Mathf.Max(1, _maxConcurrentSfx)));
        }
        else
        {
            Debug.LogWarning("SFX 재생에는 활성 PoolingManager와 PooledSfxSource 프리팹이 필요합니다.", this);
        }
    }

    private void Update()
    {
        if (_hasStarted) ApplyPendingVolumes();
        UpdateSfx();
    }

    public bool PlayBgm(AudioClip clip, bool loop = true, float volume = 1f)
    {
        if (Instance != this || !isActiveAndEnabled || !_bgmSource || !clip
            || clip.loadState == AudioDataLoadState.Failed || !IsFinite(volume)) return false;

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.pitch = 1f;
        _bgmSource.loop = loop;
        _bgmSource.volume = Mathf.Clamp01(volume);
        _bgmSource.Play();
        return true;
    }

    public void StopBgm()
    {
        if (!_bgmSource) return;
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    public bool PlaySfx(SoundDefinition sound, Vector3 position)
    {
        if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) return false;
        return TryPlaySfx(sound, position, true);
    }

    public bool PlaySfx2D(SoundDefinition sound)
    {
        return TryPlaySfx(sound, Vector3.zero, false);
    }

    public bool PlayFootstep(SoundDefinition sound, Vector3 position, bool isPlayer)
    {
        if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) return false;
        // 같은 SoundDefinition을 공유해도 호출 주체로 구분해 플레이어는 적 발소리 제한에서 제외한다.
        return TryPlaySfx(sound, position, true, !isPlayer);
    }

    private bool TryPlaySfx(SoundDefinition sound, Vector3 position, bool isSpatial, bool isEnemyFootstep = false)
    {
        if (Instance != this || !isActiveAndEnabled || _isStoppingSfx || !sound || !_sfxSourcePrefab) return false;
        UpdateSfx();
        if (_activeSfx.Count + _pendingSfxSpawns >= Mathf.Max(1, _maxConcurrentSfx)) return false;
        if (isEnemyFootstep && !CanPlayEnemyFootstep()) return false;

        PoolingManager pool = PoolingManager.Instance;
        if (!pool || !pool.isActiveAndEnabled) return false;

        // Spawn의 OnEnable에서 다른 재생 요청이 들어와도 예약 수를 포함해 상한을 지킨다.
        _pendingSfxSpawns++;
        if (isEnemyFootstep) _pendingEnemyFootstepSpawns++;
        try
        {
            PooledSfxSource source = pool.Spawn(_sfxSourcePrefab, position, Quaternion.identity);
            if (!source) return false;
            if (!isActiveAndEnabled || _isStoppingSfx || !source.TryPlay(sound, _sfxMixerGroup, isSpatial))
            {
                source.StopAndReturn();
                return false;
            }
            _activeSfx.Add(new ActiveSfx { Source = source, PlaybackVersion = source.PlaybackVersion, IsEnemyFootstep = isEnemyFootstep });
            // 풀 대여나 재생이 실패한 요청은 다음 적의 발소리까지 막지 않는다.
            if (isEnemyFootstep) _nextEnemyFootstepDspTime = AudioSettings.dspTime + _enemyFootstepMinInterval;
            return true;
        }
        finally
        {
            _pendingSfxSpawns--;
            if (isEnemyFootstep) _pendingEnemyFootstepSpawns--;
        }
    }

    private bool CanPlayEnemyFootstep()
    {
        // Spawn 콜백 중 들어온 요청도 차단하며, 시간 배율과 무관한 오디오 시간으로 간격을 유지한다.
        if (_pendingEnemyFootstepSpawns > 0 || AudioSettings.dspTime < _nextEnemyFootstepDspTime) return false;

        int activeEnemyFootsteps = 0;
        for (int i = 0; i < _activeSfx.Count; i++)
        {
            if (_activeSfx[i].IsEnemyFootstep) activeEnemyFootsteps++;
        }
        return activeEnemyFootsteps < Mathf.Max(1, _maxConcurrentEnemyFootsteps);
    }

    private void UpdateSfx()
    {
        for (int i = _activeSfx.Count - 1; i >= 0; i--)
        {
            // 반환 콜백에서 전체 정리가 요청되어도 이미 제거된 항목에 접근하지 않는다.
            if (i >= _activeSfx.Count) continue;
            ActiveSfx entry = _activeSfx[i];
            PooledSfxSource source = entry.Source;
            bool samePlayback = source && source.IsSpawned && source.PlaybackVersion == entry.PlaybackVersion;
            if (samePlayback && source.TickPlayback()) continue;

            int lastIndex = _activeSfx.Count - 1;
            _activeSfx[i] = _activeSfx[lastIndex];
            _activeSfx.RemoveAt(lastIndex);
            if (samePlayback) source.StopAndReturn();
        }
    }

    public void StopAllSfx()
    {
        if (_isStoppingSfx) return;
        _isStoppingSfx = true;
        try
        {
            for (int i = _activeSfx.Count - 1; i >= 0; i--)
            {
                ActiveSfx entry = _activeSfx[i];
                _activeSfx.RemoveAt(i);
                if (entry.Source && entry.Source.IsSpawned && entry.Source.PlaybackVersion == entry.PlaybackVersion) entry.Source.StopAndReturn();
            }
        }
        finally
        {
            _nextEnemyFootstepDspTime = 0d;
            _isStoppingSfx = false;
        }
    }

    public void SetMasterVolume(float volume)
    {
        SetVolume(ref _masterVolume, volume, MasterVolumeMask);
    }

    public void SetBgmVolume(float volume)
    {
        SetVolume(ref _bgmVolume, volume, BgmVolumeMask);
    }

    public void SetSfxVolume(float volume)
    {
        SetVolume(ref _sfxVolume, volume, SfxVolumeMask);
    }

    private void SetVolume(ref float destination, float volume, int mask)
    {
        if (!IsFinite(volume)) return;
        destination = Mathf.Clamp01(volume);
        // 옵션 UI의 OnEnable에서 호출해도 Mixer 변경은 Start 또는 Update에서만 수행한다.
        _dirtyVolumeMask |= mask;
    }

    private void ApplyPendingVolumes()
    {
        if (!_audioMixer || _dirtyVolumeMask == 0) return;
        int dirty = _dirtyVolumeMask;
        _dirtyVolumeMask = 0;
        if ((dirty & MasterVolumeMask) != 0 && !ApplyMixerVolume(_masterVolumeParameter, _masterVolume))
            Debug.LogWarning("AudioMixer의 MasterVolume 노출 파라미터 이름을 확인하세요.", this);
        if ((dirty & BgmVolumeMask) != 0 && !ApplyMixerVolume(_bgmVolumeParameter, _bgmVolume))
            Debug.LogWarning("AudioMixer의 BgmVolume 노출 파라미터 이름을 확인하세요.", this);
        if ((dirty & SfxVolumeMask) != 0 && !ApplyMixerVolume(_sfxVolumeParameter, _sfxVolume))
            Debug.LogWarning("AudioMixer의 SfxVolume 노출 파라미터 이름을 확인하세요.", this);
    }

    private bool ApplyMixerVolume(string parameter, float volume)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return false;
        float db = volume <= 0f ? MinimumVolumeDb : Mathf.Max(MinimumVolumeDb, 20f * Mathf.Log10(volume));
        return _audioMixer.SetFloat(parameter, db);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnValidate()
    {
        _maxConcurrentSfx = Mathf.Max(1, _maxConcurrentSfx);
        _sfxPrewarmCount = Mathf.Clamp(_sfxPrewarmCount, 0, _maxConcurrentSfx);
        _enemyFootstepMinInterval = IsFinite(_enemyFootstepMinInterval) ? Mathf.Max(0f, _enemyFootstepMinInterval) : 0.06f;
        _maxConcurrentEnemyFootsteps = Mathf.Max(1, _maxConcurrentEnemyFootsteps);
        _masterVolume = IsFinite(_masterVolume) ? Mathf.Clamp01(_masterVolume) : 1f;
        _bgmVolume = IsFinite(_bgmVolume) ? Mathf.Clamp01(_bgmVolume) : 1f;
        _sfxVolume = IsFinite(_sfxVolume) ? Mathf.Clamp01(_sfxVolume) : 1f;
        _dirtyVolumeMask = AllVolumeMask;
    }

    private void OnDisable()
    {
        StopAllSfx();
        StopBgm();
    }

    protected override void OnSingletonDestroyed()
    {
        StopAllSfx();
        StopBgm();
    }
}
