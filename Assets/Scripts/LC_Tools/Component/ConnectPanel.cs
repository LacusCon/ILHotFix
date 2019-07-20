using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LC_Tools
{
    public class ConnectPanel : MonoBehaviour
    {
        private static ConnectPanel _connectPanel;
        private static GameObject _panelGo;

        private static GameObject _reconnectGo;
        private static GameObject _loadingGo;
        private static GameObject _tipsGo;

        public Action successAction;
        public Action cancelAction;
        private static readonly Dictionary<CircleEnum, bool> _circles = new Dictionary<CircleEnum, bool>();

        public enum CircleEnum
        {
            NONE,
            WAITING,
            CONNECTING
        }

        public static ConnectPanel Instance
        {
            get
            {
                if (_connectPanel != null) return _connectPanel;
                var cpGo = new GameObject("ConnectPanel");
                _connectPanel = cpGo.AddComponent<ConnectPanel>();
                _connectPanel.transform.SetParent(LC_SceneManager.GetCanvasTransform());
                return _connectPanel;
            }
        }

        public void Init()
        {
            if (_panelGo != null) return;
            var prefab = Resources.Load<GameObject>("BaseConnectPanel");
            _panelGo = Instantiate(prefab, LC_SceneManager.GetCanvasTransform(), false);
            _panelGo.transform.localPosition = new Vector3(0, 0, 100);
            _panelGo.gameObject.SetActive(true);
            RegisterButton();
        }

        public void SetReconnectPanel(bool isShow = true)
        {
            _reconnectGo.SetActive(isShow);
        }

        public void SetLoadingCircle(bool isShow = true, CircleEnum circle = CircleEnum.NONE)
        {
            if (_loadingGo == null) return;
            if (circle != CircleEnum.NONE)
            {
                if (isShow)
                {
                    if (!_circles.ContainsKey(circle))
                    {
                        _circles.Add(circle, isShow);
                    }

                    _loadingGo.SetActive(true);
                }
                else
                {
                    if (_circles.ContainsKey(circle))
                    {
                        _circles.Remove(circle);
                    }

                    if (_circles.Count == 0)
                    {
                        _loadingGo.SetActive(false);
                    }
                }
            }
            else
            {
                _loadingGo.SetActive(isShow);
                _circles.Clear();
            }
        }

        public void SetTipsPanel(bool open, string content, UnityAction action)
        {
            if (open == _tipsGo.activeSelf) return;
            _tipsGo.SetActive(open);
            if (!open) return;

            var tipsCnt = _tipsGo.transform.Find("TipsText").GetComponent<Text>();
            var tipsBnt = _tipsGo.transform.Find("SubmitBtn").GetComponent<Button>();

            tipsCnt.text = content;

            tipsBnt.onClick.AddListener(() =>
            {
                action?.Invoke();
                tipsBnt.onClick.RemoveAllListeners();
                _tipsGo.SetActive(false);
            });
        }

        private void RegisterButton()
        {
            _reconnectGo = _panelGo.transform.Find("BreakLinePanel").gameObject;
            _loadingGo = _panelGo.transform.Find("Loading").gameObject;
            _tipsGo = _panelGo.transform.Find("TipsPanel").gameObject;

            var processBtn = _reconnectGo.transform.Find("SubmitBtn").GetComponent<Button>();
            var cancelBtn = _reconnectGo.transform.Find("CancelBtn").GetComponent<Button>();

            _reconnectGo.SetActive(false);
            _loadingGo.SetActive(false);

            processBtn.onClick.AddListener(() =>
            {
                successAction?.Invoke();
                _reconnectGo.SetActive(false);
            });
            cancelBtn.onClick.AddListener(() =>
            {
                cancelAction?.Invoke();
//                Application.Quit();
            });
        }
    }
}