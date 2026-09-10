using UnityEngine;

namespace Assets.MyAssets.Scripts.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMove : MonoBehaviour
    {
        private CharacterController _cc;
        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        public void Move(Vector3 moveDir, float moveSpeed)
        {
            moveDir.Normalize();
            RotateToward(moveDir);

            _cc.Move(moveDir * moveSpeed * Time.deltaTime);
        }

        private void RotateToward(Vector3 dir)
        {
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}

