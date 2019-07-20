using System;
using UniRx;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    public class HeadRecordItem : UIBehaviour
    {
        public static Action<Action<int>> UpdateItemAction;
        public static Action<int> ClickAction;

        private const string ImgPath = @"HeadImg_";

        private int nameIndex;
        private int _iconIndex;

        private Toggle _toggle;
        private Image _headImg;

        public void UpdateItem(int index)
        {
            _iconIndex = -1;
            _iconIndex = index * 2 + nameIndex;
            InjectData(_iconIndex);
            UpdateItemAction?.Invoke(SetIsOn);
        }

        public void SetIsOn(int index)
        {
            _toggle.isOn = _iconIndex == index;
//            Debug.Log($" !!! Now:{_iconIndex} Selected: {index} isNo:{_toggle.isOn}");
        }

        public void InjectData(int index)
        {
            _headImg.sprite = LC_PoolManager.Instance.GetSpriteFromAtlas("Common", "HeadImg", ImgPath + index);
        }

        private void Awake()
        {
            try
            {
                nameIndex = int.Parse(name);
            }
            catch (FormatException)
            {
            }

            _toggle = transform.Find("Toggle").GetComponent<Toggle>();
            _toggle.OnValueChangedAsObservable().Pairwise().Where(x => !x.Previous && x.Current).TakeUntilDestroy(this).Subscribe(x => { ClickAction?.Invoke(_iconIndex); });
            _headImg = transform.Find("Toggle/Icon/Head").GetComponent<Image>();
        }
    }
}