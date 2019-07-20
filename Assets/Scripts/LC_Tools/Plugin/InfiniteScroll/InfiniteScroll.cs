using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

//fork from  https://github.com/tsubaki/Unity_UI_Samples
namespace LC_Tools
{
    public class InfiniteScroll : UIBehaviour
    {
        [SerializeField] private RectTransform itemPrototype;

        [SerializeField, Range(0, 30)] public int instantiateItemCount = 9;

        [SerializeField] private Direction direction;

        public OnItemPositionChange onUpdateItem = new OnItemPositionChange();
        public Action<GameObject> InitCallback;
        public bool _isCallBack;

        [System.NonSerialized] private readonly LinkedList<RectTransform> itemList = new LinkedList<RectTransform>();

        private float diffPreFramePosition;

        private int currentItemNo;

        public enum Direction
        {
            Vertical,
            Horizontal,
        }

        // cache component

        private RectTransform _rectTransform;

        protected RectTransform rectTransform
        {
            get
            {
                if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        private float anchoredPosition
        {
            get { return direction == Direction.Vertical ? -rectTransform.anchoredPosition.y : rectTransform.anchoredPosition.x; }
        }

        private float _itemScale = -1;

        public float itemScale
        {
            get
            {
                if (itemPrototype != null && _itemScale == -1)
                {
                    _itemScale = direction == Direction.Vertical ? itemPrototype.sizeDelta.y : itemPrototype.sizeDelta.x;
                }

                return _itemScale;
            }
        }

        protected override void Start()
        {
            var controllers = GetComponents<MonoBehaviour>().OfType<IInfiniteScrollSetup>().ToList();

            // create items
            var scrollRect = GetComponentInParent<ScrollRect>();
            scrollRect.horizontal = direction == Direction.Horizontal;
            scrollRect.vertical = direction == Direction.Vertical;
            scrollRect.content = rectTransform;

            itemPrototype.gameObject.SetActive(false);

            for (int i = 0; i < instantiateItemCount; i++)
            {
                var item = Instantiate(itemPrototype);
                item.SetParent(transform, false);
                item.name = i.ToString();
                item.anchoredPosition = direction == Direction.Vertical ? new Vector2(0, -itemScale * i) : new Vector2(itemScale * i, 0);
                itemList.AddLast(item);

                item.gameObject.SetActive(true);

                foreach (var controller in controllers)
                {
                    controller.OnUpdateItem(i, item.gameObject);
                }
            }

            foreach (var controller in controllers)
            {
                controller.OnPostSetupItems();
            }

            if (InitCallback == null) return;
            _isCallBack = true;
            InitCallback.Invoke(gameObject);
        }

        public void ResetControllers()
        {
            var controllers = GetComponents<MonoBehaviour>().OfType<IInfiniteScrollSetup>().ToList();
            foreach (var item in itemList)
            {
                var count = int.Parse(item.name);
                foreach (var controller in controllers)
                {
                    controller.OnUpdateItem(count, item.gameObject);
                }
            }

            foreach (var controller in controllers)
            {
                controller.ResetItems();
            }
        }

        void Update()
        {
            if (itemList.First == null)
            {
                return;
            }

            while (anchoredPosition - diffPreFramePosition < -itemScale * 2)
            {
                diffPreFramePosition -= itemScale;

                var item = itemList.First.Value;
                itemList.RemoveFirst();
                itemList.AddLast(item);

                var pos = itemScale * instantiateItemCount + itemScale * currentItemNo;
                item.anchoredPosition = (direction == Direction.Vertical) ? new Vector2(0, -pos) : new Vector2(pos, 0);

                onUpdateItem.Invoke(currentItemNo + instantiateItemCount, item.gameObject);
                currentItemNo++;
            }

            while (anchoredPosition - diffPreFramePosition > 0)
            {
                diffPreFramePosition += itemScale;

                var item = itemList.Last.Value;
                itemList.RemoveLast();
                itemList.AddFirst(item);

                currentItemNo--;

                var pos = itemScale * currentItemNo;
                item.anchoredPosition = (direction == Direction.Vertical) ? new Vector2(0, -pos) : new Vector2(pos, 0);
                onUpdateItem.Invoke(currentItemNo, item.gameObject);
            }

            if (InitCallback == null || _isCallBack) return;
            _isCallBack = true;
            InitCallback.Invoke(gameObject);
        }

        [System.Serializable]
        public class OnItemPositionChange : UnityEngine.Events.UnityEvent<int, GameObject>
        {
        }
    }
}