using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundDefinition", menuName = "Scriptable Objects/Sound Definition")]
public sealed class SoundDefinition : ScriptableObject
{
    [SerializeField, Tooltip("같은 소리의 변형 클립입니다. 재생할 때 유효한 클립 중 하나를 균등 선택합니다.")]
    private AudioClip[] _clips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField, Range(0.01f, 3f)] private float _minPitch = 0.95f;
    [SerializeField, Range(0.01f, 3f)] private float _maxPitch = 1.05f;

    [Header("3D 거리 감쇠")]
    [SerializeField, Min(0.01f)] private float _minDistance = 1f;
    [SerializeField, Min(0.01f)] private float _maxDistance = 15f;
    [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;
    [SerializeField, Range(0, 256), Tooltip("Unity 음성 우선순위입니다. 작을수록 높으며, SFX 동시 재생 상한을 넘기면 새 요청은 재생하지 않습니다.")]
    private int _priority = 128;

    // 공유 데이터는 재생 중 변경하지 않으며 스크립트에서 잘못된 값이 들어와도 Source에 유효한 설정만 전달한다.
    public float Volume => IsFinite(_volume) ? Mathf.Clamp01(_volume) : 1f;
    public float MinPitch => IsFinite(_minPitch) ? Mathf.Clamp(_minPitch, 0.01f, 3f) : 1f;
    public float MaxPitch => IsFinite(_maxPitch) ? Mathf.Clamp(_maxPitch, MinPitch, 3f) : MinPitch;
    public float MinDistance => IsFinite(_minDistance) ? Mathf.Max(0.01f, _minDistance) : 1f;
    public float MaxDistance => IsFinite(_maxDistance) ? Mathf.Max(MinDistance + 0.01f, _maxDistance) : Mathf.Max(MinDistance + 0.01f, 15f);
    public AudioRolloffMode RolloffMode => _rolloffMode == AudioRolloffMode.Logarithmic || _rolloffMode == AudioRolloffMode.Custom ? _rolloffMode : AudioRolloffMode.Linear;
    public int Priority => Mathf.Clamp(_priority, 0, 256);

    public bool TryGetClip(out AudioClip clip)
    {
        clip = null;
        if (_clips == null || _clips.Length == 0) return false;

        int validCount = 0;
        for (int i = 0; i < _clips.Length; i++)
        {
            if (IsUsableClip(_clips[i])) validCount++;
        }
        if (validCount == 0) return false;

        // 임시 후보 목록을 만들지 않고 빈 칸과 로딩 실패 클립을 제외해 추첨한다.
        int selected = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < _clips.Length; i++)
        {
            AudioClip candidate = _clips[i];
            if (!IsUsableClip(candidate)) continue;
            if (selected-- != 0) continue;
            clip = candidate;
            return true;
        }
        return false;
    }

    private static bool IsUsableClip(AudioClip clip)
    {
        if (!clip || clip.loadState == AudioDataLoadState.Failed) return false;
        float length = clip.length;
        return IsFinite(length) && length > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnValidate()
    {
        _volume = Volume;
        _minPitch = MinPitch;
        _maxPitch = MaxPitch;
        _minDistance = MinDistance;
        _maxDistance = MaxDistance;
        _rolloffMode = RolloffMode;
        _priority = Priority;
    }
}
