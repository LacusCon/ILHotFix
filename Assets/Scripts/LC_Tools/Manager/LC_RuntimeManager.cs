
#if ILPROJ
using ILRuntime.CLR.Method;
using ILRuntime.CLR.TypeSystem;
using ILRuntime.Runtime.Generated;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace LC_Tools
{
    public class LC_RuntimeManager
    {
        public static readonly string DLL_PATH = Application.dataPath + @"/ILRuntime/DllData/";

#if ILPROJ
        private static ILRuntime.Runtime.Enviorment.AppDomain _appdomain;

        public static ILRuntime.Runtime.Enviorment.AppDomain AppDomain
        {
            get { return _appdomain; }
        }

        public static void LoadAssembly(string model_name)
        {
#if UNITY_EDITOR

            var file_path = DLL_PATH + model_name;
            var dll_data = SyncLoadAssemblyFile(file_path + "_dlc.txt");
            var pdb_data = SyncLoadAssemblyFile(file_path + "_plc.txt");
            pdb_data = null;
            LoadAssembly(dll_data, pdb_data);
#endif
        }

        public static void LoadAssembly(byte[] dll_data, byte[] pdb_data = null)
        {
            if (dll_data == null || dll_data.Length == 0)
            {
                Debug.LogError("== LoadAssembly parameter is null ==");
                return;
            }

            //首先实例化ILRuntime的AppDomain，AppDomain是一个应用程序域，每个AppDomain都是一个独立的沙盒
            if (null == _appdomain)
            {
                _appdomain = new ILRuntime.Runtime.Enviorment.AppDomain();

                //TODO For Debug
                //_appdomain.DebugService.StartDebugService(56000);
                LC_AdaptorHelper.Init(_appdomain);
                LC_Helper.Init(_appdomain);

                SetupCLRRedirection();
                SetupCLRRedirection2();

                CLRBindings.Initialize(_appdomain);
#if UNITY_EDITOR
//                _appdomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                //Debug.Log("m_pcAppDomain.UnityMainThreadID:" + _appdomain.UnityMainThreadID);
#endif
            }

            using (var fs = new MemoryStream(dll_data))
            {
                MemoryStream p = null;
                if (pdb_data != null)
                {
                    p = new MemoryStream(pdb_data);
                }
                _appdomain.LoadAssembly(fs, p, new Mono.Cecil.Pdb.PdbReaderProvider());
            }
        }

        private static unsafe void SetupCLRRedirection()
        {
            //这里面的通常应该写在InitializeILRuntime，这里为了演示写这里
            var arr = typeof(GameObject).GetMethods();
            foreach (var i in arr)
            {
                if (i.Name == "AddComponent" && i.GetGenericArguments().Length == 1)
                {
                    _appdomain.RegisterCLRMethodRedirection(i, AddComponent);
                }
            }
        }

        private static unsafe void SetupCLRRedirection2()
        {
            //这里面的通常应该写在InitializeILRuntime，这里为了演示写这里
            var arr = typeof(GameObject).GetMethods();
            foreach (var i in arr)
            {
                if (i.Name == "GetComponent" && i.GetGenericArguments().Length == 1)
                {
                    _appdomain.RegisterCLRMethodRedirection(i, GetComponent);
                }
            }
        }

        private static unsafe StackObject* AddComponent(ILIntepreter __intp, StackObject* __esp, IList<object> __mStack, CLRMethod __method, bool isNewObj)
        {
            //CLR重定向的说明请看相关文档和教程，这里不多做解释
            ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;

            var ptr = __esp - 1;
            //成员方法的第一个参数为this
            var instance = StackObject.ToObject(ptr, __domain, __mStack) as GameObject;
            if (instance == null)
                throw new System.NullReferenceException();
            __intp.Free(ptr);

            var genericArgument = __method.GenericArguments;
            //AddComponent应该有且只有1个泛型参数
            if (genericArgument == null || genericArgument.Length != 1) return __esp;
            var type = genericArgument[0];
            object res;
            if (type is CLRType)
            {
                //Unity主工程的类不需要任何特殊处理，直接调用Unity接口
                res = instance.AddComponent(type.TypeForCLR);
            }
            else
            {
                //热更DLL内的类型比较麻烦。首先我们得自己手动创建实例
                var ilInstance = new ILTypeInstance(type as ILType, false);//手动创建实例是因为默认方式会new MonoBehaviour，这在Unity里不允许
                //接下来创建Adapter实例
                var clrInstance = instance.AddComponent<MonoBehaviourAdaptor.Adaptor>();
                //unity创建的实例并没有热更DLL里面的实例，所以需要手动赋值
                clrInstance.ILInstance = ilInstance;
                clrInstance.AppDomain = __domain;
                //这个实例默认创建的CLRInstance不是通过AddComponent出来的有效实例，所以得手动替换
                ilInstance.CLRInstance = clrInstance;

                res = clrInstance.ILInstance;//交给ILRuntime的实例应该为ILInstance
                if (instance.activeInHierarchy)
                {
                    clrInstance.Awake();//因为Unity调用这个方法时还没准备好所以这里补调一次
                    clrInstance.OnEnable(); //因为Unity调用这个方法时还没准备好所以这里补调一次
                }
            }

            return ILIntepreter.PushObject(ptr, __mStack, res);

        }

        private static unsafe StackObject* GetComponent(ILIntepreter __intp, StackObject* __esp, IList<object> __mStack, CLRMethod __method, bool isNewObj)
        {
            //CLR重定向的说明请看相关文档和教程，这里不多做解释
            ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;

            var ptr = __esp - 1;
            //成员方法的第一个参数为this
            var instance = StackObject.ToObject(ptr, __domain, __mStack) as GameObject;
            if (instance == null)
                throw new System.NullReferenceException();
            __intp.Free(ptr);

            var genericArgument = __method.GenericArguments;
            //AddComponent应该有且只有1个泛型参数
            if (genericArgument == null || genericArgument.Length != 1) return __esp;
            var type = genericArgument[0];
            object res = null;
            if (type is CLRType)
            {
                //Unity主工程的类不需要任何特殊处理，直接调用Unity接口
                res = instance.GetComponent(type.TypeForCLR);
            }
            else
            {
                //因为所有DLL里面的MonoBehaviour实际都是这个Component，所以我们只能全取出来遍历查找
                var clrInstances = instance.GetComponents<MonoBehaviourAdaptor.Adaptor>();
                for (var i = 0; i < clrInstances.Length; i++)
                {
                    var clrInstance = clrInstances[i];
                    if (clrInstance.ILInstance == null) continue;
                    if (clrInstance.ILInstance.Type != type) continue;
                    res = clrInstance.ILInstance;//交给ILRuntime的实例应该为ILInstance
                    break;
                }
            }

            return ILIntepreter.PushObject(ptr, __mStack, res);

        }

        private static byte[] SyncLoadAssemblyFile(string pszFilePath)
        {
            if (!File.Exists(pszFilePath))
            {
                return null;
            }

            var pcRealStram = new FileStream(pszFilePath, System.IO.FileMode.Open);
            var n64FileLen = pcRealStram.Length;
            var pbData = new byte[n64FileLen];
            pcRealStram.Read(pbData, 0, (int)n64FileLen);
            return pbData;
        }
#endif
    }
}