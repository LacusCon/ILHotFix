using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LC_Tools
{
    internal class LC_AdaptorHelperGenerator : LC_GeneratorBase<string>
    {
        private readonly LC_AdaptorRegisterGenerator _adaptorRegister;
        public LC_AdaptorHelperGenerator() {
            _adaptorRegister = new LC_AdaptorRegisterGenerator();
        }

        public void CreateFiles()
        {
            var template_path = LC_GeneratorManager.TEMPLATE_PATH + @"lc_adaptor_helper.tmpd";
            LoadTemplateFromFile(template_path);
            var out_path = LC_GeneratorManager.ADAPTOR_OUT_PATH;
            var full_path = new StringBuilder(out_path).Append("LC_AdaptorHelper.cs").ToString();

            if (!Directory.Exists(out_path))
            {
                Directory.CreateDirectory(out_path);
            }

            LoadData("");
            using (var fs = File.Create(full_path))
            {
                var classBody = Generate();
                var sw = new StreamWriter(fs);
                sw.Write(classBody);
                sw.Flush();
            }
        }

        protected override bool LoadData(string type)
        {
            var adaptorSB = new StringBuilder();
            var adaptorList = GetAdaptorList();
            foreach (var t in adaptorList)
            {
                adaptorSB.Append(CreateAdaptorInit(t));
            }
            
            SetKeyValue("{$Namespace}", LC_GeneratorManager.GEN_NAMESPACE);
            SetKeyValue("{$AdaptorInit}", adaptorSB.ToString());
            return true;
        }

        private static List<string> GetAdaptorList()
        {
            var ret_list = new List<string>();
            var file_regex = new Regex(@"^.*[/\\](.*?)\.cs");
            foreach (var full_path in Directory.GetFiles(LC_GeneratorManager.ADAPTOR_OUT_PATH, "*Adaptor.cs", SearchOption.AllDirectories))
            {
                var fixed_name = "";
                if (!file_regex.IsMatch(full_path)) continue;
                fixed_name = file_regex.Match(full_path).Groups[1].Value;
                ret_list.Add(fixed_name);
                //Debug.Log(string.Format(" == GetAdaptorList path:[{0}]  fix:[{1}]== ", full_path, fixed_name));
            }
            return ret_list;
        }

        private string CreateAdaptorInit(string data)
        {
            _adaptorRegister.InitFromFile(LC_GeneratorManager.TEMPLATE_PATH + "lc_adaptor_register.tmpd", data);
            return _adaptorRegister.Generate();
        }
    }
}
