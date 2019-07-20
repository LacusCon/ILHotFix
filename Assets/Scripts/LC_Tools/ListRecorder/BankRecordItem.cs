using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    public class BankRecordItem : UIBehaviour
    {
        [SerializeField] Text _timeText;
        [SerializeField] Text _receiverText;
        [SerializeField] Text _goldText;

        [SerializeField] Image uiBackground;

        private readonly Color[] colors =
        {
            new Color32(44, 38, 38, 255),
            new Color32(27, 22, 20, 255),
        };

        public static Action<int, Action<string, string, string>> UpdateItemAction;

        public void UpdateItem(int count)
        {
            uiBackground.color = colors[Mathf.Abs(count) % colors.Length];
            UpdateItemAction?.Invoke(count, InjectData);
        }

        public void InjectData(string time, string receiver, string gold)
        {
            _timeText.text = time;
            _receiverText.text = receiver;
            _goldText.text = gold;
        }
    }
}