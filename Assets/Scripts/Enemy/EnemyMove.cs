using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class EnemyMove : MonoBehaviour
{
    [Header("이동")]
    [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float _rotationSpeed = 540f;

    [Header("NavMesh 경로")]
    [SerializeField, Min(0.01f)] private float _pathRefreshInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float _cornerReachDistance = 0.1f;
    [SerializeField, Min(0.1f)] private float _circleLookAheadDistance = 2f;

    [Header("중력")]
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _groundedVerticalVelocity = -2f;

    private CharacterController _characterController;
    private float _verticalVelocity;
    private Room _navigationRoom;
    private bool _usesNavigation;
    private NavMeshPath _navigationPath;
    // 적마다 한 번 생성하며 경로 조회 시 corners 프로퍼티의 배열 할당을 피한다.
    private readonly Vector3[] _pathCorners = new Vector3[32];
    private int _pathCornerCount;
    private int _pathCornerIndex;
    private float _pathRefreshRemaining;
    private bool _hasPathRequest;
    private bool _isCirclePath;

    public float MoveAmount { get; private set; }

    private void Awake()
    {
        TryGetComponent(out _characterController);
        _navigationPath = new NavMeshPath();
    }

    public void ResetForSpawn()
    {
        // 이전 위치에서 누적된 낙하 속도를 제거한다.
        _verticalVelocity = 0f;
        MoveAmount = 0f;
        SetNavigationRoom(null);
    }

    public void SetNavigationRoom(Room room)
    {
        // Room의 소유 정보는 EnemyManager가 전달하며 씬 전체에서 방을 검색하지 않는다.
        _navigationRoom = room;
        _usesNavigation = room && room.UsesNavigation;
        ClearNavigationPath();
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        // 하위 컴포넌트끼리 참조하지 않고 Manager가 스탯을 전달한다.
        _moveSpeed = Mathf.Max(0f, moveSpeed);
    }

    public void MoveTowards(Vector3 targetPosition, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        Vector3 moveDirection = targetPosition - transform.position;
        moveDirection.y = 0f;
        float remainingDistance = float.PositiveInfinity;

        if (_usesNavigation && !TryGetNavigationDirection(targetPosition, false, deltaTime, out moveDirection, out remainingDistance))
        {
            // 경로 실패 중에는 재조회 간격을 유지하고 직진으로 장애물을 밀지 않는다.
            MoveHorizontal(Vector3.zero, 0f, deltaTime, 0f);
            return;
        }

        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            MoveStationary(deltaTime);
            return;
        }

        moveDirection.Normalize();
        RotateTowards(moveDirection, deltaTime);
        MoveHorizontal(moveDirection, _moveSpeed, deltaTime, remainingDistance);
    }

    public void MoveAttack(Vector2 localDisplacement, float deltaTime)
    {
        ClearNavigationPath();
        Vector3 displacement = transform.right * localDisplacement.x + transform.forward * localDisplacement.y;
        ApplyGravity(deltaTime);
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);
        MoveAmount = 0f;
    }

    public void MovePoiseReaction(Vector3 worldDisplacement, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        ClearNavigationPath();

        // 피격 이동을 걷기 애니메이션의 이동 입력으로 사용하지 않는다.
        MoveAmount = 0f;

        // 수평 이동은 피격 설정을 사용하고 수직 이동은 기존 중력을 유지한다.
        ApplyGravity(deltaTime);
        worldDisplacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(worldDisplacement);
    }

    public void MoveStationary(float deltaTime)
    {
        ClearNavigationPath();
        ApplyGravity(deltaTime);
        _characterController.Move(Vector3.up * (_verticalVelocity * deltaTime));
        MoveAmount = 0f;
    }

    public Vector2 MoveCircleTarget(Vector3 targetPosition, float speed, float desiredDistance, float distanceTolerance,
        float directionSign, float deltaTime)
    {
        if (deltaTime <= 0f) return Vector2.zero;

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            MoveStationary(deltaTime);
            return Vector2.zero;
        }

        float distance = toTarget.magnitude;
        Vector3 radialDirection = toTarget / distance;
        Vector3 tangentDirection = Vector3.Cross(Vector3.up, radialDirection) * directionSign;

        // 목표 거리 근처에서는 앞뒤 보정을 생략하여 이동 방향의 떨림을 막는다.
        float distanceError = distance - Mathf.Max(0f, desiredDistance);
        float tolerance = Mathf.Max(0f, distanceTolerance);
        float radialAmount = 0f;
        if (Mathf.Abs(distanceError) > tolerance)
        {
            radialAmount = Mathf.Sign(distanceError) * Mathf.Clamp01(Mathf.Abs(distanceError) - tolerance);
        }

        Vector3 moveDirection = Vector3.ClampMagnitude(tangentDirection + radialDirection * radialAmount, 1f);
        FaceTarget(targetPosition, deltaTime);
        float remainingDistance = float.PositiveInfinity;
        if (_usesNavigation)
        {
            // Circle의 진행 방향에 짧은 목표점을 두고 그 위치까지 우회한다.
            Vector3 navigationTarget = GetFootPosition() + moveDirection * Mathf.Max(0.1f, _circleLookAheadDistance);
            if (!TryGetNavigationDirection(navigationTarget, true, deltaTime, out moveDirection, out remainingDistance))
            {
                return MoveHorizontal(Vector3.zero, 0f, deltaTime, 0f);
            }
        }

        return MoveHorizontal(moveDirection, speed, deltaTime, remainingDistance);
    }

    private Vector2 MoveHorizontal(Vector3 direction, float speed, float deltaTime, float remainingDistance)
    {
        ApplyGravity(deltaTime);

        Vector3 previousPosition = transform.position;
        float moveDistance = Mathf.Min(Mathf.Max(0f, speed) * deltaTime, remainingDistance);
        Vector3 displacement = direction * moveDistance;
        if (_usesNavigation && displacement.sqrMagnitude > 0.000001f)
        {
            displacement = ConstrainNavigationDisplacement(displacement);
        }

        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);

        // 충돌로 막힌 경우를 포함한 실제 이동량을 애니메이션과 막힘 판정에 전달한다.
        Vector3 actualVelocity = (transform.position - previousPosition) / deltaTime;
        actualVelocity.y = 0f;
        Vector3 localVelocity = transform.InverseTransformDirection(actualVelocity);
        float referenceSpeed = Mathf.Max(0.01f, _moveSpeed);
        Vector2 localMovement = new Vector2(localVelocity.x, localVelocity.z) / referenceSpeed;
        MoveAmount = Mathf.Clamp01(localMovement.magnitude);
        return Vector2.ClampMagnitude(localMovement, 1f);
    }

    private bool TryGetNavigationDirection(Vector3 targetPosition, bool isCircle, float deltaTime,
        out Vector3 direction, out float remainingDistance)
    {
        direction = Vector3.zero;
        remainingDistance = 0f;
        _pathRefreshRemaining -= deltaTime;
        if (!_navigationRoom || !_navigationRoom.TryGetNavigationFilter(out NavMeshQueryFilter filter)) return false;

        Vector3 footPosition = GetFootPosition(); //그냥 EnemyManager의 Position으로 단순화 할 수 있음.
        if (!_navigationRoom.TrySampleNavigationPosition(footPosition, out Vector3 startPosition)) return false;

        if (!_hasPathRequest || _isCirclePath != isCircle || _pathRefreshRemaining <= 0f)
        {
            _hasPathRequest = true;
            _isCirclePath = isCircle;
            _pathRefreshRemaining = _pathRefreshInterval;
            _pathCornerCount = 0;
            _pathCornerIndex = 1;

            bool hasTarget = _navigationRoom.TrySampleNavigationPosition(targetPosition, out Vector3 endPosition);
            if (!hasTarget && isCircle)
            {
                // 작은 장애물 너머와 가까운 목표점을 차례로 시도하고 벽을 향한 직진은 피한다.
                Vector3 offset = targetPosition - footPosition;
                hasTarget = _navigationRoom.TrySampleNavigationPosition(footPosition + offset * 2f, out endPosition);
                if (!hasTarget) hasTarget = _navigationRoom.TrySampleNavigationPosition(footPosition + offset * 0.5f, out endPosition);
            }

            if (!hasTarget || !NavMesh.CalculatePath(startPosition, endPosition, filter, _navigationPath)
                || _navigationPath.status != NavMeshPathStatus.PathComplete) return false;

            int cornerCount = _navigationPath.GetCornersNonAlloc(_pathCorners);
            // 버퍼에 경로가 잘린 경우나 다른 방을 통과하는 경로는 사용하지 않는다.
            if (cornerCount < 2 || cornerCount > _pathCorners.Length
                || (_pathCorners[cornerCount - 1] - endPosition).sqrMagnitude > 0.01f) return false;
            for (int index = 0; index < cornerCount; index++)
            {
                if (!_navigationRoom.ContainsNavigationPosition(_pathCorners[index])) return false;
            }

            _pathCornerCount = cornerCount;
        }

        if (_pathCornerCount < 2) return false;

        float reachDistance = Mathf.Max(0.01f, _cornerReachDistance);
        while (_pathCornerIndex < _pathCornerCount - 1)
        {
            Vector3 cornerOffset = _pathCorners[_pathCornerIndex] - footPosition;
            cornerOffset.y = 0f;
            if (cornerOffset.sqrMagnitude > reachDistance * reachDistance) break;
            // 모서리에 아직 도달하지 않았을 때 다음 선분으로 갈아타며 기둥을 가로지르지 않는다.
            Vector3 followingCorner = _pathCorners[_pathCornerIndex + 1];
            if (NavMesh.Raycast(startPosition, followingCorner, out NavMeshHit cornerHit, filter)
                && (cornerHit.position - followingCorner).sqrMagnitude > 0.0025f) break;
            _pathCornerIndex++;
        }

        Vector3 nextCorner = _pathCorners[_pathCornerIndex];
        if (NavMesh.Raycast(startPosition, nextCorner, out NavMeshHit hit, filter) && (hit.position - nextCorner).sqrMagnitude > 0.0025f)
        {
            // 새 Carving이나 강제 이동으로 현재 경유점이 막히면 다음 프레임에 다시 구한다.
            ClearNavigationPath();
            return false;
        }

        direction = nextCorner - footPosition;
        direction.y = 0f;
        remainingDistance = direction.magnitude;
        if (remainingDistance <= 0.0001f) return false;

        direction /= remainingDistance;
        return true;
    }

    private Vector3 ConstrainNavigationDisplacement(Vector3 displacement)
    {
        if (!_navigationRoom || !_navigationRoom.TryGetNavigationFilter(out NavMeshQueryFilter filter)) return Vector3.zero;

        Vector3 footPosition = GetFootPosition();
        if (!_navigationRoom.TrySampleNavigationPosition(footPosition, out Vector3 startPosition)) return Vector3.zero;
        if (!_navigationRoom.TrySampleNavigationPosition(footPosition + displacement, out Vector3 endPosition)) return Vector3.zero;

        Vector3 constrained = endPosition - footPosition;
        constrained.y = 0f;
        if (NavMesh.Raycast(startPosition, endPosition, out NavMeshHit hit, filter))
        {
            constrained = hit.position - footPosition;
            constrained.y = 0f;
            float distance = constrained.magnitude;
            constrained = distance > 0.01f ? constrained * ((distance - 0.01f) / distance) : Vector3.zero;
        }

        // 보정 위치까지 순간 이동하지 않고 이번 프레임에 요청한 이동 거리만 허용한다.
        return Vector3.ClampMagnitude(constrained, displacement.magnitude);
    }

    private Vector3 GetFootPosition()
    {
        Vector3 scale = transform.lossyScale;
        float radius = _characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(_characterController.height * Mathf.Abs(scale.y), radius * 2f);
        return transform.TransformPoint(_characterController.center) - Vector3.up * (height * 0.5f);
    }

    private void ClearNavigationPath()
    {
        _pathCornerCount = 0;
        _pathCornerIndex = 0;
        _pathRefreshRemaining = 0f;
        _hasPathRequest = false;
    }

    public void FaceTarget(Vector3 targetPosition, float deltaTime)
    {
        Vector3 targetDirection = targetPosition - transform.position;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            RotateTowards(targetDirection.normalized, deltaTime);
        }
    }

    private void RotateTowards(Vector3 direction, float deltaTime)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * deltaTime);
    }

    private void ApplyGravity(float deltaTime)
    {
        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = _groundedVerticalVelocity;
            return;
        }

        _verticalVelocity += _gravity * deltaTime;
    }
}
