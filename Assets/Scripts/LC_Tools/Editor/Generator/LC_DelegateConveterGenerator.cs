using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LC_Tools
{
    public class LC_DelegateConveterGenerator : LC_GeneratorBase<string>
    {
        private static string format_str = @"<#{0}_start#>.*<#{0}_end#>";
        private Regex _returnRegex = new Regex(String.Format(format_str, "return"), RegexOptions.Singleline);
        private Regex _voidRegex = new Regex(String.Format(format_str, "void"), RegexOptions.Singleline);
        //private Regex _fixedParaRegex = new Regex(@"<.*?>");

        protected override bool LoadData(string data)
        {
            if (data == null || String.IsNullOrEmpty(Template))
                return false;

            string[] content = data.Split('|');
            if (content.Length < 4)
            {
                Debug.Log(string.Format("== Delegate Convert Format is Missing  Len:[{0}] Content:[{1}] ==", content.Length, data));
            }

            string method = content[0];
            string generic = content[1];
            string para = content[2];
            string ret = content[3];

            //if (method.Contains("DG.Tweening.Core.DOGetter"))
            //{
            //    int i = 0;
            //}

            if (String.IsNullOrEmpty(ret))
            {
                if (_returnRegex.IsMatch(Template))
                {
                    Template = _returnRegex.Replace(Template, "");
                }
                SetKeyValue("{$returnType}", "");
                SetKeyValue("{$symbol}", "");
            }
            else
            {
                if (_voidRegex.IsMatch(Template))
                {
                    Template = _voidRegex.Replace(Template, "");
                }
                SetKeyValue("{$returnType}", ret);
            }

            int para_count = LC_GeneratorManager.SplitComplexParameter(para).Count;
            string args = "";
            for (int i = 0; i < para_count; i++)
            {
                args += "arg" + i + ",";
            }

            args = args.Trim(',');

            string fixed_name = method;

            if (!string.IsNullOrEmpty(generic))
            {
                fixed_name += "<" + generic + ">";
            }


            SetKeyValue("{$DelegateName}", fixed_name);
            if (!String.IsNullOrEmpty(para))
            {
                //Debug.Log(string.Format("==LoadData Content:[{0}] Len:[{1}]  Ret:[{2}]", para, para.Length, ret));
                SetKeyValue("{$symbol}", ", ");
            }
            else {
                SetKeyValue("{$symbol}", "");
            }

            SetKeyValue("{$argsType}", para);
            if (!String.IsNullOrEmpty(para))
            {
                SetKeyValue("{$angle_left}", "<");
                SetKeyValue("{$angle_right}", ">");
            }
            else {
                SetKeyValue("{$angle_left}", "");
                SetKeyValue("{$angle_right}", "");
            }

            SetKeyValue("{$args}", args);
            ClearDefinedText();
            return true;
        }
    }
}