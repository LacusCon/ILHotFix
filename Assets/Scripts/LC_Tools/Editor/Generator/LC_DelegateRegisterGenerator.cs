using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LC_Tools
{
    public class LC_DelegateRegisterGenerator : LC_GeneratorBase<string>
    {
        private static string format_str = @"<#{0}_start#>.*<#{0}_end#>";
        private Regex _returnRegex = new Regex(String.Format(format_str, "return"), RegexOptions.Singleline);
        private Regex _voidRegex = new Regex(String.Format(format_str, "void"), RegexOptions.Singleline);

        protected override bool LoadData(string data)
        {
            if (data == null || String.IsNullOrEmpty(Template))
                return false;

            string[] content = data.Split('|');
            if (content.Length < 3)
            {
                Debug.Log(string.Format("== Delegate Register Format is Missing  Len:[{0}] Content:[{1}] ==", content.Length, content));
            }

            string generic = content[0];
            string para = content[1];
            string ret = content[2];

            //TODO for Dictionary Para Register
            if (string.IsNullOrEmpty(para))
            {
                para = generic;
            }
            
            if (String.IsNullOrEmpty(ret))
            {
                if (_returnRegex.IsMatch(Template))
                {
                    Template = _returnRegex.Replace(Template, "");
                }
            }
            else
            {
                if (_voidRegex.IsMatch(Template))
                {
                    Template = _voidRegex.Replace(Template, "");
                }
            }

            string show_str = para;
            if (String.IsNullOrEmpty(para))
            {
                show_str = ret;
            }
            else if(!String.IsNullOrEmpty(ret)) {
                show_str += ", " + ret;
            }

            SetKeyValue("{$argsType}", show_str);

            ClearDefinedText();
            return true;
        }
    }
}
