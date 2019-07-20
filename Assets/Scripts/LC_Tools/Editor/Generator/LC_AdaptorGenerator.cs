using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace LC_Tools
{
    class LC_AdaptorGenerator : LC_GeneratorBase<Type>
    {
        private string _filePath;
        private LC_VirtualMethodGenerator _vmg;
        private LC_AbstractMethodGenerator _amg;
        private LC_NormalMethodGenerator _nmg;
        private LC_ValuerGenerator _vg;
        private LC_MessageGenerator _mg;
        
        private readonly string[] _MONO_MESSAGE = {
                "Awake",
                "FixedUpdate",
                "LateUpdate",
                "OnAnimatorIK|int",
                "OnAnimatorMove",
                "OnApplicationFocus|bool",
                "OnApplicationPause|bool",
                "OnApplicationQuit",
                "OnAudioFilterRead|float[], int",
                "OnBecameInvisible",
                "OnBecameVisible",
                "OnCollisionEnter|UnityEngine.Collision",
                "OnCollisionEnter2D|UnityEngine.Collision2D",
                "OnCollisionExit|UnityEngine.Collision",
                "OnCollisionExit2D|UnityEngine.Collision2D",
                "OnCollisionStay|UnityEngine.Collision",
                "OnCollisionStay2D|UnityEngine.Collision2D",
                "OnConnectedToServer",
                "OnControllerColliderHit|UnityEngine.ControllerColliderHit",
                "OnDestroy",
                "OnDisable",
                //"OnDisconnectedFromServer|NetworkDisconnection",
                "OnDrawGizmos",
                "OnDrawGizmosSelected",
                "OnEnable",
                "OnFailedToConnect",
                "OnFailedToConnectToMasterServer",
                "OnGUI",
                "OnJointBreak|float",
                "OnJointBreak2D|UnityEngine.Joint2D",
                //"OnMasterServerEvent",
                "OnMouseDown",
                "OnMouseDrag",
                "OnMouseEnter",
                "OnMouseExit",
                "OnMouseOver",
                "OnMouseUp",
                "OnMouseUpAsButton",
                "OnNetworkInstantiate",
                "OnParticleCollision|UnityEngine.GameObject",
                "OnParticleTrigger",
                //"OnPlayerConnected",
                //"OnPlayerDisconnected",
                "OnPostRender",
                "OnPreCull",
                "OnPreRender",
                "OnRenderImage|UnityEngine.RenderTexture,UnityEngine.RenderTexture",
                "OnRenderObject",
                "OnSerializeNetworkView",
                "OnServerInitialized",
                "OnTransformChildrenChanged",
                "OnTransformParentChanged",
                "OnTriggerEnter|UnityEngine.Collider",
                "OnTriggerEnter2D|UnityEngine.Collider2D",
                "OnTriggerExit|UnityEngine.Collider",
                "OnTriggerExit2D|UnityEngine.Collider2D",
                "OnTriggerStay|UnityEngine.Collider",
                "OnTriggerStay2D|UnityEngine.Collider2D",
                "OnValidate",
                "OnWillRenderObject",
                "Reset",
                "Start",
                "Update",
        };

        private readonly string[] _EXCEPT_METHOD = new string[] {
            //"GetComponent",
        };

        public void CreateFiles(Type[] types, string out_path)
        {
            if (types == null || types.Length == 0)
                return;

            var template_path = LC_GeneratorManager.TEMPLATE_PATH + @"lc_adaptor.tmpd";
            LoadTemplateFromFile(template_path);
            foreach (var type in types)
            {
                var targetName = GetAdaptorName(type);
                var full_path = new StringBuilder(out_path).Append(targetName).Append("Adaptor.cs").ToString();

                if (!Directory.Exists(out_path))
                {
                    Directory.CreateDirectory(out_path);
                }

                LoadData(type);
                using (var fs = File.Create(full_path))
                {
                    var value = Generate();
                    var sw = new StreamWriter(fs);
                    sw.Write(value);
                    sw.Flush();
                }
            }

            Debug.LogWarning($"== Adaptor Create Complete!!! Count:[{types.Length}] ==");
        }

        private string GetAdaptorName(Type type)
        {
            var targetName = type.Name;
            var reg_result = Regex.Match(targetName, @"(^.*)`1");
            if (reg_result.Groups.Count > 1)
            {
                targetName = reg_result.Groups[1].Value;
            }

            return targetName;
        }

        protected override bool LoadTemplateFromFile(string filePath)
        {
            _filePath = Path.GetDirectoryName(filePath);

            _vmg = new LC_VirtualMethodGenerator();
            _amg = new LC_AbstractMethodGenerator();
            _nmg = new LC_NormalMethodGenerator();
            _vg = new LC_ValuerGenerator();
            _mg = new LC_MessageGenerator();
            //_ig = new LC_InterfaceGenerator();

            return base.LoadTemplateFromFile(filePath);
        }

        private Dictionary<string, LC_Valuer> _valuerDict = new Dictionary<string, LC_Valuer>();
        private Regex _valuerRegex = new Regex(@"^[gs]et_(.+)");

        protected override bool LoadData(Type type)
        {
            var methodsBody = "";
            var methods = type.GetMethods();
            _vmg.Clear();
            _amg.Clear();
            _vg.Clear();
            _valuerDict.Clear();

            foreach (var methodInfo in methods.Where(methodInfo => methodInfo.DeclaringType.FullName != "System.Object"))
            {
                if (methodInfo.IsPrivate || methodInfo.IsFamily)
                {
                    continue;
                }

                var atts = methodInfo.GetCustomAttributes(false);
                var isObsolete = false;
                foreach (var attribute in atts.OfType<ObsoleteAttribute>())
                {
                    Debug.LogWarning($"---- Method Has Obsolete Name:[{methodInfo.Name}]  Message:[{attribute.Message}]");
                    isObsolete = true;
                    break;
                }
                if (isObsolete)
                {
                    continue;
                }

                var method_name = methodInfo.Name;
                if (IsExceptMethod(method_name)) continue;
                if (_valuerRegex.IsMatch(method_name))
                {
                    var fixed_name = _valuerRegex.Match(method_name).Groups[1].ToString();
                    if (!_valuerDict.ContainsKey(fixed_name))
                    {
                        _valuerDict.Add(fixed_name, new LC_Valuer());
                    }

                    var vs = _valuerDict[fixed_name];
                    if (Regex.IsMatch(method_name, @"^get_"))
                    {
                        vs.get_method = methodInfo;
                    }
                    else
                    {
                        vs.set_method = methodInfo;
                    }
                }

                else if (methodInfo.IsVirtual)
                {
                    methodsBody += CreateVirtualMethod(methodInfo);
                }
                else
                {
                    methodsBody += CreateNormalMethod(methodInfo);
                }
            }

            foreach (var valuer in _valuerDict)
            {
                methodsBody += CreateValuerMethod(valuer.Value);
            }

            if (type.FullName.Equals("UnityEngine.MonoBehaviour"))
            {
                foreach (var t in _MONO_MESSAGE)
                {
                    methodsBody += CreateMessageMethod(t);
                }
            }

            SetKeyValue("{$Namespace}", LC_GeneratorManager.GEN_NAMESPACE);
            SetKeyValue("{$ClassName}", GetAdaptorName(type));
            SetKeyValue("{$MethodArea}", methodsBody);

            //var interfaceStr = "";
            //foreach (var iface in type.GetInterfaces())
            //{
            //    interfaceStr += CreateInterfaceAdaptor(iface, type);
            //}

            //SetKeyValue("{$Interface}", interfaceStr);
            return true;
        }

        private bool IsExceptMethod(string method_name) {

            for (var i = 0; i < _EXCEPT_METHOD.Length; i++)
            {
                if (method_name.Contains(_EXCEPT_METHOD[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private string CreateVirtualMethod(MethodInfo methodInfo)
        {
            var file = methodInfo.ReturnType.FullName == "System.Void" ? "/lc_virtual_void.tmpd" : "/lc_virtual_return.tmpd";
            _vmg.InitFromFile(_filePath + file, methodInfo);
            return _vmg.Generate();
        }

        //private string CreateAbstractMethod(MethodInfo methodInfo)
        //{
        //    string file = methodInfo.ReturnType.FullName == "System.Void" ? "/lc_normal_void.tmpd" : "/lc_normal_return.tmpd";
        //    _amg.InitFromFile(
        //      _filePath + file, methodInfo);
        //    return _amg.Generate();
        //}

        private string CreateNormalMethod(MethodInfo methodInfo)
        {
            var file = methodInfo.ReturnType.FullName == "System.Void" ? "/lc_normal_void.tmpd" : "/lc_normal_return.tmpd";
            _nmg.InitFromFile(_filePath + file, methodInfo);
            return _nmg.Generate();
        }

        private string CreateValuerMethod(LC_Valuer valuer)
        {
            _vg.InitFromFile(_filePath + "/lc_valuer.tmpd", valuer);
            return _vg.Generate();

        }

        private string CreateMessageMethod(string message_name)
        {
            _mg.InitFromFile(_filePath + "/lc_message.tmpd", message_name);
            return _mg.Generate();
        }
    }
}
