using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LC_Tools
{
    public class LC_NormalMethodGenerator : LC_GeneratorBase<MethodInfo>
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
            }

            return true;
        }
    }

}