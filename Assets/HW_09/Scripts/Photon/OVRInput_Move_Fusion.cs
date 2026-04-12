using UnityEngine;
using Fusion;

public class OVRInput_Move_Fusion : NetworkBehaviour
{
    [Header("References")]
    public Transform CenterEye; // 인스펙터에서 CenterEyeAnchor를 꼭 연결하세요

    [Header("Move Settings")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 3.5f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 0.5f;
    public float gravity = -9.81f;

    private Vector3 _velocity;
    private CharacterController controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // 1. 트래킹 모드를 강제로 '바닥 기준'으로 고정 (가장 중요)
            // 이게 되어야 헤드셋 높이만큼 카메라가 자동으로 올라갑니다.
            OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            // 2. 문제의 자식 오브젝트들 위치 초기화
            Transform parts = transform.Find("Player_OVRInput_Parts");
            if (parts != null) parts.localPosition = Vector3.zero;

            // 3. 내 카메라/리스너 활성화
            var cams = GetComponentsInChildren<Camera>(true);
            foreach (var c in cams) c.enabled = true;

            var listeners = GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners) l.enabled = true;
        }
        else
        {
            // 남의 캐릭터는 트래킹 릭은 놔두고, 카메라/리스너/이동만 끔
            var cams = GetComponentsInChildren<Camera>(true);
            foreach (var c in cams) c.enabled = false;

            var listeners = GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners) l.enabled = false;

            this.enabled = false;
        }
    }

    void Update()
    {
        // 내 캐릭터가 아니면 입력 처리 안 함
        if (!Object.HasInputAuthority) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        // VR 컨트롤러 입력 (L스틱)
        Vector2 input = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        bool isRunning = OVRInput.Get(OVRInput.RawButton.LThumbstick);

        // 이동 방향 계산 (CenterEye 기준)
        Vector3 forward = CenterEye.forward;
        Vector3 right = CenterEye.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * input.y) + (right * input.x);
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // X, Z 축 이동 적용
        Vector3 moveAmount = moveDir * currentSpeed * Time.deltaTime;
        // 중력 적용
        if (controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
            
            Vector3 pos = transform.position;
            pos.y = 0;
            transform.position = pos;
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }

        moveAmount.y = _velocity.y;
        
        controller.Move(moveAmount * Time.deltaTime);
    }

}