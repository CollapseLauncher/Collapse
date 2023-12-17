#if !DISABLEDISCORD
namespace Discord
{
    public partial class ActivityManager
    {
        public void RegisterCommand()
        {
            RegisterCommand(null);
        }
    }
}
#endif