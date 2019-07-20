using System.Reflection;

namespace LC_Tools
{
    public class LC_AbstractMethodGenerator : LC_GeneratorBase<MethodInfo>
    {
        protected override bool LoadData(MethodInfo methodInfo)
        {
            if (!CommonProcess(methodInfo))
            {
                return false;
            }

            if (methodInfo.ReturnType.FullName == "System.Void")
            {
                SetKeyValue("{$returnDefault}", "");
            }
            else
            {
                SetKeyValue("{$returnDefault}", "return " + GetTypeNullValue(methodInfo.ReturnType) + ";");
            }

            return true;
        }
    }
}
