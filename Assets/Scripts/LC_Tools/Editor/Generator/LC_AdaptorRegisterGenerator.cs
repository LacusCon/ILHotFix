namespace LC_Tools
{
    public class LC_AdaptorRegisterGenerator : LC_GeneratorBase<string>
    {
        protected override bool LoadData(string data)
        {
            if (data == null)
                return false;
            SetKeyValue("{$TypeName}", data);
            return true;
        }
    }
}
