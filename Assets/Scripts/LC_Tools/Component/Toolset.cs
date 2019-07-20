using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace LC_Tools
{
    public static class Toolset
    {
        private const string RSA_PUBLIC_KEY =
            @"<RSAKeyValue><Modulus>xxxxxxxxxxxxxxxxxxxxxxxxxxx</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public static void CopyText(string textToCopy)
        {
            var editor = new TextEditor
            {
                text = textToCopy
            };
            editor.SelectAll();
            editor.Copy();
        }

        public static string PasteText()
        {
            var editor = new TextEditor();
            editor.Paste();
            return editor.text;
        }

        public static string GenRandomPwd(int len)
        {
            var gid = Guid.NewGuid().ToString().Replace("-", "");
            return len <= gid.Length ? gid.Substring(0, len) : gid;
        }

        /// <summary>
        /// 公開鍵暗号で文字列を暗号化する
        /// </summary>
        /// <param name="text">平文の文字列</param>
        /// <returns>暗号化された文字列</returns>
        public static string RSA_Encrypt(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(RSA_PUBLIC_KEY);
                data = rsa.Encrypt(data, false);
                return Convert.ToBase64String(data);
            }
        }

        public static void PostJsonFromURL<T>(string url, WWWForm form, Action<T> callback, Action<string> error = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                error?.Invoke("url is null");
                return;
            }
            
            ObservableWWW.Post(url, form)
                .Retry(3).DoOnError(onError =>
                {
                    Debug.LogError($"==PostJsonFromURL== {onError}");
                    error?.Invoke(onError.ToString());
                })
                .Subscribe(ret =>
                {
                    if (string.IsNullOrEmpty(ret))
                    {
                        callback?.Invoke(default(T));
                        return;
                    }

                    try
                    {
                        var para = JsonToObject<T>(ret);
                        callback?.Invoke(para);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"==PostJsonFromURL Change== {ex.ToString()}");
                        error?.Invoke(ex.Message.ToString());
                    }
                });
        }

        public static T JsonToObject<T>(string str)
        {
//            return JsonMapper.ToObject<T>(str);
            return JsonConvert.DeserializeObject<T>(str);
        }

        public static void LoadSpriteOnURL(string url, Action<Sprite> callback, Action<string> error)
        {
            if (string.IsNullOrEmpty(url))
            {
                callback?.Invoke(null);
                return;
            }

            ObservableWWW.GetWWW(url)
//                .Retry(3)
//                .SubscribeOnMainThread()
                .DoOnError(onError => { error?.Invoke(onError.ToString()); })
                .Subscribe(
                    www =>
                    {
//                        Debug.LogError("LoadImgOnURL:" + www.texture.name);
                        var tex = www.texture;
                        if (tex == null) return;
                        var ret = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                        ret.name = tex.name;
                        callback?.Invoke(ret);
                    },
                    onError => Debug.LogError("取得失敗")
                );
        }

        public static bool IsIPAddress(string url)
        {
            return Regex.IsMatch(url, @"^(http(s)*:\/\/)*(\d+\.){3}\d+", RegexOptions.IgnoreCase);
        }

        public static DateTime ConvertIntDatetime(uint utc)
        {
            var startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            return startTime.AddSeconds(utc);
        }

        public static int ConvertNowForUTCInterval(uint utc)
        {
            var startTime = ConvertIntDatetime(utc);
            return (int) (DateTime.Now - startTime).TotalMinutes;
        }
    }
}