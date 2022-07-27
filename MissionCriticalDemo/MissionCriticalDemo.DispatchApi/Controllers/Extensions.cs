namespace MissionCriticalDemo.DispatchApi.Controllers
{
    public static class Extensions
    {

        public static string ToGuidString(this Guid guid)
        {
            return guid.ToString("X");
        }
    }
}
