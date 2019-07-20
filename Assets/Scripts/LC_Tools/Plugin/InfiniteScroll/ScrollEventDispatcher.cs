using System;
using System.Collections.Generic;
using UnityEngine;

//fork from  https://github.com/tsubaki/Unity_UI_Samples
namespace LC_Tools
{
    public static class ScrollEventDispatcher
    {
        public enum RecordType
        {
            GameSelector,
            Bank,
            Att,
            Head,
        }

        // Use this for initialization
        public static readonly Dictionary<RecordType, Action<GameObject, int>> targetDict = new Dictionary<RecordType, Action<GameObject, int>>();

        public static void AppendRecordType(RecordType rt, Action<GameObject, int> target)
        {
            if (targetDict.ContainsKey(rt))
            {
                targetDict[rt] = target;
            }
            else
            {
                targetDict.Add(rt, target);
            }
        }

        public static void RemoveRecordType(RecordType rt)
        {
            if (targetDict.ContainsKey(rt))
            {
                targetDict.Remove(rt);
            }
        }
    }
}