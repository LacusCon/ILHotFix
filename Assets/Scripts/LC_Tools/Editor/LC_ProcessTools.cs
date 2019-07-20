using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
using UnityEngine.U2D;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using UnityEngine.UI;

#if ILPROJ
using Mono.Cecil;
using ILRuntime.Runtime.Generated;

#endif
namespace LC_Tools
{
    public class LC_ProcessTools
    {
        private static readonly List<string> _modelList = new List<string>
        {
            "ILRuntime", "GameHall", "Common"
        };

        private static readonly Dictionary<string, string> _checkAtlasRepeat = new Dictionary<string, string>();
        private static readonly List<string> _checkDirs = new List<string>
        {
            "GameFramework", "ILRuntime", "GameHall", "Common"
        };
#if ILPROJ
        private static readonly Type[] _adaptorTypes = {typeof(Exception), typeof(MonoBehaviour)};
#endif

        private enum QualityEnum
        {
            None,
            Normal,
            Crunched,
            RGB_ETC4
        }

        [MenuItem("LC_Tools/SpriteAtlas/BuildIn", false, 31)]
        public static void SpriteAtlasBuildIn()
        {
            SpriteAtlasBuildInOrOut(true);
        }

        [MenuItem("LC_Tools/SpriteAtlas/BuildOut", false, 32)]
        public static void SpriteAtlasBuildOut()
        {
            SpriteAtlasBuildInOrOut(false);
        }

        #region Android

        //[MenuItem("LC_Tools/ProcessPicture/AndroidSpriteAtlasCrunched", false, 1)]
        //public static void AndroidSpriteAtlasCrunched()
        //{
        //    ConvertAtlasFormat(BuildTarget.Android, QualityEnum.Crunched);
        //}

        [MenuItem("LC_Tools/Android/SpriteAtlasNormal", false, 31)]
        public static void AndroidSpriteAtlasNormal()
        {
            ConvertAtlasFormat(BuildTarget.Android, QualityEnum.Normal);
        }

        //[MenuItem("LC_Tools/ProcessPicture/PackingAndroid", false, 90)]
        //public static void PackingAndroid()
        //{
        //    SpriteAtlasUtility.PackAllAtlases(BuildTarget.Android);
        //    //SpriteAtlasUtility.PackAllAtlases(BuildTarget.StandaloneWindows64);
        //}

        //[MenuItem("LC_Tools/ProcessPicture/AndroidImageCrunched", false, 10)]
        //public static void AndroidImageCrunched()
        //{
        //    ConvertIMGFormat(BuildTarget.Android, QualityEnum.Crunched);
        //}

        [MenuItem("LC_Tools/Android/ImageNormal", false, 3)]
        public static void AndroidImageNormal()
        {
            ConvertImgFormat(BuildTarget.Android, QualityEnum.Normal);
        }

        [MenuItem("LC_Tools/Android/ImageHigh", false, 4)]
        public static void AndroidImageHigh()
        {
            ConvertImgFormat(BuildTarget.Android, QualityEnum.None);
        }

        //[MenuItem("LC_Tools/Android/RestoreImage", false, 5)]
        //public static void AndroidRestoreImage() {
        //    ConvertIMGFormat(BuildTarget.Android, QualityEnum.None, true);
        //}

        #endregion

        #region IOS

        [MenuItem("LC_Tools/IOS/SpriteAtlasNormal", false, 12)]
        public static void IosSpriteAtlasNormal()
        {
            ConvertAtlasFormat(BuildTarget.iOS, QualityEnum.Normal);
        }

        [MenuItem("LC_Tools/IOS/ImageNormal", false, 13)]
        public static void IosImageNormal()
        {
            ConvertImgFormat(BuildTarget.iOS, QualityEnum.Normal);
        }

        [MenuItem("LC_Tools/IOS/ImageHigh", false, 14)]
        public static void IosImageHigh()
        {
            ConvertImgFormat(BuildTarget.iOS, QualityEnum.None);
        }

        #endregion

        #region Check

        [MenuItem("LC_Tools/Check/CheckALLMissingScript", false, 20)]
        public static void CheckAllMissingScript()
        {
            var dirs = new List<string>();
            GetDirs(Application.dataPath, ref dirs, @"\.prefab$");
            var goList = new List<GameObject>();
            foreach (var t in dirs)
            {
                var go = AssetDatabase.LoadMainAssetAtPath(t) as GameObject;
                if (go != null)
                {
                    goList.Add(go);
                }
            }

            CleanMissingScript(goList.ToArray());
        }

//        [MenuItem("LC_Tools/Check/CheckSelectedMissingScript", false, 21)]
//        public static void CheckSelectedMissingScript()
//        {
//            CleanMissingScript(Selection.gameObjects);
//        }

