using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Project.Core
{
    public class PlayerSlotUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image goblinIcon;
        [SerializeField] private TMP_Text statusText;

        [SerializeField] private Color emptyColor = Color.gray;
        [SerializeField] private Color readyColor = Color.green;

        public bool IsOccupied { get; private set; }

        public void SetEmpty()
        {
            IsOccupied = false;
            background.color = emptyColor;
            goblinIcon.color = new Color(1, 1, 1, 0.3f);
            statusText.text = "Waiting...\nPress any button to join";
        }

        public void SetReady()
        {
            IsOccupied = true;
            background.color = readyColor;
            goblinIcon.color = Color.white;
            statusText.text = "Ready!";
        }
    }
}