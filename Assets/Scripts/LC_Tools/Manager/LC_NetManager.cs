using System.Collections.Generic;
using HiSocket;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using System;
//using GameFramework;

namespace LC_Tools
{
    public class LC_NetManager : Singleton<LC_NetManager>
    {
        private const int DATA_TIMEOUT = 7000; //ms
        private const int RECONNECT_INTERVAL = 6; //s
        private const int CONNECT_TIMEOUT = 115; //100 ms
        private const int BEATING_INTERVAL = 1000; //ms
        private const int BEATING_TIMEOUT = 3;
        private static TcpConnection _tcpConnection;
        private BinaryMessage _userInfo;

        private readonly Queue<BinaryMessage> _waitSend = new Queue<BinaryMessage>();
        private readonly BinaryMessage _heartMsg = new BinaryMessage {ProtocolId = 1};
        private readonly BinaryMessage _subHeartMsg = new BinaryMessage {ProtocolId = 0};

        private static int _infoCD;

        private bool AutoConnect { get; set; } = true;

        private static bool _firstSuccess;
        private static bool _isOpenPanel;
        private static bool _isConnecting;
        private static bool _isReconnect;
        public static bool waitServerResponse;

        private IDisposable _connectObserver;

        /// <summary>
        /// 重连成功后回调
        /// </summary>
        public Action reconnectCallback;

        public static bool IsConnected()
        {
            return !_isConnecting && _tcpConnection.IsConnected && !_isOpenPanel;
        }

        public void SaveUserInfo(BinaryMessage binary)
        {
            _userInfo = binary;
        }

        public void Send(BinaryMessage binary, bool log = true)
        {
            if (!_tcpConnection.IsConnected || waitServerResponse)
            {
                Debug.LogError($"== Send == -- Net Disconnected -- ID: {binary.ProtocolId}");
                _waitSend.Enqueue(binary);
                return;
            }

            if (log && binary.ProtocolId != 99)
            {
                Debug.Log($"~~~~~~~~~~~ Send Message ID:[{binary.ProtocolId}] ~~~~~~~~~~~");
            }

            var buffLen = binary.GetSendBuffer().Length;
            if (buffLen > 8000)
            {
                Debug.LogError($"!!!!!!!!!!!!! Send Message too large: {buffLen}");
            }

            Send(binary.GetSendBuffer());
        }

        private static void Send(byte[] message)
        {
            _tcpConnection.Send(message);
        }

        public void DisConnect()
        {
            _isConnecting = false;
            _tcpConnection.DisConnect();
        }

        public void SetHeartID(int pid)
        {
            _subHeartMsg.ProtocolId = pid;
        }

        private void Awake()
        {
            SetPersistent(gameObject);
            _tcpConnection = new TcpConnection(new DefaultNetPackage());
        }

        private void Start()
        {
            _tcpConnection.OnReceive += obj =>
            {
                _infoCD = 0;
                LC_MessageManager.StockNetMessage(obj);
            };

            _tcpConnection.OnConnected += ConnectedAction;
            _tcpConnection.OnDisconnected += DisconnectedAction;

            var panelCD = 0;
            this.LateUpdateAsObservable()
                .Sample(TimeSpan.FromMilliseconds(100))
                .OnErrorRetry().TakeUntilDestroy(this).Subscribe(_ =>
                {
                    if (_tcpConnection.IsConnected)
                    {
                        panelCD = 0;
                        return;
                    }

                    if (_isOpenPanel || ++panelCD < CONNECT_TIMEOUT) return;
                    panelCD = 0;
                    AutoConnect = false;
                    DisConnect();
                    SetReconnectPanel(true);
                });

            this.UpdateAsObservable()
                .Sample(TimeSpan.FromSeconds(RECONNECT_INTERVAL))
                .Where(_ => _firstSuccess && !_tcpConnection.IsConnected && AutoConnect && !_isOpenPanel && !_isConnecting)
                .OnErrorRetry()
                .TakeUntilDestroy(this)
                .Subscribe(
                    x =>
                    {
//                        Debug.LogWarning($"== LC_NetManager ReConnect  Connected:{_tcpConnection.IsConnected} == ");
                        DisConnect();
                        Reconnect();
                    });

            var chgBetting = true;
            this.UpdateAsObservable()
                .Where(_ => _tcpConnection.IsConnected && !_isOpenPanel && !waitServerResponse)
                .Sample(TimeSpan.FromMilliseconds(BEATING_INTERVAL))
                .OnErrorRetry()
                .TakeUntilDestroy(this)
                .Subscribe(x =>
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        DisConnect();
                        return;
                    }

                    if (chgBetting || _subHeartMsg.ProtocolId == 0)
                    {
//                    Debug.Log("~~~ Beating ~~~");
                        Send(_heartMsg, false);
                    }
                    else
                    {
                        //TODO delete when next framework
                        Send(_subHeartMsg);
                    }

                    chgBetting = !chgBetting;

                    if (++_infoCD < BEATING_TIMEOUT) return;
                    Debug.LogError($"$$$$$$$$$$$$$$$$ Beating TimeOut  $$$$$$$$$$$$$ {_infoCD}");
                    _infoCD = 0;
                    DisConnect();
                    Reconnect();
                });