        [MenuItem("LC_Tools/Check/CheckRepeatResources", false, 22)]
        public static void CheckRepeatResources()
        {
            CheckRepeat();
        }

        [MenuItem("LC_Tools/Check/CheckResourceRelation", false, 23)]
        public static void CheckResourceRelation()
        {
            const string filePattern = @"*.prefab|*.unity";
            var splitPattern = filePattern.Split('|');
            var checkList = new List<string>();
            var resultDict = new Dictionary<string, List<string>>();
            foreach (var t1 in _modelList)
            {
                checkList.Clear();
                var modelName = t1;
                var checkResReg = new Regex(@"Assets/((" + modelName + @")|(Common))/", RegexOptions.IgnoreCase);
                var checkScriptReg = new Regex($@"Assets/Scripts/(({modelName})|(GameFrameWork)|(Common)|(LC_Tools))/", RegexOptions.IgnoreCase);
                var pluginReg = new Regex(@"Assets/Plugins", RegexOptions.IgnoreCase);
                var rootPath = Application.dataPath + Path.AltDirectorySeparatorChar + modelName;

                foreach (var pattern in splitPattern)
                {
                    var fileArray = Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories);
                    checkList.AddRange(fileArray);
                }

                foreach (var t in checkList)
                {
                    var single = t;
                    single = single.Substring(single.IndexOf("Assets", StringComparison.Ordinal));
                    var elements = AssetDatabase.GetDependencies(single);
                    var deps = elements.Where(ele => !checkResReg.IsMatch(ele) && !checkScriptReg.IsMatch(ele) && !pluginReg.IsMatch(ele)).ToList();

                    if (deps.Count > 0)
                    {
                        resultDict.Add(single, deps);
                    }
                }
            }

            if (resultDict.Count > 0)
            {
                var fullPath = Application.streamingAssetsPath + @"/CheckLog/";
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                var fileName = @"Log_" + Time.realtimeSinceStartup + @".log";
                using (var fs = File.Create(fullPath + fileName))
                {
                    var sw = new StreamWriter(fs);
                    foreach (var item in resultDict)
                    {
                        var key = @"KEY::[" + item.Key + @"]";
                        sw.WriteLine(key);
                        foreach (var item2 in item.Value)
                        {
                            var dep = @"------[" + item2 + "]";
                            sw.WriteLine(dep);
                        }

                        sw.WriteLine("");
                    }

                    sw.Flush();
                }
            }

            Debug.LogWarning($"== CheckResourceRelation Finish == Count:[{resultDict.Count}]");
        }

