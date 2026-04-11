using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public Transform centerEyeAnchor; // OVRCameraRig 하위의 CenterEyeAnchor 연결
    public Transform bodyCapsule;     // 캡슐 오브젝트 연결

    public override void FixedUpdateNetwork()
    {
        // 내 캐릭터(Local Player)일 때만 내 위치를 몸에 전달
        if (Object.HasInputAuthority)
        {
            // 캡슐의 위치를 카메라의 X, Z 좌표와 맞춤 (Y는 바닥에 고정하거나 조절)
            Vector3 newPos = centerEyeAnchor.position;
            // newPos.y = transform.position.y; // 캡슐이 위아래로 출렁이지 않게 하려면 주석 해제
            bodyCapsule.position = newPos;

            // 캡슐의 회전도 카메라의 시선 방향과 맞춤 (Y축 회전만)
            Vector3 newRot = centerEyeAnchor.eulerAngles;
            newRot.x = 0;
            newRot.z = 0;
            bodyCapsule.eulerAngles = newRot;
        }
    }
}