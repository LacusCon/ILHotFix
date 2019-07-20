using System.Collections.Generic;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEngine;

namespace LC_Tools
{
    public static class LC_ILLog
    {

        private static readonly string _dllPath = LC_RuntimeManager.DLL_PATH;
        private static readonly List<string> _targetDLL = new List<string> { "Common"};
        private static readonly string _mainDLL = "/Users/xxxx/ScriptAssemblies/";
        private static readonly string _refDLL = "/Users/xxxx/Bin/";
        private static readonly string _assistDLL = "/Users/xxxx/AssistBin/";
        private static readonly List<string> _ignoreList = new List<string> {
                "Update",
                "LateUpdate",
                "Rolling",
            };

#if !ILPROJ
        [MenuItem("LC_Tools/Log/InsertHotDLL", false, 14)]
        public static void InsertHotDLL()
        {
            var insert_count = 0;
            foreach (var item in _targetDLL)
            {
                var path = _dllPath + item + "_dlc.txt";
                insert_count += ProcessDLL(path);
            }

            Debug.LogError($"== InsertHotDLL Complete Count:[{insert_count}]==");
        }

        private static int ProcessDLL(string file_path)
        {

            var ret_count = 0;
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(_refDLL);
            resolver.AddSearchDirectory(_assistDLL);
            resolver.AddSearchDirectory(_mainDLL);
            using (var assembly = AssemblyDefinition.ReadAssembly(file_path, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true }))
            {
                ret_count = ProcessModule(assembly.MainModule);
                assembly.Write();
            }
            return ret_count;
        }

        private static int ProcessModule(ModuleDefinition module)
        {

            var ret_count = 0;
            const string fmt = @"%% [{0}] - [{1}] %%  uuid:[{2}] para:[{3}] ";
            foreach (var classDefine in module.GetTypes())
            {
                foreach (var method in classDefine.Methods)
                {
                    if (method.IsConstructor || method.IsGetter || IsIgnoreFunc(method.Name))
                    {
                        continue;
                    }

                    var methodName = method.FullName;
                    var instructions = method.Body?.Instructions;
                    if (instructions == null)
                    {
                        continue;
                    }

                    ret_count++;
                    var il = method.Body.GetILProcessor();

                    var startStock = new List<Instruction>();
                    //para

                    //uuid
                    //var uuidRef = module.ImportReference(typeof(System.Guid).GetMethod("NewGuid"));

                    var uuid = "--"; //System.Guid.NewGuid().ToString();
                    var paras = "";
                    if (method.HasParameters)
                    {
                        var builder = new StringBuilder();
                        var pd = method.Parameters.ToArray();
                        foreach (var item in pd)
                        {
                            builder.Append(item.ToString()).Append(",");
                        }
                        paras = builder.ToString().Trim(',');
                    }
                    var st_content = string.Format(fmt, classDefine.Name, method.Name, uuid, paras);
                    var st_ldstr = il.Create(OpCodes.Ldstr, st_content);
                    //il.InsertBefore(instructions[0], st_ldstr);
                    startStock.Add(st_ldstr);

                    var patchRef = module.ImportReference(typeof(Debug).GetMethod("Log", new[] { typeof(string) }));
                    var call = il.Create(OpCodes.Call, patchRef);
                    //il.InsertBefore(instructions[0], call);
                    startStock.Add(call);

                    for (var i = startStock.Count - 1; i >= 0; i--)
                    {
                        il.InsertBefore(instructions[0], startStock[i]);
                    }
                }
            }

            return ret_count;
        }

        private static bool IsIgnoreFunc(string name)
        {
            foreach (var item in _ignoreList)
            {
                if (item.Equals(name))
                {
                    return true;
                }
            }
            return false;
        }
#endif
    }
}