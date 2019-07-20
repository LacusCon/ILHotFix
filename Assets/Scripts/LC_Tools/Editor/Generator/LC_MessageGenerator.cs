using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LC_Tools
{
    public class LC_MessageGenerator : LC_GeneratorBase<string>
    {
        protected override bool LoadData(string message_name)
        {
            string[] mess_array = message_name.Split('|');

            int para_count = 0;
            string name = mess_array[0];
            string paras = "";
            if (mess_array.Length > 1)
            {
                paras = mess_array[1];
            }

            List<string> para_array = LC_GeneratorManager.SplitComplexParameter(paras);
            para_count = para_array.Count;

            string args = "";
            string args_no_type = "";
            for (int i = 0; i < para_count; i++)
            {
                string tmp = para_array[i];
                string now_arg = " arg" + i + ",";
                args += tmp + now_arg;
                args_no_type += now_arg;
            }

            if (args_no_type.Length == 0)
            {
                args_no_type = "null";
            }

            SetKeyValue("{$args}", args.Trim(','));
            SetKeyValue("{$args_no_type}", args_no_type.Trim(','));
            SetKeyValue("{$args_count}", para_count);
            SetKeyValue("{$MethodName}", name);
            return true;
        }
    }
}