        /// <summary>
        /// Check for non-associated files
        /// </summary>
        [MenuItem("LC_Tools/Check/CheckNonAssociated", false, 30)]
        public static void CheckNonAssociated()
        {
            //Check not in Prefab&Scenes's Script & image & sound
            const string resourcesPattern = @"*.cs|*.ogg|*.mp3|*.mp4|*.wav|*.png|*.jpg|*.jpeg|*.tga|*.ttf|*.anim";
            const string containerPattern = @"*.prefab|*.unity";

            var resourceList = new List<string>();
            //List<string> viewList = new List<string>();
            var containerList = new List<string>();
            foreach (var subDir in _checkDirs)
            {
                var fullPath = Application.dataPath + Path.AltDirectorySeparatorChar + subDir;
                if (!Directory.Exists(fullPath)) continue;
                Platform_resource(fullPath, resourcesPattern, ref resourceList);
                Platform_resource(fullPath, containerPattern, ref containerList);
            }

            foreach (var single in containerList)
            {
                var deps = AssetDatabase.GetDependencies(single);
                foreach (var t in deps)
                {
                    var res = t.Replace(@"\", @"/");
                    if (resourceList.Contains(res))
                    {
                        resourceList.Remove(res);
                    }
                }
            }

            if (resourceList.Count > 0)
            {
                var fullPath = Application.streamingAssetsPath + @"/CheckLog/";
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                var fileName = @"NoAssociated_" + Time.realtimeSinceStartup + @".log";
                using (var fs = File.Create(fullPath + fileName))
                {
                    var sw = new StreamWriter(fs);
                    foreach (var item in resourceList)
                    {
                        var key = @"[" + item + @"]";
                        sw.WriteLine(key);
                    }

                    sw.Flush();
                }
            }

            Debug.LogWarning($"== CheckNonAssociated Finish == Count:[{resourceList.Count}]");
        }

        [MenuItem("LC_Tools/Check/CheckResourcesUsage", false, 30)]
        public static void CheckResourcesUsage()
        {
            //Check not in Prefab&Scenes's Script & image & sound
            const string resourcesPattern = @"*.ttf";
            const string containerPattern = @"*.prefab|*.unity";

            var resourceList = new List<string>();
            var containerList = new List<string>();
            foreach (var subDir in _checkDirs)
            {
                var fullPath = Application.dataPath + Path.AltDirectorySeparatorChar + subDir;
                if (!Directory.Exists(fullPath)) continue;
                Platform_resource(fullPath, resourcesPattern, ref resourceList);
                Platform_resource(fullPath, containerPattern, ref containerList);
            }

            var resDict = new Dictionary<string, List<string>>();
            foreach (var res in resourceList)
            {
                resDict.Add(res, new List<string>());
            }

            foreach (var single in containerList)
            {
                var deps = AssetDatabase.GetDependencies(single);
                foreach (var t in deps)
                {
                    var res = t.Replace(@"\", @"/");
                    if (resDict.ContainsKey(res))
                    {
                        resDict[res].Add(single);
                    }
                }
            }

            if (resDict.Count > 0)
            {
                var fullPath = Application.streamingAssetsPath + @"/CheckLog/";
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                var fileName = @"ResourceUsage_" + Time.realtimeSinceStartup + @".log";
                using (var fs = File.Create(fullPath + fileName))
                {
                    var sw = new StreamWriter(fs);
                    foreach (var item in resDict)
                    {
                        sw.WriteLine($"Key:[{item.Key}]");
                        foreach (var ele in item.Value)
                        {
                            sw.WriteLine($"   ---[{ele}]");
                        }
                    }

                    sw.Flush();
                }
            }

            Debug.LogWarning($"== CheckResourcesUsage Finish == Count:[{resDict.Count}]");
        }

        [MenuItem("LC_Tools/Check/CheckSomeComponent", false, 31)]
        public static void CheckSomeComponent()
        {
            const string containerPattern = @"*.prefab";

            var resourceList = new List<string>();
            foreach (var subDir in _checkDirs)
            {
                var fullPath = Application.dataPath + Path.AltDirectorySeparatorChar + subDir;
                if (!Directory.Exists(fullPath)) continue;
                Platform_resource(fullPath, containerPattern, ref resourceList);
            }

            foreach (var item in resourceList)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(item);
//                var nowSource = go.GetComponents<AudioSource>();
//                var sonSource = go.GetComponentsInChildren<AudioSource>();
//                var nowListener = go.GetComponent<AudioListener>();
//                var sonListener = go.GetComponentInChildren<AudioListener>();
//                if (nowSource.Length > 0 || sonSource.Length > 0 || nowListener != null || sonListener != null)
//                {
//                    Debug.LogWarning($"== CheckSomeComponent Find Audio Component In [{item}] ==");
//                }
                var list = go.GetComponentsInChildren<Text>();
                foreach (var text in list)
                {
                    if (text.font == null)
                    {
                        Debug.LogError($"== CheckSomeComponent Missing Font In [{item}] ==");
                    }
                }
            }
        }

        #endregion

        [MenuItem("LC_Tools/ClearCache", false, 40)]
        public static void ClearCache()
        {
            var defaultCache = Caching.defaultCache;
            Debug.LogWarning($"== Cache Path: [{defaultCache.path}] ==");
            defaultCache.ClearCache();
            AssetDatabase.Refresh();
        }

        [MenuItem("LC_Tools/Build/Android", false, 50)]
        public static void BuildAndroid()
        {
            PackingAssetBundle(BuildTarget.Android);
        }

        [MenuItem("LC_Tools/Build/IOS", false, 51)]
        public static void BuildIos()
        {
            PackingAssetBundle(BuildTarget.iOS);
        }

        [MenuItem("LC_Tools/Build/Windows", false, 52)]
        public static void BuildWin()
        {
            PackingAssetBundle(BuildTarget.StandaloneWindows64);
        }

//        [MenuItem("LC_Tools/Build/Web", false, 53)]
//        public static void BuildWeb()
//        {
//            PackingAssetBundle(BuildTarget.WebGL);
//        }

        #region IL Tools

#if ILPROJ
        /// <summary>
        /// ILRuntime
        /// </summary>
        [MenuItem("ILRuntime/0. Auto Generate")]
        static void AutoGenerate()
        {
            GenerateAdaptor();
            GenerateDelegate();
            GenerateCLRBindingByAnalysis();
            Debug.LogError("== ILRuntime/AutoGenerate Complete!!! ==");
        }

        [MenuItem("ILRuntime/-1. Generate Adaptor")]
        static void GenerateAdaptor()
        {
            var generator = new LC_GeneratorManager();
            generator.GenerateAdaptor(_adaptorTypes);
            Debug.LogWarning("== ILRuntime/GenerateAdaptor Complete!!! ==");
        }

        [MenuItem("ILRuntime/-2. Generate Delegate")]
        static void GenerateDelegate()
        {
            // 委托适配器 将委托实例传给ILRuntime外部使用
            //TODO 根据Unity主程序类生成 DelegateConvertor
            //TOOD 根据IL包生成 DelegateAdapter
            var generator = new LC_GeneratorManager();
            generator.GenerateHelper();
            Debug.LogWarning("== ILRuntime/GenerateDelegate Complete!!! ==");
        }

        /// <summary>
        /// 
        /// </summary>
        [MenuItem("ILRuntime/-3.Generate CLR Binding Code by Analysis")]
        static void GenerateCLRBindingByAnalysis()
        {
            //CLR绑定 从热更DLL中调用Unity主工程或者Unity的接口

            //用新的分析热更dll调用引用来生成绑定代码
            var domain = new ILRuntime.Runtime.Enviorment.AppDomain();
            //string path = Application.dataPath + "/ILRuntime/DllData/";
            foreach (var full_path in Directory.GetFiles(LC_RuntimeManager.DLL_PATH, "*_dlc.txt", SearchOption.TopDirectoryOnly))
            {
                var relative_path = full_path.Substring(full_path.IndexOf("Assets"));
                using (var fs =
                    new FileStream(relative_path, FileMode.Open, FileAccess.Read))
                {
                    domain.LoadAssembly(fs);
                }
            }

            //Crossbind Adaptor is needed to generate the correct binding code
            LC_AdaptorHelper.Init(domain);
            var generated_path = Application.dataPath + "/ILRuntime/Generated/";
            var relative = generated_path.Substring(generated_path.IndexOf("Assets"));
            ILRuntime.Runtime.CLRBinding.BindingCodeGenerator.GenerateBindingCode(domain, relative);
            Debug.LogWarning("== ILRuntime/Generate CLR Binding Code by Analysis Complete!!! ==");
        }

        [MenuItem("ILRuntime/-4.ReadIL")]
        static void ReadILFromDll()
        {
            var path = LC_RuntimeManager.DLL_PATH;
            var method_count = 0;

            foreach (var full_path in Directory.GetFiles(path, "SHZ_dlc.txt", SearchOption.TopDirectoryOnly))
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

                        //foreach (Instruction instruct in instructions)
                        //{
                        //    MethodReference now_inst = instruct.Operand as MethodReference;
                        //    if (now_inst == null) continue;
                        //    string now_all = now_inst.ToString();
                        //    string now_name = now_inst.DeclaringType.FullName;
                        //    string now_ret = now_inst.ReturnType.ToString();
                        //}

                        //TODO
                    }
                }
            }

            //string out_path = Application.streamingAssetsPath + @"/il_content/";
            //if (!Directory.Exists(out_path))
            //{
            //    Directory.CreateDirectory(out_path);
            //}

            //string file_name = @"il_" + Time.realtimeSinceStartup + @".log";
            //using (var fs = File.Create(out_path + file_name))
            //{
            //    var sw = new StreamWriter(fs);
            //    foreach (var item in resourceList)
            //    {
            //        string key = @"[" + item + @"]";
            //        sw.WriteLine(key);
            //    }
            //    sw.Flush();
            //}
        }

