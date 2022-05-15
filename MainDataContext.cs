using System.Collections.Concurrent;
using VEDriversLite;
using VEDriversLite.Bookmarks;

namespace VENFT_WebAPI_Test
{
    public static class MainDataContext
    {
        public static List<string> ObservedAccounts { get; set; } = new List<string>();
        public static ConcurrentDictionary<string, ActiveTab> ObservedAccountsTabs { get; set; } = new ConcurrentDictionary<string, ActiveTab>();
    }
}
