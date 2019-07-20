using System.Reflection;

namespace LC_Tools
{
    public class LC_VirtualMethodGenerator : LC_GeneratorBase<MethodInfo>
    {
        protected override bool LoadData(MethodInfo methodInfo)
        {
            if (!CommonProcess(methodInfo))
            {
                return false;
            }

            if (methodInfo.ReturnType.FullName != "System.Void")
            {
                SetKeyValue("{$returnDefault}", "return " + GetTypeNullValue(methodInfo.ReturnType) + ";");
                //overide base.{$MethodName} ({$args_no_type});
            }
            return true;
        }
    }
}
