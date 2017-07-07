using System.Web;
using System.Web.Mvc;

namespace CrystalGroup.ISD.DocumentManage
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}