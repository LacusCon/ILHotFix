using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LC_Tools
{
    public static class PreloadConfig
    {
        public static List<string> GetPreloadList(string model)
        {
            var txt = Resources.Load<TextAsset>("Preload");
            var json = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txt.text);
            return json.ContainsKey(model) ? json[model] : null;
        }
    }
}