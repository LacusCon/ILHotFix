using System;
using System.Collections.Generic;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace LC_Tools
{
    public class LC_UIManager : Singleton<LC_UIManager>
    {
        public static readonly List<UILayerInfo> UILayerList = new List<UILayerInfo>();

        private readonly Color _greyColor = new Color32(150, 129, 101, 255);
        private readonly Color _brownColor = new Color32(97, 34, 21, 255);

        private static readonly Dictionary<int, UIInfo> UiDefaultPosDict = new Dictionary<int, UIInfo>();
        private GameObject _coverGo;
        private const float ActionDuration = 0.5f;

        private class UIInfo
        {
            public Vector3 defaultPos;
            public int renderQueue;
        }

        public class UILayerInfo
        {
            public bool haveAlpha;
            public Transform pedalTf;
            public bool ignoreOnce;
            public readonly Dictionary<GameObject, UIAction> elements = new Dictionary<GameObject, UIAction>();
        }

        public enum UIAction
        {
            None,
            LeftSlip,
            RightSlip,
            TopSlip,
            BottomSlip,
            Pop
        }

        public static Action<int> AlterLayer;

//        public void OpenWithDefault(List<GameObject> uiList, UIAction action = UIAction.RightSlip)
//        {
//            var layerInfo = new UILayerInfo {haveAlpha = true};
//            foreach (var ui in uiList)
//            {
//                layerInfo.elements.Add(ui, action);
//            }
//
//            OpenUILayer(layerInfo);
//        }

        public void OpenUILayer(UILayerInfo info, bool show = true)
        {
            foreach (var item in info.elements)
            {
                var obj = item.Key;
                obj.SetActive(show);
                var key = obj.GetInstanceID();
                if (UiDefaultPosDict.ContainsKey(key)) continue;
                var cur = new UIInfo();
                var renderer = obj.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    cur.renderQueue = renderer.material.renderQueue;
                }

                cur.defaultPos = obj.transform.localPosition;
                UiDefaultPosDict.Add(key, cur);
            }

            if (!info.haveAlpha)
            {
                SetTopUI(false);
            }
            else if (info.pedalTf)
            {
                SetCover(true, info.pedalTf);
            }

            if (show) Performance(info, true);
            UILayerList.Add(info);
            AlterLayer?.Invoke(UILayerList.Count);
        }

        public void ShowTopUI()
        {
            SetTopUI(true, false, true);
        }

        public void CloseTopUI()
        {
            if (SetTopUI(false, true))
            {
                SetTopUI(true);
            }

            SetCover(false);
            AlterLayer?.Invoke(UILayerList.Count);
        }

        public static void DestroyUIQueue()
        {
            UILayerList.Clear();
            UiDefaultPosDict.Clear();
            AlterLayer?.Invoke(0);
        }

        public static void ButtonZoom(GameObject btn, Action callback = null)
        {
            btn.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.1f, 1).OnComplete(() => { callback?.Invoke(); });
        }

        public void PanelPopup(GameObject panel, Action callback = null)
        {
            panel.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.3f, 1).OnComplete(() => { callback?.Invoke(); });
        }

        public void BtnPopup(Transform btn)
        {
            btn.DOPunchScale(Vector3.one * 0.1f, 0.1f);
        }

        public void SetSelectionColorChange(Transform selectionBase)
        {
            var allToggle = selectionBase.GetComponentsInChildren<Toggle>();
            foreach (var toggle in allToggle)
            {
                var txt = toggle.transform.Find("Label").GetComponent<Text>();
                toggle.OnValueChangedAsObservable().TakeUntilDestroy(toggle.gameObject).Subscribe(_ => { txt.color = toggle.isOn ? _brownColor : _greyColor; });
            }
        }

        public void LinkSelection2Panel(Transform selectionBase, Transform panelBase)
        {
            var allToggle = selectionBase.GetComponentsInChildren<Toggle>();
            for (var i = 0; i < panelBase.childCount; i++)
            {
                var panel = panelBase.GetChild(i);
                var fixName = panel.name + "Toggle";
                foreach (var toggle in allToggle)
                {
                    if (panel.name.Equals(toggle.name) || fixName.Equals(toggle.name))
                    {
                        toggle.OnValueChangedAsObservable().TakeUntilDestroy(toggle.gameObject).Subscribe(_ => { panel.gameObject.SetActive(toggle.isOn); });
                    }
                }
            }
        }

        public void InitClickSound(Transform tf)
        {
            var btnGroup = tf.GetComponentsInChildren<Button>(true);
            var toggleGroup = tf.GetComponentsInChildren<Toggle>(true);

            foreach (var btn in btnGroup)
            {
                var scale = btn.transform.localScale;
                btn.OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromMilliseconds(DoubleClick.MILLISECONDS))
                    .TakeUntilDestroy(tf.gameObject)
                    .Subscribe(_ =>
                    {
                        btn.transform.localScale = scale;
                        LC_SoundManager.Instance.PlaySound("GameHall", GetSoundName(btn.name));
                        ButtonZoom(btn.gameObject);
                    });
            }

            foreach (var toggle in toggleGroup)
            {
                toggle.OnValueChangedAsObservable()
                    .Pairwise().Where(x => !x.Previous && x.Current)
                    .ThrottleFirst(TimeSpan.FromMilliseconds(DoubleClick.MILLISECONDS))
                    .TakeUntilDestroy(tf.gameObject)
                    .Subscribe(_ => { LC_SoundManager.Instance.PlaySound("GameHall", GetSoundName(toggle.name)); });
            }
        }

        private static void Performance(UILayerInfo info, bool isPositive)
        {
            foreach (var element in info.elements)
            {
                var obj = element.Key;
                obj.SetActive(true);
                var tf = obj.transform;
                var rectTransform = tf.GetComponent<RectTransform>();
                if (rectTransform == null) continue;

                var relativePos = Vector3.zero;
                var defaultPos = Vector3.zero;
                if (UiDefaultPosDict.ContainsKey(obj.GetInstanceID()))
                {
                    defaultPos = UiDefaultPosDict[obj.GetInstanceID()].defaultPos;
                }
                else
                {
                    Debug.LogError($"!!!!! Performance: PosDict not contained: {obj.name} !!!!!");
                    return;
                }

                var srcPos = defaultPos;
                var destPos = defaultPos;
                switch (element.Value)
                {
                    case UIAction.None:
                        obj.SetActive(isPositive);
                        continue;
                    case UIAction.LeftSlip:
                        relativePos = new Vector3(srcPos.x - rectTransform.rect.width, srcPos.y, srcPos.z);
                        break;
                    case UIAction.RightSlip:
                        relativePos = new Vector3(srcPos.x + rectTransform.rect.width, srcPos.y, srcPos.z);
                        break;
                    case UIAction.TopSlip:
                        relativePos = new Vector3(srcPos.x, srcPos.y + rectTransform.rect.height, srcPos.z);
                        break;
                    case UIAction.BottomSlip:
                        relativePos = new Vector3(srcPos.x, srcPos.y - rectTransform.rect.height, srcPos.z);
                        break;
                    case UIAction.Pop:
                        if (isPositive)
                        {
                            obj.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.3f, 1);
                        }
                        else
                        {
                            obj.SetActive(false);
                        }

                        continue;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var duration = ActionDuration;
                if (isPositive)
                {
                    tf.localPosition = relativePos;
                }
                else
                {
                    duration = ActionDuration - 0.2f;
                    destPos = relativePos;
                }

                obj.transform.DOLocalMove(destPos, duration).SetEase(Ease.OutBack).OnComplete(() => { obj.SetActive(isPositive); });
//                obj.transform.DOLocalJump(destPos, 0.3f, 1, duration).SetEase(Ease.OutBack).OnComplete(() => { obj.SetActive(isPositive); });
            }
        }

        private static void ChangeRenderQueue(Transform tf, bool active)
        {
            var count = UILayerList.Count;
            if (!active) count -= 1;
            var gain = (active ? count : -count) * 10;
            var material = tf.GetComponent<Material>();
            material.renderQueue = material.renderQueue + gain;
//            var count = tf.childCount;
//            if (count > 0)
//            {
//                for (int i = 0; i < count; i++)
//                {
//                    ChangeRenderQueue(tf.transform.GetChild(count), renderUP);
//                }
//            }
            var comps = tf.GetComponentsInChildren<Material>(true);
            foreach (var comp in comps)
            {
                comp.renderQueue = material.renderQueue + gain;
            }
        }

        private static string GetSoundName(string name)
        {
            if ("Shop".Equals(name)) return "KXM_shop";
            return name.Contains("Close") ? "KXM_TY_close" : "KXM_TY_icon";
        }

        private static bool SetTopUI(bool active, bool delete = false, bool enforce = false)
        {
            var count = UILayerList.Count;
            if (count <= 0) return false;
            var cur = UILayerList[count - 1];
            if (enforce) cur.ignoreOnce = false;
            if (cur.ignoreOnce)
            {
                cur.ignoreOnce = false;
                return false;
            }

            Performance(cur, active);
            if (delete) UILayerList.Remove(cur);
            return !cur.haveAlpha;
        }

        private void SetCover(bool active, Transform parent = null)
        {
            if (_coverGo == null)
            {
                Debug.Log($"####### Create Cover resWidth:[{Screen.currentResolution.width}] resHeight:[{Screen.currentResolution.height}] Width:[{Screen.width}] Height:[{Screen.height}] #########");
                _coverGo = new GameObject("UI_Cover");
                var img = _coverGo.AddComponent<Image>();
                img.color = new Color32(0, 0, 0, 180);
                var rectTransform = _coverGo.transform as RectTransform;
//                rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
                rectTransform.sizeDelta = new Vector2(4096, 4096);
            }

            if (parent != null)
            {
                _coverGo.transform.SetParent(parent, false);
            }

            _coverGo.transform.SetAsFirstSibling();
            _coverGo.SetActive(active);
        }

        private void Awake()
        {
            SetPersistent(gameObject);
        }

        private void Start()
        {
            LC_SceneManager.AddCallBacker((pre, cur) => { DestroyUIQueue(); });
        }
    }
}