            this.UpdateAsObservable().Where(_ => _tcpConnection.IsConnected && !_isConnecting && !waitServerResponse)
                .OnErrorRetry()
                .TakeUntilDestroy(this)
                .Subscribe(x =>
                {
                    while (_waitSend.Count > 0)
                    {
                        var msg = _waitSend.Dequeue();
                        Debug.Log($"==  --> Send Cache Msg --> == Count: {_waitSend.Count} ID:{msg.ProtocolId}");
                        Send(msg);
                    }
                });

            this.LateUpdateAsObservable()
                .Sample(TimeSpan.FromSeconds(RECONNECT_INTERVAL * 5))
                .Select(_ => waitServerResponse)
                .Pairwise().Where(x => x.Previous && x.Previous)
                .TakeUntilDestroy(this)
                .Subscribe(x =>
                {
                    Debug.LogError("!!! Wait Server Response TimeOut !!!");
//                    LC_MessageManager.EventDispatcher(100001, 0, "服务器应答超时！！！"); //TODO
                    ConnectPanel.Instance.SetTipsPanel(true, "网络状态不佳，请重开游戏", Application.Quit);
                    waitServerResponse = false;
                });

            ConnectPanel.Instance.successAction = () =>
            {
                Reconnect();
                SetReconnectPanel(false);
            };
            ConnectPanel.Instance.cancelAction = Application.Quit;
            StartConnect();
        }

        private void Reconnect()
        {
            _isReconnect = true;
            Observable.Timer(TimeSpan.FromMilliseconds(500)).Where(_ => _isConnecting || !_tcpConnection.IsConnected)
                .Subscribe(_ => { ConnectPanel.Instance.SetLoadingCircle(true, ConnectPanel.CircleEnum.CONNECTING); });
            StartConnect();
            AutoConnect = true;
        }

        private void ReconnectData()
        {
            _isReconnect = false;
            waitServerResponse = true;
            if (_userInfo == null || _userInfo.ProtocolId == 0) return;
            _tcpConnection.Send(_userInfo.GetSendBuffer());
            Debug.LogError($"$$$$$$$$$$$$$$$$ ReconnectData  $$$$$$$$$$$$$ Child:[{_userInfo.ProtocolId}]");
        }

        private static void StartConnect()
        {
            Debug.Log("==== LC_NetManager StartConnect =====");
            if (_tcpConnection.IsConnected) return;
            _isConnecting = true;
            AddressConfig.GetGameServerIP(UseValid);
//            UseValid(AddressConfig.GetGameServerIP());
        }

        private static void UseValid(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                _isConnecting = false;
                return;
            }

            var add = uri.Split(',');
            _tcpConnection.Connect(add[0], int.Parse(add[1]));
            _tcpConnection.Socket.SendTimeout = DATA_TIMEOUT;
            _tcpConnection.Socket.ReceiveTimeout = DATA_TIMEOUT;
        }

        private static void SetReconnectPanel(bool open)
        {
            _isOpenPanel = open;
            ConnectPanel.Instance.SetReconnectPanel(open);
            ConnectPanel.Instance.SetLoadingCircle(false, ConnectPanel.CircleEnum.CONNECTING);
        }

        private void ConnectedAction()
        {
            _connectObserver = Observable.WhenAll().ObserveOnMainThread().Subscribe(_ =>
            {
                Debug.Log("== --> ConnectedAction  --> ==");
                if (_isReconnect) ReconnectData();
                _isConnecting = false;
                if (_userInfo == null) waitServerResponse = false;
                ConnectPanel.Instance.SetLoadingCircle(false, ConnectPanel.CircleEnum.CONNECTING);
                _firstSuccess = true;
            });
        }

        private static void DisconnectedAction()
        {
            _isConnecting = false;
//            Debug.LogError(" !!== DisconnectedAction show dialog ==!! ");
        }
        
        private void OnDestroy()
        {
            _tcpConnection?.Dispose();
            _connectObserver?.Dispose();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            AutoConnect = !pauseStatus;
            if (pauseStatus)
            {
                DisConnect();
            }
            else
            {
                Reconnect();
            }
        }
    }
}