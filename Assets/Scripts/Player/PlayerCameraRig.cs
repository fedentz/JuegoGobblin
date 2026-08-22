using UnityEngine;

namespace Project.Player
{
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        private void OnEnable() => SplitScreenManager.Register(cam);
        private void OnDisable() => SplitScreenManager.Unregister(cam);
    }
}