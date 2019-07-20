using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;

namespace LC_Tools
{
    public class LC_MessageManager : Singleton<LC_MessageManager>
    {
        private static readonly Dictionary<int, Action<BinaryMessage>> _registerDict =
            new Dictionary<int, Action<BinaryMessage>>();

        private static readonly Dictionary<int, Delegate> _eventDict = new Dictionary<int, Delegate>();

        private static readonly Queue<BinaryMessage> _netMessageQueue = new Queue<BinaryMessage>();

        public static void AddListener(int target, Action<BinaryMessage> action)
        {
            if (_registerDict.ContainsKey(target)) return;
            _registerDict.Add(target, action);
        }

        public static void RemoveListener(int target)
        {
            if (!_registerDict.ContainsKey(target)) return;
            _registerDict.Remove(target);
        }

        public static void AddEventListener(int target, Action action)
        {
            if (_eventDict.ContainsKey(target))
            {
                _eventDict[target] = (Action) _eventDict[target] + action;
            }
            else
            {
                _eventDict.Add(target, action);
            }
        }

        public static void AddEventListener<T>(int target, Action<T> action)
        {
            if (_eventDict.ContainsKey(target))
            {
                _eventDict[target] = (Action<T>) _eventDict[target] + action;
            }
            else
            {
                _eventDict.Add(target, action);
            }
        }

        public static void AddEventListener<T, U>(int target, Action<T, U> action)
        {
            if (_eventDict.ContainsKey(target))
            {
                _eventDict[target] = (Action<T, U>) _eventDict[target] + action;
            }
            else
            {
                _eventDict.Add(target, action);
            }
        }

        public static void AddEventListener<T, U, X>(int target, Action<T, U, X> action)
        {
            if (_eventDict.ContainsKey(target))
            {
                _eventDict[target] = (Action<T, U, X>) _eventDict[target] + action;
            }
            else
            {
                _eventDict.Add(target, action);
            }
        }

        public static void AddEventListener<T, U, X, W>(int target, Action<T, U, X, W> action)
        {
            if (_eventDict.ContainsKey(target))
            {
                _eventDict[target] = (Action<T, U, X, W>) _eventDict[target] + action;
            }
            else
            {
                _eventDict.Add(target, action);
            }
        }
//        public void AddSubListener(int target, Delegate @delegate)
//        {
//#if UNITY_EDITOR
//            var paras = @delegate.Method.GetParameters();
//            if (paras.Length > 4)
//            {
//                Debug.LogError($"== Target:[{target}]  Parameter is over 4 ==");
//            }
//#endif
//            if (_subRegisterDict.ContainsKey(target))
//                _subRegisterDict[target] = @delegate;
//            else
//                _subRegisterDict.Add(target, @delegate);
//        }

        public static void RemoveEventListener(int target)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict.Remove(target);
        }


        public static void RemoveEventListener(int target, Action action)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict[target] = (Action) _eventDict[target] - action;
            ClearIgnoreEvent(target);
        }

        public static void RemoveEventListener<T>(int target, Action<T> action)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict[target] = (Action<T>) _eventDict[target] - action;
            ClearIgnoreEvent(target);
        }

        public static void RemoveEventListener<T, U>(int target, Action<T, U> action)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict[target] = (Action<T, U>) _eventDict[target] - action;
            ClearIgnoreEvent(target);
        }

        public static void RemoveEventListener<T, U, W>(int target, Action<T, U, W> action)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict[target] = (Action<T, U, W>) _eventDict[target] - action;
            ClearIgnoreEvent(target);
        }

        public static void RemoveEventListener<T, U, W, X>(int target, Action<T, U, W, X> action)
        {
            if (!_eventDict.ContainsKey(target)) return;
            _eventDict[target] = (Action<T, U, W, X>) _eventDict[target] - action;
            ClearIgnoreEvent(target);
        }

        private static void ClearIgnoreEvent(int target)
        {
            if (_eventDict[target] == null)
            {
                _eventDict.Remove(target);
            }
        }

        public static void StockNetMessage(byte[] ctx)
        {
            var message = BinaryMessage.CreateBinary(ctx);
//            if (message.ProtocolId != 87)
//            {
//                Debug.LogWarning($"<-- Dispatcher <-- Protocol ID:[{message.ProtocolId}]");
//            }
            _netMessageQueue.Enqueue(message);
        }

        public static void EventDispatcher(int protocol)
        {
            if (!_eventDict.TryGetValue(protocol, out var d)) return;
            var action = d as Action;
            action?.Invoke();
        }

        public static void EventDispatcher<T>(int protocol, T para1)
        {
            if (!_eventDict.TryGetValue(protocol, out var d))
            {
                Debug.LogWarning($" !!!! Can't Find ProtocolID:{protocol} In Event Dispatcher");
                return;
            }

            var action = d as Action<T>;
            action?.Invoke(para1);
        }

        public static void EventDispatcher<T, U>(int protocol, T para1, U para2)
        {
            if (!_eventDict.TryGetValue(protocol, out var d)) return;
            var action = d as Action<T, U>;
            action?.Invoke(para1, para2);
        }

        public static void EventDispatcher<T, U, X>(int protocol, T para1, U para2, X para3)
        {
            if (!_eventDict.TryGetValue(protocol, out var d)) return;
            var action = d as Action<T, U, X>;
            action?.Invoke(para1, para2, para3);
        }

        public static void EventDispatcher<T, U, X, W>(int protocol, T para1, U para2, X para3, W para4)
        {
            if (!_eventDict.TryGetValue(protocol, out var d)) return;
            var action = d as Action<T, U, X, W>;
            action?.Invoke(para1, para2, para3, para4);
        }

        public void Init()
        {
//            Debug.LogError($"===== LC_MessageManager : Init =======");
            this.UpdateAsObservable().Sample(TimeSpan.FromMilliseconds(50)).Where(_ => _netMessageQueue.Count > 0)
                .OnErrorRetry().Subscribe(_ =>
                {
                    while (_netMessageQueue.Count > 0)
                    {
                        var mess = _netMessageQueue.Dequeue();
                        if (mess.ProtocolId != 87 && mess.ProtocolId != 112)
                        {
                            Debug.Log($"--> Dispatcher --> ID:{mess.ProtocolId}");
                        }

//                        if (mess.ProtocolId.Equals(121020))
//                        {
//                            var i = 0;
//                        }
                        if (_registerDict.TryGetValue(mess.ProtocolId, out var proxy))
                        {
                            proxy?.Invoke(mess);
                        }
                    }
                });
        }

        private void Awake()
        {
//            Init();
            SetPersistent(gameObject);
        }
    }
}