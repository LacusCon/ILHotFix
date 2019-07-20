using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using UniRx;
using UnityEngine;
using static LC_Tools.LC_ResourceManager;

namespace LC_Tools
{
    public static class AddressConfig
    {
        public static string versionCode = "1.0.0";
        public const string WeChatAppID = "000000000000000000";
        public const RunningModelEnum RUNNING_MODEL_ENUM = RunningModelEnum.Editor;
        private static readonly List<string> _gameServers = new List<string>
        {
            "192.168.0.1,100", //Test
        };

        private static readonly List<string> _resServers = new List<string>
        {
            "192.168.0.1", //Test
        };

        private static readonly Queue<string> _gameQueue = new Queue<string>();
        private static readonly Queue<string> _resQueue = new Queue<string>();

        public static void GetGameServerIP(Action<string> callback)
        {
            InitQueue();
            SearchNet(_gameQueue, 0, callback);
        }

//        public static string GetGameServerIP()
//        {
//            return _gameServers[0];
//        }

        public static string GetResourceServerIP()
        {
            return _resServers[0];
        }

//        public static void GetResourceServerIP(Action<string> callback)
//        {
//            InitQueue();
//            SearchNet(_resQueue, 0, callback);
//        }

        private static void SearchNet(Queue<string> queue, int index, Action<string> callback)
        {
            if (index >= queue.Count)
            {
                Debug.LogError("!!! Have not Valid IP Address !!!");
                callback.Invoke(null);
                return;
            }

            var address = queue.Dequeue();
            queue.Enqueue(address);
            
//            callback.Invoke(address);//TODO
            CheckValid(ChangeAddress(address), ret =>
            {
                Debug.LogWarning($"=== SearchNet Check: {address} Ret:{ret}");
                if (ret)
                {
                    callback.Invoke(address);
                }
                else
                {
                    SearchNet(queue, ++index, callback);
                }
            });
        }

        private static readonly Regex IpRegex = new Regex(@"(\d{1,3}(\.\d{1,3}){3})");
        private static readonly Regex DomainRegex = new Regex(@"^(http(s)?://)?(.+?)/?$");

        private static string ChangeAddress(string address)
        {
            var group = address.Split(',');
            var port = "";
//            if (group.Length == 2)
//            {
//                port = $":{group[1]}";
//            }
            var curIp = group[0];
            if (IpRegex.IsMatch(curIp))
            {
                return IpRegex.Match(curIp).Groups[1].Value + port;
            }

            if (!DomainRegex.IsMatch(curIp)) return "";
            var domain = DomainRegex.Match(curIp).Groups[3].Value;
            var hostInfo = Dns.GetHostEntry(domain);
            return hostInfo.AddressList[0] + port;
        }

        private static void CheckValid(string url, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(url))
            {
                callback.Invoke(false);
                return;
            }

            Observable.FromMicroCoroutine(token =>
                    StartPing(url, callback, token))
                .Timeout(TimeSpan.FromMilliseconds(300))
                .DoOnError(ex =>
                {
                    callback.Invoke(false);
                })
                .Subscribe();
        }

        private static IEnumerator StartPing(string ip, Action<bool> callback, CancellationToken token)
        {
            var p = new Ping(ip);
            while (!p.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return null;
            }

            callback?.Invoke(p.time >= 0);
        }

        private static void InitQueue()
        {
            if (_gameQueue.Count == 0)
            {
                foreach (var server in _gameServers)
                {
                    _gameQueue.Enqueue(server);
                }
            }

            if (_resQueue.Count != 0) return;
            foreach (var server in _resServers)
            {
                _resQueue.Enqueue(server);
            }
        }
    }
}