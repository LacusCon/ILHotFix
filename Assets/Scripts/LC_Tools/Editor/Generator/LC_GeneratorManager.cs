#if ILPROJ
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LC_Tools
{
    class LC_GeneratorManager : LC_GeneratorBase<string>
    {
        public static readonly string TEMPLATE_PATH = Application.dataPath + @"/Scripts/LC_Tools/Editor/Generator/Template/";
        public static readonly string ADAPTOR_OUT_PATH = Application.dataPath + @"/ILRuntime/Adaptors/";
        public static readonly string HELPER_OUT_PATH = Application.dataPath + @"/ILRuntime/Register/";
        public static readonly string GEN_NAMESPACE = @"ILRuntime.Runtime.Generated";

        private List<string> _delegateAdaptorList = new List<string>();
        private List<string> _delegateConvertList = new List<string>();
        private List<string> _overParaList = new List<string>();

        private static readonly string[] _CUSTOM_ADAPTOR = {
            //"|System.Int32,System.Collections.Generic.List<WYY.NA.GameFramework.TurnItemControl>,System.Int32[]|",
            //"UnityEngine.U2D.SpriteAtlas||",
            "WYY.NA.GameFramework.Telegram_Json.GeneralSituationRet||",
            "WYY.NA.GameFramework.Telegram_Json.DevelopmentAgentRet||",
        };

        private static readonly string[] _CUSTOM_CONVERTOR = {
            "WYY.NA.GameFramework.EventTriggerListener.VoidDelegate||UnityEngine.GameObject|",
//            "WYY.NA.GameFramework.WYYDoTweenAgentTweenCallback|||",
            //"UnityEngine.Component|ILRuntime.Runtime.Intepreter.ILTypeInstance|ILRuntime.Runtime.Intepreter.ILTypeInstance|",
        };

        private static readonly string[] _EXCEPT_CONVERTOR = {
            //"System.Comparison",
        };

        private static readonly string[] _COMMON_NAMESPACE = {
            "WYY.NA.GameFramework",
            "LC_Tools",
        };

        private static readonly string[] _GENERIC_SYMBOL = {
            "T",
            "U",
        };

        //private string _filePath;
        private class DelegateType
        {
            public const string Action = "System.Action";
            public const string Func = "System.Func";
            private const string Predicate = "System.Predicate";
            private const string UnityAction = "UnityEngine.Events.UnityAction";
            public const string Delegate = @"/"; //TODO
            private const string CommonDelegate = @"::.ctor(System.Object,System.IntPtr)";

            public static bool IsDelegateType(string str)
            {
                if (str.Contains(Action)) return true;
                if (str.Contains(Func)) return true;
                if (str.Contains(Predicate)) return true;
                if (str.Contains(UnityAction)) return true;
                return str.Contains(Delegate) || str.Contains(CommonDelegate);
            }

            public static bool IsJustDelegate(string str)
            {
                if (str.Contains(Action) || str.Contains(Func) || str.Contains(Predicate) || str.Contains(UnityAction)) return false;
                return str.Contains(Delegate) || str.Contains(CommonDelegate);
            }
        }

        private LC_AdaptorGenerator _adaptor;
        private LC_AdaptorHelperGenerator _adaptorHelper;
        private LC_DelegateRegisterGenerator _register;
        private LC_DelegateConveterGenerator _delegate;

        public LC_GeneratorManager()
        {
            _adaptor = new LC_AdaptorGenerator();
            _adaptorHelper = new LC_AdaptorHelperGenerator();
            _register = new LC_DelegateRegisterGenerator();
            _delegate = new LC_DelegateConveterGenerator();
        }

        public void GenerateAdaptor(Type[] types)
        {
            var out_path = ADAPTOR_OUT_PATH + @"Generated/";
            if (Directory.Exists(out_path))
            {
                Directory.Delete(out_path, true);
            }

            _adaptor.CreateFiles(types, out_path);
            _adaptorHelper.CreateFiles();
        }

        public void GenerateHelper()
        {
            var template_path = TEMPLATE_PATH + @"lc_helper.tmpd";
            //_filePath = TEMPLATE_PATH;
            LoadTemplateFromFile(template_path);
            LoadDataFromDLL();

            var full_path = new StringBuilder(HELPER_OUT_PATH).Append("LC_Helper.cs").ToString();
            if (!Directory.Exists(HELPER_OUT_PATH))
            {
                Directory.CreateDirectory(HELPER_OUT_PATH);
            }

            LoadData("");
            using (var fs = File.Create(full_path))
            {
                var classed = Generate();
                var blank = new Regex(@"\r\n +\r\n", RegexOptions.Singleline);
                if (blank.IsMatch(classed))
                {
                    classed = blank.Replace(classed, "\r\n");
                }

                var sw = new StreamWriter(fs);
                sw.Write(classed);
                sw.Flush();
            }

            Debug.LogWarning(" $$$  Helper was Generated  $$$");
        }

        private void LoadDataFromDLL()
        {
#if ILPROJ
            var path = LC_RuntimeManager.DLL_PATH;
            var method_count = 0;

            foreach (var full_path in Directory.GetFiles(path, "*_dlc.txt", SearchOption.TopDirectoryOnly))
            {
                var module = ModuleDefinition.ReadModule(full_path);
                if (module == null) return;

                foreach (var typeDefine in module.Types)
                {
                    method_count += typeDefine.Methods.Count;
                    foreach (var methodDefine in typeDefine.Methods)
                    {
                        var methodName = methodDefine.FullName;
                        var instructions = methodDefine?.Body?.Instructions;
                        if (instructions == null) continue;

                        foreach (var instruct in instructions)
                        {
                            var now_inst = instruct.Operand as MethodReference;
                            if (now_inst == null) continue;
                            var now_all = now_inst.ToString();
                            var now_name = now_inst.DeclaringType.FullName;
                            var now_ret = now_inst.ReturnType.ToString();

                            if (instruct.OpCode == OpCodes.Call && !DelegateType.IsDelegateType(now_all))
                            {
                                if (IsExceptContent(now_name)) continue;

                                //generic|para|return|
                                ParameterProcess(now_all, now_ret);
                            }

                            if (instruct.OpCode != OpCodes.Newobj || instruct.Previous == null || instruct.Previous.OpCode != OpCodes.Ldftn) continue;
                            if (!DelegateType.IsDelegateType(now_all)) continue;
                            var pre_inst = instruct.Previous.Operand as MethodReference;
                            if (pre_inst == null) continue;
                            var pre_all = pre_inst.ToString();
                            var pre_ret = pre_inst.ReturnType.ToString();
                            var register_name = now_name;
                            //string wait_name = now_name;
                            //string wait_ret = now_ret;

                            if (now_name.Contains(DelegateType.Delegate))
                            {
                                var para_regex = new Regex(" .*?::(.*)");
                                if (para_regex.IsMatch(pre_all))
                                {
                                    register_name = para_regex.Match(pre_all).Groups[0].Value;
                                    //wait_name = para_regex.Match(pre_all).Groups[0].Value;
                                    //wait_ret = pre_ret;
                                }
                            }

                            if (IsExceptContent(pre_all)) continue;

                            //method|generic|para|return|
                            var line_para = ParameterProcess(pre_all, pre_ret, now_name);
                            if (string.IsNullOrEmpty(line_para)) line_para = @"|||";
                            MethodProcess(register_name, line_para);
                        }
                    }
                }
            }

            AddCustomAdaptor();
            AddCustomConverter();

            //for (int i = 0; i < _delegateAdaptorList.Count; i++)
            //{
            //    Debug.Log(string.Format("******* Delegate Adpater Index:[{0}] Content:[{1}] *******", i, _delegateAdaptorList[i]));
            //}
            //Debug.Log(" =========================== Delegate Adpater Up && Convert Down 4 =========================== ");
            //for (int i = 0; i < _delegateConvertList.Count; i++)
            //{
            //    Debug.Log(string.Format("******* Delegate Converter Index:[{0}] Content:[{1}] *******", i, _delegateConvertList[i]));
            //}
            for (var i = 0; i < _overParaList.Count; i++)
            {
                Debug.LogWarning(string.Format("!!!!! Parameters Over !!!!! index:[{0}] Content:[{1}]", i, _overParaList[i]));
            }
#endif
        }

        private string ParameterProcess(string src_para, string ret_str, string gen_con = null)
        {
            var normal_call = @"(<(.+?)>)*?\((.*)\)"; // all string normal call
//            string normal_call = @"^(<(.+?)>)*?(?!.*:).*\((.*)\)"; // all string normal call
            var dg_generic = @"<(.*)>";
            var dg_generic_para = @"(<(.*?)>)*?\((.*)\)";

            var fix_src_para = SplitColon(src_para);
            if (fix_src_para.StartsWith(ret_str))
            {
                fix_src_para = fix_src_para.Substring(ret_str.Length);
            }

            var fix_gen_con = SplitColon(gen_con);
            var line_generic = "";
            var line_para = "";
            var line_ret = SplitColon(ret_str);

            if (string.IsNullOrEmpty(fix_gen_con))
            {
                var norCallReg = new Regex(normal_call);
                if (norCallReg.IsMatch(fix_src_para))
                {
                    var ngc = norCallReg.Match(fix_src_para).Groups;
                    var nor_gen = ngc[2].Value;
                    if (string.IsNullOrEmpty(nor_gen)) //TODO
                    {
                        var sim = new Regex("<(.*)>");
                        if (sim.IsMatch(fix_src_para))
                        {
                            nor_gen = sim.Match(fix_src_para).Groups[1].Value;
                        }
                    }

                    var nor_para = ngc[3].Value;
                    line_generic = nor_gen;

//                    if (!string.IsNullOrEmpty(nor_gen))
                    //{
                    var generic_fixed = ProcessGenericPara(nor_gen, nor_para, ret_str);
                    line_para = generic_fixed.Item1;
                    line_ret = generic_fixed.Item2;
                    // }
                }
            }
            else
            {
                var registerReg = new Regex(dg_generic);
                var register_gen = "";
                if (registerReg.IsMatch(fix_gen_con))
                {
                    register_gen = registerReg.Match(fix_gen_con).Groups[1].Value;
                }

                var genParaReg = new Regex(dg_generic_para);
                if (genParaReg.IsMatch(fix_src_para))
                {
                    var ggc = genParaReg.Match(fix_src_para).Groups;
                    var chg_gen = ggc[2].Value;
                    var paras = ggc[3].Value;

                    var generic_fixed = ProcessGenericPara(chg_gen, paras, ret_str);
                    line_para = generic_fixed.Item1;
                    line_ret = generic_fixed.Item2;
                }

                line_generic = register_gen;
            }

            if (line_ret.Equals("System.Void"))
            {
                line_ret = "";
            }

            var ret_builder = new StringBuilder(line_generic).Append("|").Append(line_para).Append("|").Append(line_ret);
            var result = ChangeUncommonToBak(ret_builder.ToString());

            // 跨域参数不要超过4个
            var para_count = SplitComplexParameter(line_para).Count;
            if (para_count > 4)
            {
                //Debug.LogWarning(string.Format(" !!!!!! Parameter Count Over Limited !!!!! Count:[{0}] Str:[{1}]", para_count, fix_src_para));
                if (!_overParaList.Contains(fix_src_para))
                {
                    _overParaList.Add(fix_src_para);
                }

                return null;
            }

            result = ProcessGenericPara(result).Replace("/", ".").Replace("&", "");
            AddRegister(result);
            return result;
        }

        private string MethodProcess(string fullName, string line_para)
        {
            var convert_content = fullName;
            var fix_Reg = new Regex(@"(^.*?)[`<\(]");
            if (fix_Reg.IsMatch(fullName)) convert_content = fix_Reg.Match(fullName).Groups[1].Value;

            if (IsILElement(convert_content))
            {
                return null;
            }

            if (convert_content.Contains(DelegateType.Action) || convert_content.Contains(DelegateType.Func)) return null;

            convert_content += "|" + line_para;
            convert_content = convert_content.Replace("/", ".");
            AddConverter(convert_content);
            return convert_content;
        }

        private Tuple<string, string> ProcessGenericPara(string src_generic, string src_para, string src_ret)
        {
            var gen_list = SplitComplexParameter(src_generic);
            var tmp_para = src_para;

            if (_GENERIC_SYMBOL.Length > 0)
            {
                var symbol_index = new List<string>();
                var genSb = new StringBuilder(@"\b(");
                foreach (var t in _GENERIC_SYMBOL)
                {
                    genSb.Append(t).Append(@"|");
                }

                var gen_str = genSb.ToString().Trim('|') + @")\b";
                var sort_reg = new Regex(gen_str);
                if (sort_reg.IsMatch(tmp_para))
                {
                    var mc = sort_reg.Matches(tmp_para);
                    for (var i = 0; i < mc.Count; i++)
                    {
                        var now_str = mc[i].Value;
                        if (!symbol_index.Contains(now_str))
                        {
                            symbol_index.Add(now_str);
                        }
                    }
                }

                for (var i = 0; i < symbol_index.Count; i++)
                {
                    var gen_change = new Regex(@"\b" + symbol_index[i] + @"\b");
                    if (gen_list.Count > i)
                    {
                        tmp_para = gen_change.Replace(tmp_para, gen_list[i]);
                    }

//                    else
//                    {
//                        int ii = 0;
//                    }
                }

                //Debug.LogWarning(string.Format("==ParameterProcess Generic Result == Src:[{0}] Dest:[{1}]", src_para, tmp_para));
            }

            var _fixed_unknow = new Regex(@"!+(\d+)");
            if (_fixed_unknow.IsMatch(tmp_para))
            {
                var index = int.Parse(_fixed_unknow.Match(tmp_para).Groups[1].Value);
                tmp_para = _fixed_unknow.Replace(tmp_para, gen_list[index]);
            }

            var tmp_ret = src_ret;
            if (!_fixed_unknow.IsMatch(src_ret)) return new Tuple<string, string>(tmp_para, tmp_ret);
            {
                var index = int.Parse(_fixed_unknow.Match(src_ret).Groups[1].Value);
                if (gen_list.Count > index)
                {
                    tmp_ret = _fixed_unknow.Replace(src_ret, gen_list[index]);
                }
//                else
//                {
//                    int ii = 0;
//                }
            }

            return new Tuple<string, string>(tmp_para, tmp_ret);
        }

        private bool IsExceptContent(string cont)
        {
            return _EXCEPT_CONVERTOR.Any(cont.Contains);
        }

        public static List<string> SplitComplexParameter(string src_para)
        {
            var para_list = new List<string>();
            if (string.IsNullOrEmpty(src_para)) return para_list;

            if (!src_para.Contains("<"))
            {
                para_list = src_para.Split(',').ToList();
            }
            else
            {
                var tmpSB = new StringBuilder();
                var para_array = src_para.ToArray();
                var angle_count = 0;
                foreach (var now in para_array)
                {
                    switch (now)
                    {
                        case '<':
                            angle_count++;
                            break;
                        case '>':
                            angle_count--;
                            break;
                        default:
                        {
                            if (angle_count == 0 && now.Equals(','))
                            {
                                para_list.Add(tmpSB.ToString());
                                tmpSB.Clear();
                                continue;
                            }

                            break;
                        }
                    }

                    tmpSB.Append(now);
                }

                if (tmpSB.Length > 0)
                {
                    para_list.Add(tmpSB.ToString());
                    tmpSB.Clear();
                }
            }

            return para_list;
        }

        private static string CommaMatch(Match m)
        {
            return m.Value.Equals(",") ? "|" : m.Value;
        }

        private static string ChangeUncommonToBak(string src)
        {
            //if (!IsILElement(src)) return src;
            var naRegex = new Regex(@"WYY\.NA\.(?!GameFramework)[\._\da-zA-Z]+");
            return naRegex.IsMatch(src) ? naRegex.Replace(src, "ILTypeInstance") : src;
        }

        private static string SplitColon(string src)
        {
            //"!!0 UnityEngine.Component::GetComponent<WYY.NA.GameFramework.WindowAnimation>()"
            //"!!0 UnityEngine.Object::Instantiate<UnityEngine.GameObject>(!!0)"
            //"!1 System.Collections.Generic.KeyValuePair`2<System.Int32,WYY.NA.GameFramework.CMsg>::get_Value()"
            if (string.IsNullOrEmpty(src)) return src;
            var colon = new Regex(@"::[^<>]*?\(\)");
            return colon.IsMatch(src) ? colon.Replace(src, "()") : src;
        }

        private bool IsILElement(string cont)
        {
            // Common || ILRuntime Pack
            return !string.IsNullOrEmpty(cont) && !IsCommonElement(cont) && cont.Contains("WYY.NA.");
        }

        private static bool IsCommonElement(string cont)
        {
            return _COMMON_NAMESPACE.Any(cont.Contains);
        }

        private void AddRegister(string content)
        {
            var amount_array = content.Split('|');
            var count = amount_array.Sum(t => t.Length);

            if (count > 0 && !_delegateAdaptorList.Contains(content))
            {
                _delegateAdaptorList.Add(content);
            }
        }

        private void AddConverter(string content)
        {
            if (!string.IsNullOrEmpty(content) && !_delegateConvertList.Contains(content))
            {
                _delegateConvertList.Add(content);
            }
        }

        private static string ProcessGenericPara(string para)
        {
            var generic_regex = new Regex(@"`\d+");
            return generic_regex.IsMatch(para) ? generic_regex.Replace(para, "") : para;
        }

        protected override bool LoadData(string data)
        {
            var registerList = new List<string>();
//            var regRegex = new Regex($"^.*<(.+)>", RegexOptions.Singleline);
            var regRegex = new Regex(@"^.*(app\.DelegateManager\.Register(.*<.+>).*?;)", RegexOptions.Singleline);
            var registerSB = new StringBuilder();
            foreach (var t in _delegateAdaptorList)
            {
                var ret = CreateDelegateRegisterInit(t);
                if (regRegex.IsMatch(ret))
                {
                    var groups = regRegex.Match(ret).Groups;
                    var key = groups[2].Value;
                    if (registerList.Contains(key)) continue;
                    registerList.Add(key);
                    registerSB.Append("\t\t\t").Append(groups[1].Value).Append("\n");
                }
                else
                {
                    Debug.LogError($"!!! LoadData Error :[{ret}] !!!");
                }

                //Debug.Log(string.Format(" ++++++ LoadData Delegate Adaptor ++++++ Index:[{0}] Src:[{1}] Ret:[{2}] ", i, _delegateAdaptorList[i], ret));
            }

            SetKeyValue("{$DelegateRegInit}", registerSB.ToString());

            var convertSB = new StringBuilder();
            foreach (var t in _delegateConvertList)
            {
                var ret = CreateDelegateConverterInit(t);
                convertSB.Append(ret);
                //Debug.Log(string.Format(" ++++++ LoadData Delegate Convertor ++++++ Index:[{0}] Src:[{1}] Ret:[{2}] ", i, _delegateConvertList[i], ret));
            }

            SetKeyValue("{$DelegateConvertInit}", convertSB.ToString());
            SetKeyValue("{$Namespace}", GEN_NAMESPACE);
            return true;
        }

        private string CreateDelegateRegisterInit(string data)
        {
            _register.InitFromFile(TEMPLATE_PATH + "lc_delegate_register.tmpd", data);
            return _register.Generate();
        }

        private string CreateDelegateConverterInit(string data)
        {
            _delegate.InitFromFile(TEMPLATE_PATH + "lc_delegate_converter.tmpd", data);
            return _delegate.Generate();
        }

        private void AddCustomAdaptor()
        {
            foreach (var t in _CUSTOM_ADAPTOR)
            {
                AddRegister(t);
            }
        }

        private void AddCustomConverter()
        {
            foreach (var t in _CUSTOM_CONVERTOR)
            {
                AddConverter(t);
            }
        }
    }
}