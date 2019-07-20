using System;
using System.Collections.Generic;
using DragonBones;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Animation = DragonBones.Animation;

namespace LC_Tools
{
    public class SelectorRecordItem : UIBehaviour
    {
        [SerializeField] public List<Button> buttons;
        public int index;

        public enum IconState
        {
            None,
            Freeze,
            Action,
            Expect,
        }

        private struct DLElement
        {
            public GameObject sliderGo;
            public Image slider;
            public Text value;
            public Image nameBG;
            public Image nameLbl;
            public GameObject downGo;
            public GameObject goldGo;
        }

        private readonly Dictionary<int, Dictionary<IconState, string>> _boneDict = new Dictionary<int, Dictionary<IconState, string>>();
        private readonly Dictionary<int, DLElement> _dlDict = new Dictionary<int, DLElement>();
        private readonly Animation[] _animation = new Animation[3];

        private readonly Color[] colors =
        {
            new Color32(255, 255, 255, 123),
            new Color32(255, 255, 255, 255),
        };

        public void InjectData(int index, GameObject spine, string title)
        {
            var titleImg = buttons[index].transform.Find("NameBar/Name").GetComponent<Image>();
            titleImg.sprite = LC_PoolManager.Instance.GetSpriteFromAtlas("GameHall", "Title", title);
            var panel = buttons[index].transform.Find("Pedal");
            spine.transform.SetParent(panel, false);

            var uac = spine.GetComponent<UnityArmatureComponent>();
            _animation[index] = uac.animation;
            var actions = _animation[index].animationNames;

            var boneSet = new Dictionary<IconState, string> {{IconState.None, ""}};
            foreach (var action in actions)
            {
                var tmp = action.Split('_');
                if (tmp.Length < 2) continue;
                switch (tmp[1])
                {
                    case "start":
                        boneSet.Add(IconState.Action, action);
                        break;
                    case "dark":
                        boneSet.Add(IconState.Freeze, action);
                        break;
                    default:
                        boneSet.Add(IconState.Expect, action);
                        break;
                }

//                Debug.Log($"Action: {action}");
            }

            if (_boneDict.ContainsKey(index))
            {
                Debug.LogError($"!!! SelectorRecordItem Name:{gameObject.name} Index:{index} has existed !!!");
                _boneDict[index] = boneSet;
            }
            else
            {
                _boneDict.Add(index, boneSet);
            }
        }

        public void SetIconState(int index, float progress = 0)
        {
            if (!_dlDict.ContainsKey(index)) return;
            SetBoneState(index, IconState.Freeze);
            var target = _dlDict[index];
            var fixText = (float) Math.Round(progress, 2);
            target.slider.fillAmount = fixText;
            var hundred = (float) Math.Round(progress * 100, 2);
            target.value.text = $"{hundred}%";
            if (hundred < 100f) return;
            SetBoneState(index, IconState.Action);
        }

        public void SetBoneState(int index, IconState state)
        {
            SetDLIcon(index, state);
            if (!_boneDict.ContainsKey(index)) return;
            var bone = _boneDict[index];
            if (!bone.ContainsKey(state)) return;
            var target = bone[state];
            if (_animation[index].lastAnimationName.Equals(target)) return;
            _animation[index]?.Play(target);
            if (state != IconState.Action)
            {
                _animation[index].Stop();
            }
        }

        private void SetDLIcon(int index, IconState state)
        {
            if (!_dlDict.ContainsKey(index)) return;
            var target = _dlDict[index];
            switch (state)
            {
                case IconState.None:
                    target.nameBG.color = colors[1];
                    target.nameLbl.color = colors[1];
                    target.sliderGo.SetActive(false);
                    target.downGo.SetActive(true);
                    target.goldGo.SetActive(false);
                    break;
                case IconState.Freeze:
                    target.nameBG.color = colors[0];
                    target.nameLbl.color = colors[0];
                    target.sliderGo.SetActive(true);
                    target.downGo.SetActive(false);
                    target.goldGo.SetActive(false);
                    break;
                case IconState.Action:
                    target.nameBG.color = colors[1];
                    target.nameLbl.color = colors[1];
                    target.sliderGo.SetActive(false);
                    target.downGo.SetActive(false);
                    target.goldGo.SetActive(true);
                    break;
            }
        }

        private void Awake()
        {
            try
            {
                index = int.Parse(gameObject.name);
            }
            catch (FormatException)
            {
            }

            for (var i = 1; i < buttons.Count; i++)
            {
                var button = buttons[i];
                var slider = button.transform.Find("Slider").gameObject;
                var combo = new DLElement
                {
                    sliderGo = slider,
                    slider = slider.transform.Find("Circle").GetComponent<Image>(),
                    value = slider.transform.Find("Num").GetComponent<Text>(),
                    nameBG = button.transform.Find("NameBar/NameBG").GetComponent<Image>(),
                    nameLbl = button.transform.Find("NameBar/Name").GetComponent<Image>(),
                    downGo = button.transform.Find("NameBar/Icon/Down").gameObject,
                    goldGo = button.transform.Find("NameBar/Icon/Gold").gameObject
                };
                _dlDict.Add(i, combo);
            }
        }

        private void OnDestroy()
        {
            _dlDict.Clear();
            _boneDict.Clear();
        }
    }
}