#endif

        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static void CheckRepeat()
        {
            var checkRepeatDict = new Dictionary<string, string>();
            var extStr = @"\.((png)|(jpg)|(jpeg)|(tga)|(prefab)|(ogg)|(mp3)|(mp4)|(wav))$";
            var fullPath = @"Assets/(.*?)/.*/(.*)" + extStr;
            foreach (var path in Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories))
            {
                var fixPath = path.Replace(@"\", "/");
                if (!Regex.IsMatch(fixPath, fullPath, RegexOptions.IgnoreCase)) continue;
                var gco = Regex.Match(fixPath, fullPath, RegexOptions.IgnoreCase).Groups;
                var tmpKey = gco[1].Value + "_" + gco[2].Value;
                if (checkRepeatDict.ContainsKey(tmpKey))
                {
                    Debug.LogError($"===发现重名的资源  Name:[{tmpKey}] Path:[{gco[0].Value}]");
                }
                else
                {
                    checkRepeatDict.Add(tmpKey, gco[0].Value);
                }
            }
        }

        private static void SpriteAtlasBuildInOrOut(bool isIn)
        {
#if UNITY_2018
            var dirs = new List<string>();
            GetDirs(Application.dataPath, ref dirs, @"\.spriteatlas$");

            foreach (var t in dirs)
            {
                //Debug.Log(string.Format("== SpriteAtlas 路径为: {0} ==", dirs[i]));
                var sa = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(t);
                sa.SetIncludeInBuild(isIn);
            }

            Debug.LogError($"==== 完成对应品质的Atlas Build 转换: {isIn}====");
#endif
        }

   private static void ConvertAtlasFormat(BuildTarget bt, QualityEnum qe)
        {
#if UNITY_2018
            var dirs = new List<string>();
            GetDirs(Application.dataPath, ref dirs, @"\.spriteatlas$");

            foreach (var t in dirs)
            {
                //Debug.Log(string.Format("== SpriteAtlas 路径为: {0} ==", dirs[i]));
                var sa = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(t);
                sa.SetIncludeInBuild(true); //TODO

                var saps = sa.GetPackingSettings();
                saps.enableRotation = false;
                saps.enableTightPacking = false;
                sa.SetPackingSettings(saps);

                var platName = GetPlatName(bt);
                var tips = sa.GetPlatformSettings(platName);
                if (tips == null) continue;
                tips.overridden = true;
                tips.format = GetImageQuality(bt, qe);
                //tips.compressionQuality = (int)TextureCompressionQuality.Normal; TODO
                sa.SetPlatformSettings(tips);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"==== 完成对应品质的Atlas转换: {qe.ToString()}====");
#endif
        }

        private static string GetPlatName(BuildTarget bt)
        {
            //https://docs.unity3d.com/2018.4/Documentation/ScriptReference/TextureImporterPlatformSettings-name.html
            switch (bt)
            {
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneLinux:
                case BuildTarget.StandaloneWindows64:
                    return "Standalone";
                case BuildTarget.iOS:
                    return "iPhone";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.WebGL:
                    return "WebGL";
                default:
                    return "";
            }
        }

        private static void ConvertImgFormat(BuildTarget bt, QualityEnum qe, bool isRestore = false)
        {
            var imageBase = new TextureImporterPlatformSettings
            {
                overridden = true,
                name = bt.ToString(),
                //maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32,
                //compressionQuality = (int)TextureCompressionQuality.Best, TODO
                allowsAlphaSplitting = false
            };

            var dirs = new List<string>();

            GetDirs(Application.dataPath, ref dirs, @"\.((pn)|(jp))g$");

            foreach (var t in dirs)
            {
                var lowName = t.ToLowerInvariant();
                var valid = false;
                foreach (var t1 in _checkDirs)
                {
                    var lowDir = t1.ToLowerInvariant();
                    if (!lowName.Contains(lowDir)) continue;
                    valid = true;
                    break;
                }

                if (!valid) continue;

                var importer = AssetImporter.GetAtPath(t) as TextureImporter;
                //if (!importer.textureType.Equals(TextureImporterType.Sprite))
                //{
                //    Debug.LogError(string.Format("==检测到非Sprite资源==  path:{0}", dirs[j]));
                //    continue;
                //}

                if (!isRestore)
                {
                    imageBase.format = GetImageQuality(bt, qe, importer.DoesSourceTextureHaveAlpha());
                    importer.SetPlatformTextureSettings(imageBase);
                    importer.mipmapEnabled = false;
                }
                else
                {
                    importer.ClearPlatformTextureSettings(bt.ToString());
                    //importer.SaveAndReimport();
                }

                //image_base = importer.GetPlatformTextureSettings(BuildTarget.Android.ToString());
                //Debug.Log(string.Format("==检测到资源==  path:{0}  overridden:{1}  name:{2} maxTextureSize:{3}  format:{4} compressionQuality:{5}  allowsAlphaSplitting:{6}  crunchedCompression:{7}",
                //    dirs[j], Android_png.overridden,
                //    Android_png.name, Android_png.maxTextureSize,
                //    Android_png.format, Android_png.compressionQuality,
                //    Android_png.allowsAlphaSplitting, Android_png.crunchedCompression));
            }

            AssetDatabase.Refresh();
            //}
            Debug.LogWarning(
                $"==图片转换处理结束==  对象平台：{bt.ToString()}  处理数量：{dirs.Count}  品质：{qe.ToString()}");
        }

        /// <summary>
        /// 获取对应压缩材质的图片
        /// https://docs.unity3d.com/Manual/class-TextureImporterOverride.html
        /// </summary>
        /// <param name="bt"></param>
        /// <param name="qe"></param>
        /// <param name="isAlpha"></param>
        /// <returns></returns>
        private static TextureImporterFormat GetImageQuality(BuildTarget bt, QualityEnum qe, bool isAlpha = true)
        {
            if (qe.Equals(QualityEnum.None))
            {
                return isAlpha ? TextureImporterFormat.RGBA32 : TextureImporterFormat.RGB24;
            }

            switch (bt)
            {
                case BuildTarget.iOS:
                    switch (qe)
                    {
                        case QualityEnum.Normal:
//                            return isAlpha ? TextureImporterFormat.PVRTC_RGBA4 : TextureImporterFormat.PVRTC_RGB4;
                            return isAlpha ? TextureImporterFormat.ASTC_RGBA_4x4 : TextureImporterFormat.ASTC_RGB_4x4;
                        case QualityEnum.Crunched:
                            return isAlpha
                                ? TextureImporterFormat.ETC2_RGBA8Crunched
                                : TextureImporterFormat.ETC_RGB4Crunched;
                        case QualityEnum.RGB_ETC4:
                            return isAlpha
                                ? TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA
                                : TextureImporterFormat.ETC_RGB4;
//                        default:
//                            break;
                    }

                    break;
                case BuildTarget.Android:
                    switch (qe)
                    {
                        case QualityEnum.Normal:
                            return isAlpha ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC_RGB4;
                        case QualityEnum.Crunched:
                            return isAlpha
                                ? TextureImporterFormat.ETC2_RGBA8Crunched
                                : TextureImporterFormat.ETC_RGB4Crunched;
                        case QualityEnum.RGB_ETC4:
                            return isAlpha
                                ? TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA
                                : TextureImporterFormat.ETC_RGB4;
//                        default:
//                            break;
                    }

                    break;

                //case BuildTarget.StandaloneOSX:
                //case BuildTarget.StandaloneWindows:
                //case BuildTarget.StandaloneWindows64:
                //case BuildTarget.StandaloneLinux:
                //case BuildTarget.StandaloneLinux64:
                //case BuildTarget.StandaloneLinuxUniversal:
                //case BuildTarget.PS4:
                //case BuildTarget.XboxOne:
                //    break;
            }

            return TextureImporterFormat.RGBA32;
        }

        private static int go_count, components_count, missing_count;

        /// <summary>
        /// Clean Prefab Missing Script
        /// </summary>
        private static void CleanMissingScript(GameObject[] goArray)
        {
            go_count = 0;
            components_count = 0;
            missing_count = 0;
            if (goArray.Length == 0) return;
            for (var i = 0; i < goArray.Length; i++)
            {
                FindInGo(goArray[i]);
            }

            Debug.LogWarning($"==共找到{go_count}个Go，其中有{missing_count}个Script丢失==");
        }

        private static void FindInGo(GameObject g)
        {
            go_count++;
            var components = g.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                components_count++;
                if (components[i] != null) continue;
                missing_count++;
                var s = g.name;
                var t = g.transform;
                while (t.parent != null)
                {
                    s = t.parent.name + "/" + s;
                    t = t.parent;
                }

                //GameObject.DestroyImmediate(components[i]);
                Debug.Log(s + " has an empty script attached in position: " + i, g);
            }

            // Now recurse through each child GO (if there are any):
            foreach (Transform childT in g.transform)
            {
                //Debug.Log("Searching " + childT.name  + " " );
                FindInGo(childT.gameObject);
            }
        }

        private static void GetDirs(string dirPath, ref List<string> dirs, string regexExt)
        {
            dirs.AddRange(from path in Directory.GetFiles(dirPath)
                let ext = Path.GetExtension(path)
                where Regex.IsMatch(ext, regexExt)
                select path.Substring(path.IndexOf("Assets", StringComparison.Ordinal)));

            if (Directory.GetDirectories(dirPath).Length <= 0) return;
            foreach (var path in Directory.GetDirectories(dirPath))
            {
                GetDirs(path, ref dirs, regexExt);
            }
        }

        private static void PackingAssetBundle(BuildTarget buildTarget)
        {
            var startTime = DateTime.Now;
            AssetDatabase.Refresh();

            var outPath = Application.streamingAssetsPath + "/" + buildTarget;
            const BuildAssetBundleOptions bundleOptions = BuildAssetBundleOptions.DeterministicAssetBundle |
                                                          BuildAssetBundleOptions.ForceRebuildAssetBundle |
                                                          BuildAssetBundleOptions.IgnoreTypeTreeChanges |
                                                          BuildAssetBundleOptions.ChunkBasedCompression;

            const string dirPattern = "AB_*|Texture*|Scene*|Font*|DllData|Prefabs|Audios";
            var ilPatten = "|*_dlc.txt";
//            if (LC_ResourceManager.RUNTIME_DEBUG)
//            {
//                ilPatten = "|*_*lc.txt";
//            }

            var filePattern = "*.spriteatlas|*.prefab|*.unity|*.ogg|*.mp3|*.mp4|*.wav|*.png|*.jpg|*.jpeg|*.tga" + ilPatten;
            //Debug.LogWarning(string.Format("取得的路径是 :{0}", path));

            var pathArray = Directory.GetDirectories(Application.dataPath, "*", SearchOption.TopDirectoryOnly).Where(name => !name.Contains(@"/."));
            var gameList = (from item in pathArray let curDir = item.Substring(item.IndexOf("Assets", StringComparison.Ordinal) + 7) where _modelList.Contains(curDir) select item).ToList();

            var fontList = new List<string>();
            var assetBundleList = new List<AssetBundleBuild>();
            foreach (var item in gameList)
            {
                var tempResList = new List<string>();

                //get font
                Platform_resource(item, "*.ttf", ref fontList);

                var validList = GetValidDirs(item, dirPattern, SearchOption.TopDirectoryOnly);
                foreach (var valid in validList)
                {
                    Platform_resource(valid, filePattern, ref tempResList);
                }

                var curDir = item.Substring(item.IndexOf("Assets", StringComparison.Ordinal) + 7);
                //TODO set mega pack
                SplitAssetBundleWithType(tempResList, curDir, ref assetBundleList);
            }

            if (fontList.Count > 0)
            {
                // 公共资源处理，字体
                var fontAb = new AssetBundleBuild
                {
                    assetBundleName = "font",
                    assetBundleVariant = "",
                    assetNames = fontList.ToArray()
                };
                assetBundleList.Add(fontAb);
            }

            if (Directory.Exists(outPath))
            {
                Directory.Delete(outPath, true);
            }

            Directory.CreateDirectory(outPath);
            BuildPipeline.BuildAssetBundles(outPath, assetBundleList.ToArray(), bundleOptions, buildTarget);
            AssetDatabase.Refresh();
            _checkAtlasRepeat.Clear();
            //BuildPipeline.GetCRCForAssetBundle();
            var useTime = DateTime.Now - startTime;
            Debug.LogError(
                $"==打包完成,打包总数量::  {buildTarget.ToString()}:[{assetBundleList.Count}]  耗时 [{useTime.Minutes}:{useTime.Seconds}]==");
        }

        private static IEnumerable<string> GetValidDirs(string path, string dirPattern, SearchOption option)
        {
            var splitPattern = dirPattern.Split('|');
            return splitPattern.SelectMany(pattern => Directory.GetDirectories(path, pattern, option).Where(name => !name.EndsWith(".meta"))).ToList();
        }

        private static void SplitAssetBundleWithType(IEnumerable<string> resourceList, string pathName,
            ref List<AssetBundleBuild> bundles)
        {
            //"*.spriteatlas|*.prefab|*.unity|*.ogg|*.mp3|*.mp4|*.wav|*.png|*.jpg|*.jpeg";
            var sceneRegex = new Regex(@"\.unity$", RegexOptions.IgnoreCase);
            var prefabRegex = new Regex(@"\.prefab$", RegexOptions.IgnoreCase);
//            var imageRegex = new Regex(@"\.((spriteatlas)|(png)|(jpg)|(jpeg)|(tga))$", RegexOptions.IgnoreCase);
            var imageRegex = new Regex(@"\.((png)|(jpg)|(jpeg)|(tga))$", RegexOptions.IgnoreCase);
            var spriteRegex = new Regex(@"^.*/(.+?)\.spriteatlas$", RegexOptions.IgnoreCase);
            var soundRegex = new Regex(@"\.((ogg)|(mp3)|(mp4)|(wav))$", RegexOptions.IgnoreCase);
            var ilRegex = new Regex(@"^.*/(.+?)_[dp]lc\.txt", RegexOptions.IgnoreCase);

            var sceneList = new List<string>();
            var prefabList = new List<string>();
            var imageList = new List<string>();
            var spriteList = new List<string>();
            var soundList = new List<string>();
            var otherList = new List<string>();
            var ilDict = new Dictionary<string, List<string>>();
            //List<string> il_list = new List<string>();

            // 拆分不同类型资源到不同包
            //根据prefab，spriteatlas来判断公共资源
            foreach (var res in resourceList)
            {
                if (sceneRegex.IsMatch(res))
                {
                    sceneList.Add(res);
                }
                else if (prefabRegex.IsMatch(res))
                {
                    prefabList.Add(res);
                }
                else if (imageRegex.IsMatch(res))
                {
                    imageList.Add(res);
                }
                else if (spriteRegex.IsMatch(res))
                {
                    var name = spriteRegex.Match(res).Groups[1].Value;
                    if (_checkAtlasRepeat.ContainsKey(name))
                    {
                        var message =
                            $"=== Find Repeat Sprite Atlas Name: [{name}] Src:[{_checkAtlasRepeat[name]}] Rep:[{res}]";
                        new InvalidDataException(message);
                    }
                    else
                    {
                        _checkAtlasRepeat.Add(name, res);
                    }

                    spriteList.Add(res);
                }
                else if (soundRegex.IsMatch(res))
                {
                    soundList.Add(res);
                }
                else if (ilRegex.IsMatch(res))
                {
                    var key = ilRegex.Match(res).Groups[1].Value;
                    if (ilDict.ContainsKey(key))
                    {
                        ilDict[key].Add(res);
                    }
                    else
                    {
                        var tmp = new List<string>
                        {
                            res
                        };
                        ilDict.Add(key, tmp);
                    }
                }
                else
                {
                    otherList.Add(res);
                }
            }

            //TODO test
            foreach (var sprite in spriteList)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(sprite);
                var array = new Sprite[atlas.spriteCount];
                atlas.GetSprites(array);
                foreach (var sin in array)
                {
                    var single = sin.name.Split('(')[0];
                    var reg = new Regex($@"/{single}\.");
                    foreach (var image in imageList)
                    {
                        if (!reg.IsMatch(image)) continue;
                        imageList.Remove(image);
                        break;
                    }
                }
            }

            const string variantName = "";
            var preName = pathName + "_";

            if (sceneList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.scenes,
                    assetBundleVariant = variantName, assetNames = sceneList.ToArray()
                });
            }

            if (prefabList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.prefabs,
                    assetBundleVariant = variantName, assetNames = prefabList.ToArray()
                });
            }

            if (imageList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.images,
                    assetBundleVariant = variantName, assetNames = imageList.ToArray()
                });
            }

            if (spriteList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.spriteatlas,
                    assetBundleVariant = variantName, assetNames = spriteList.ToArray()
                });
            }

            if (soundList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.sounds,
                    assetBundleVariant = variantName, assetNames = soundList.ToArray()
                });
            }

            if (ilDict.Count > 0)
            {
                foreach (var item in ilDict)
                {
                    bundles.Add(new AssetBundleBuild
                    {
                        assetBundleName = item.Key + "_" + LC_ResourceManager.ResourceType.il,
                        assetBundleVariant = variantName, assetNames = item.Value.ToArray()
                    });
                }
            }

            if (otherList.Count > 0)
            {
                bundles.Add(new AssetBundleBuild
                {
                    assetBundleName = preName + LC_ResourceManager.ResourceType.others,
                    assetBundleVariant = variantName, assetNames = otherList.ToArray()
                });
            }
        }

        private static string GetRelativePath(string path)
        {
            string ret = null;
            const string pathRegex = @"^.*(Assets.*)$";
            if (Regex.IsMatch(path, pathRegex, RegexOptions.IgnoreCase))
            {
                ret = Regex.Match(path, pathRegex, RegexOptions.IgnoreCase).Groups[1].ToString();
                //Debug.Log(string.Format("==相对路径为 {0}", ret));
            }

            return ret?.Replace(@"\", @"/");
        }

        private static void Platform_resource(string subPath, string filePattern, ref List<string> retList)
        {
            var splitPattern = filePattern.Split('|');
            foreach (var pattern in splitPattern)
            {
                //Debug.Log(string.Format("==现在的pattern：[{0}]==",  pattern));
                var fileArray = Directory.GetFiles(subPath, pattern, SearchOption.AllDirectories).Where(name => !name.Contains(@"/.") && !name.EndsWith(".meta"));
                retList.AddRange(fileArray.Select(GetRelativePath));
            }
        }
    }
}