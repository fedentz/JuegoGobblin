using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Project.Core
{
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager inputManager;
        [SerializeField] private PlayerSlotUI[] slots;

        private int nextSlotIndex;

        private void Awake()
        {
            foreach (var slot in slots) slot.SetEmpty();
        }

        private void OnEnable() => inputManager.onPlayerJoined += OnPlayerJoined;
        private void OnDisable() => inputManager.onPlayerJoined -= OnPlayerJoined;

        private void OnPlayerJoined(PlayerInput player)
        {
            Debug.Log($"OnPlayerJoined llamado. nextSlotIndex={nextSlotIndex}, slots.Length={slots.Length}");
            DontDestroyOnLoad(player.gameObject);

            if (nextSlotIndex < slots.Length)
            {
                Debug.Log($"Llamando SetReady en slot {nextSlotIndex}");
                slots[nextSlotIndex].SetReady();
                nextSlotIndex++;
            }
        }

        public void StartGame()
        {
            if (nextSlotIndex < 1) return;
            SceneManager.LoadScene("SampleScene");
        }
    }
}