using System;

namespace LC_Tools
{
    public class LC_InterfaceGenerator : LC_GeneratorBase<Tuple<Type, Type>>
    {
        protected override bool LoadData(Tuple<Type, Type> data)
        {
            if (data == null)
                return false;

            SetKeyValue("{$ClassName}", data.Item1.Name);
            SetKeyValue("{$AdaptorName}", data.Item2.Name + "Adaptor.Adaptor");

            return true;
        }
    }
}
