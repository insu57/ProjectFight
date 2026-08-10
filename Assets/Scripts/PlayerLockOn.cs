using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLockOn : MonoBehaviour
{
    [Header("Target Search")]
    [SerializeField, Min(0f)] private float _searchRange = 15f;
    [SerializeField] private LayerMask _targetLayerMask;
    [SerializeField, Range(0f, 0.49f)] private float _viewportPadding = 0.05f;
    [SerializeField, Min(1)] private int _maxCandidateCount = 32;

    [Header("Camera")]
    [SerializeField] private Camera _viewCamera;

    [Header("Runtime")]
    [SerializeField] private LockOnTarget _currentTarget;

    private Collider[] _candidateBuffer;

    public LockOnTarget CurrentTarget => _currentTarget;
    public bool HasTarget => _currentTarget;

    private void Awake()
    {
        if (!_viewCamera)
        {
            _viewCamera = Camera.main;
        }

        // 탐색 시마다 배열을 생성하지 않도록 검색 버퍼를 한 번만 할당한다.
        _candidateBuffer = new Collider[_maxCandidateCount];
    }

    public bool TryAcquireTarget()
    {
        _currentTarget = FindBestTarget();
        Debug.Log($"{_currentTarget} !!!");
        return _currentTarget;
    }

    private LockOnTarget FindBestTarget()
    {
        if (!_viewCamera) return null;

        int candidateCount = Physics.OverlapSphereNonAlloc(transform.position, _searchRange, _candidateBuffer, _targetLayerMask,
            QueryTriggerInteraction.Collide);
        LockOnTarget bestTarget = null;
        float bestScreenDistance = float.MaxValue;

        for (int i = 0; i < candidateCount; i++)
        {
            LockOnTarget candidate = _candidateBuffer[i].GetComponentInParent<LockOnTarget>();

            if (!candidate) continue;

            Vector3 viewportPosition = _viewCamera.WorldToViewportPoint(candidate.AimPosition);

            if (!IsInsideViewport(viewportPosition)) continue;

            float horizontalOffset = viewportPosition.x - 0.5f;
            float verticalOffset = viewportPosition.y - 0.5f; //중앙
            float screenDistance = horizontalOffset * horizontalOffset + verticalOffset * verticalOffset;

            if (screenDistance >= bestScreenDistance) continue;

            bestScreenDistance = screenDistance;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool IsInsideViewport(Vector3 viewportPosition)
    {
        return viewportPosition.z > 0f
            && viewportPosition.x >= _viewportPadding
            && viewportPosition.x <= 1f - _viewportPadding
            && viewportPosition.y >= _viewportPadding
            && viewportPosition.y <= 1f - _viewportPadding;
    }
}
