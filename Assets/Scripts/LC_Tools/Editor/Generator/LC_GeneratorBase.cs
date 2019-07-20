using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace LC_Tools
{
    public abstract class LC_GeneratorBase<TData> : LC_IGenerator
    {
        private readonly Regex _regex = new Regex("\\{\\$(?:[a-z][a-z0-9_]*)\\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private readonly Regex _defineRegex = new Regex("<#.*?#>", RegexOptions.Singleline);

        protected string Template;
        protected TData Data;

        protected string GenTmpad;
        protected HashSet<string> KeyWordList = new HashSet<string>();
        protected HashSet<string> DefinedList = new HashSet<string>();
        protected Dictionary<string, object> KeyDictionary = new Dictionary<string, object>();
        protected Dictionary<string, int> countDict = new Dictionary<string, int>();

        private static Dictionary<string, string> _templateDict = new Dictionary<string, string>();
        protected virtual bool LoadTemplate(string template)
        {
            Template = template;

            KeyWordList.Clear();
            var m = _regex.Match(Template);
            while (m.Success)
            {
                if (!KeyWordList.Contains(m.Value))
                {
                    KeyWordList.Add(m.Value);
                }
                m = m.NextMatch();
            }

            return !string.IsNullOrEmpty(Template);
        }

        protected virtual bool LoadTemplateFromFile(string filePath)
        {
            var content = "";
            var key = filePath.Replace("/", ".");
            if (_templateDict.ContainsKey(key))
            {
                content = _templateDict[key];
            }
            else { 
                if (!File.Exists(filePath))
                    return false;
                content = File.ReadAllText(filePath);
                _templateDict.Add(key, content);
            }

            LoadTemplate(content);
            return true;
        }

        protected abstract bool LoadData(TData data);

        protected void SetKeyValue(string key, object content)
        {
            var cont = content.ToString();
            if (!KeyWordList.Contains(key))
            {
                Console.WriteLine("Invalid key word");
                return;
            }

            if (KeyDictionary.ContainsKey(key))
                KeyDictionary[key] = cont;
            else
                KeyDictionary.Add(key, cont);
        }

        protected void ClearDefinedText() {
            Template = _defineRegex.Replace(Template, "");
        }

        private string GetContent(string key)
        {
            if (!KeyDictionary.ContainsKey(key))
            {
                Debug.LogWarning($"== GetContent Have Not Key :[{key}] ==");
                return "";
            }
            var content = KeyDictionary[key];
            var s = content as string;
            if (s != null)
                return s;
            var generator = content as LC_IGenerator;
            return generator?.Generate();
        }

        private void Replace(string keyword, string content)
        {
            GenTmpad = GenTmpad.Replace(keyword, content);
        }

        public void Clear()
        {
            countDict.Clear();
        }

        protected bool CommonProcess(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                return false;

            var argStr = "";
            var argNoTypeStr = "";

            var static_str = "";
            //if (methodInfo.IsStatic)
            //{
            //    static_str = "static";
            //}
            SetKeyValue("{$static}", static_str);

            var target_name = methodInfo.Name;
            if (countDict.ContainsKey(target_name))
            {
                countDict[target_name]++;
            }
            else
            {
                countDict.Add(target_name, 0);
            }
            SetKeyValue("{$MethodNameNum}", target_name + countDict[target_name]);
            SetKeyValue("{$modifier}", GetAccessModifier(methodInfo));

            SetKeyValue("{$returnType}", methodInfo.ReturnType.FullName == "System.Void" ? "void" : methodInfo.ReturnType.Name);

            SetKeyValue("{$MethodName}", methodInfo.Name);
            foreach (var pInfo in methodInfo.GetParameters())
            {
                var paramType = pInfo.ParameterType.Name;
                if (paramType.Equals("Object"))
                {
                    paramType = paramType.ToLowerInvariant();
                }
                if (paramType.Equals("List`1"))
                {
                    var gg = pInfo.ParameterType.GetGenericArguments();
                    paramType = $"List<{gg[0]}>";
                }

                argStr += paramType + " " + pInfo.Name + ",";
                argNoTypeStr += pInfo.Name + ",";
            }

            SetKeyValue("{$Generic}", methodInfo.IsGenericMethod ? "<T>" : "");

            if (!methodInfo.IsFinal && (methodInfo.IsAbstract || methodInfo.IsVirtual))
            {
                //Debug.Log(string.Format("%%% Check5 Can be Override: Name:[{0}] IsAbstract:[{1}] DeclaringType:[{2}] ReflectedType[{3}]", 
                //    methodInfo.Name, methodInfo.IsAbstract, methodInfo.DeclaringType, methodInfo.ReflectedType));
                SetKeyValue("{$override}", "override");
            }
            else
            {
                SetKeyValue("{$override}", "new");
            }

            argStr = argStr.Trim(',');
            argNoTypeStr = argNoTypeStr.Trim(',');
            SetKeyValue("{$args}", argStr);
            SetKeyValue("{$args_count}", methodInfo.GetParameters().Length);
            SetKeyValue("{$comma}", argStr == "" ? "" : ",");
            SetKeyValue("{$args_no_type}", argNoTypeStr);

            ClearDefinedText();
            return true;
        }

        protected string GetAccessModifier(MethodInfo method)
        {
            if (method.IsPrivate)
                return "private";
            if (method.IsFamilyOrAssembly)
                return "protected internal";
            if (method.IsAssembly)
                return "internal";
            if (method.IsFamily)
                return "protected";
            if (method.IsPublic)
                return "public";
            //dont know what the hell 
            //if (method.IsFamilyAndAssembly)
            //    return "public";
            return "public";
        }

        protected string GetTypeNullValue(Type in_type)
        {
            var ret_str = "";
            switch (in_type.Name)
            {
                case nameof(Byte):
                case nameof(UInt16):
                case nameof(Int16):
                case nameof(UInt32):
                case nameof(Int32):
                case nameof(UInt64):
                case nameof(Int64):
                case nameof(Single):
                case nameof(Double):
                    ret_str = "0";
                    break;
                case nameof(Boolean):
                    ret_str = "false";
                    break;
                case nameof(HideFlags):
                case nameof(Selectable.Transition):
                    ret_str = in_type.Name + ".None";
                    break;
                case nameof(SpriteState):
                    ret_str = "new SpriteState()";
                    break;
                case nameof(Type):
                    ret_str = "new Type()";
                    break;
                case nameof(Image.FillMethod):
                    ret_str = "new FillMethod()";
                    break;
                case nameof(Navigation):
                    ret_str = "new Navigation()";
                    break;
                case nameof(ColorBlock):
                    ret_str = "new ColorBlock()";
                    break;
                case nameof(ScrollRect.ScrollbarVisibility):
                    ret_str = "new ScrollbarVisibility()";
                    break;
                case nameof(ScrollRect.MovementType):
                    ret_str = "new MovementType()";
                    break;
                    
                //case nameof(UnityEngine.UI.Image.Type):
                //    ret_str = "UnityEngine.UI.Image.Type.Simple";
                //    break;
                //case nameof(UnityEngine.UI.Image.FillMethod):
                //    ret_str = "UnityEngine.UI.Image.FillMethod.Horizontal";
                //    break;
                //case nameof(UnityEngine.UI.ScrollRect.MovementType):
                //    ret_str = "UnityEngine.UI.ScrollRect.MovementType.Unrestricted";
                //    break;
                //case nameof(UnityEngine.UI.ScrollRect.ScrollbarVisibility):
                //    ret_str = "UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent";
                //    break;
                case nameof(Vector2):
                case nameof(Vector3):
                case nameof(Vector4):
                case nameof(Rect):
                    ret_str = in_type.Name + ".zero";
                    break;
                case nameof(UnityEngine.Color):
                    ret_str = "UnityEngine.Color.black";
                    break;
                //case nameof(System.Collections.IDictionary):
                    //break;
                case "T":
                    ret_str = "default(T)";
                    break;
                default:
                    if (in_type.FullName.Equals("UnityEngine.UI.Image.Type"))
                    {
                        ret_str = "UnityEngine.UI.Image.Type.Simple";
                    }
                    //Debug.LogWarning(string.Format("!!!! Type:[{0}] have not match case !!!!", in_type));
                    ret_str = "null";
                    break;
            }
            return ret_str;
        }

        public bool InitFromFile(string tmpdFilePath, TData data)
        {
            return LoadTemplateFromFile(tmpdFilePath) && LoadData(data);
        }

        public string Generate()
        {
            if (string.IsNullOrEmpty(Template))
            {
                Console.WriteLine("{0}'s Template  is null,please use LoadTemplate to init template", GetType().Name);
                return null;
            }

            GenTmpad = Template;

            foreach (var key in KeyWordList)
            {
                Replace(key, GetContent(key));
            }

            return GenTmpad;
        }
    }
}
