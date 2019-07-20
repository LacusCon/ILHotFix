using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LC_Tools
{
    public class LC_ValuerGenerator : LC_GeneratorBase<LC_Valuer>
    {
        private static string format_str = @"<#{0}et_start#>.*<#{0}et_end#>";
        private Regex _getRegex = new Regex(String.Format(format_str, "g"), RegexOptions.Singleline);
        private Regex _setRegex = new Regex(String.Format(format_str, "s"), RegexOptions.Singleline);

        protected override bool LoadData(LC_Valuer valuer)
        {
            MethodInfo methodInfo = null;
            if (String.IsNullOrEmpty(Template)) return false;

            if (valuer.get_method == null && _getRegex.IsMatch(Template))
            {
                Template = _getRegex.Replace(Template, "");
            }
            if (valuer.set_method == null && _setRegex.IsMatch(Template))
            {
                Template = _setRegex.Replace(Template, "");
            }

            if (valuer.get_method != null)
            {
                methodInfo = valuer.get_method;
                SetKeyValue("{$returnDefault}", "return " + GetTypeNullValue(methodInfo.ReturnType) + ";");
            }
            else
            {
                methodInfo = valuer.set_method;
            }

            if (!CommonProcess(methodInfo))
            {
                return false;
            }

            //SetKeyValue("{$override}", "");
            SetKeyValue("{$MethodName}", methodInfo.Name.Substring(4));
            return true;
        }
    }

}