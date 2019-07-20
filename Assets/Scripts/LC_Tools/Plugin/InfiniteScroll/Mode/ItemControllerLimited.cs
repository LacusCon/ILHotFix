using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    [RequireComponent(typeof(InfiniteScroll))]
    public class ItemControllerLimited : UIBehaviour, IInfiniteScrollSetup
    {
        [SerializeField, Range(0, 999)] public int max = 30;
        [SerializeField] public ScrollEventDispatcher.RecordType _recordType;

        private ScrollRect _rect;

        public void OnPostSetupItems()
        {
            var infiniteScroll = GetComponent<InfiniteScroll>();
            infiniteScroll.onUpdateItem.AddListener(OnUpdateItem);
            _rect = GetComponentInParent<ScrollRect>();
            _rect.movementType = ScrollRect.MovementType.Elastic;
            ResetItems();
        }

        public void ResetItems()
        {
            var infiniteScroll = GetComponent<InfiniteScroll>();
            var rectTransform = GetComponent<RectTransform>();
            var delta = rectTransform.sizeDelta;
            if (_rect == null || _rect.vertical)
            {
                delta.y = infiniteScroll.itemScale * max;
            }
            else
            {
                delta.x = infiniteScroll.itemScale * max;
                if (_recordType == ScrollEventDispatcher.RecordType.GameSelector)
                {
                    delta.x += 100;
                }
            }

            rectTransform.sizeDelta = delta;
        }

        public void OnUpdateItem(int itemCount, GameObject obj)
        {
            if (itemCount < 0 || itemCount >= max)
            {
                obj.SetActive(false);
            }
            else
            {
                obj.SetActive(true);
                var dict = ScrollEventDispatcher.targetDict;
                if (dict.ContainsKey(_recordType))
                {
                    dict[_recordType]?.Invoke(obj, itemCount);
                }
            }
        }
    }
}